using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers deserialization for ALL configured providers in the given
/// <see cref="Mode"/>. Phase 37-01 D-02/D-04: defaults to Deploy (source-wins); when Mode="seed",
/// runs destination-wins — rows/pages whose natural key or PageUniqueId is already on target
/// are preserved.
///
/// Use via DW CLI: dw command SerializerDeserialize [mode=seed]
/// Or via Management API: POST /Admin/Api/SerializerDeserialize?mode=seed
/// </summary>
public sealed class SerializerDeserializeCommand : CommandBase
{
    /// <summary>Deployment mode: "deploy" (default) or "seed". Case-insensitive.</summary>
    public string Mode { get; set; } = "deploy";

    /// <summary>
    /// Phase 37-04 STRICT-01: optional strict-mode override. Null = use config.StrictMode,
    /// which itself falls back to the entry-point default (API/CLI default = true, per D-16).
    /// Explicit true/false overrides both.
    /// </summary>
    public bool? StrictMode { get; set; }

    /// <summary>
    /// Phase 37-04: internal flag set by admin-UI action buttons to flip the entry-point
    /// default to AdminUi (lenient). API/CLI callers leave this false so they get the
    /// default-strict behavior. Not serialised to Management API.
    /// </summary>
    public bool IsAdminUiInvocation { get; set; } = false;

    private string? _logFile;
    private readonly List<string> _logLines = new();

    private void Log(string message)
    {
        _logLines.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }

    private void FlushLog(string logFile, LogFileSummary summary)
    {
        LogFileWriter.WriteSummaryHeader(logFile, summary);
        foreach (var line in _logLines)
            File.AppendAllText(logFile, line + "\n");
    }

    public override CommandResult Handle()
    {
        // T-37-01-03: parse mode string strictly before any path interpolation.
        if (!Enum.TryParse<DeploymentMode>(Mode, ignoreCase: true, out var deploymentMode))
        {
            return new()
            {
                Status = CommandResult.ResultType.Invalid,
                Message = $"Invalid mode '{Mode}'. Expected 'deploy' or 'seed' (case-insensitive)."
            };
        }

        // D-38-11: DW CommandBase does not bind query params by default for POST.
        // Fallback: if Mode stayed at the "deploy" default, check the query string.
        // UNCONDITIONAL per D-38-11 + checker blocker B4 — no curl-probe escape hatch.
        if (string.Equals(Mode, "deploy", StringComparison.OrdinalIgnoreCase))
        {
            var fromQuery = Dynamicweb.Context.Current?.Request?["mode"];
            if (!string.IsNullOrEmpty(fromQuery))
            {
                Mode = fromQuery;
                if (!Enum.TryParse<DeploymentMode>(Mode, ignoreCase: true, out deploymentMode))
                {
                    return new()
                    {
                        Status = CommandResult.ResultType.Invalid,
                        Message = $"Invalid mode '{Mode}'. Expected 'deploy' or 'seed' (case-insensitive)."
                    };
                }
            }
        }

        // D-38-11 (extension): honor ?strictMode=true|false if supplied via query string.
        // Only applies when StrictMode is still null (not overridden by the JSON body).
        if (StrictMode is null)
        {
            var strictFromQuery = Dynamicweb.Context.Current?.Request?["strictMode"];
            if (!string.IsNullOrEmpty(strictFromQuery) && bool.TryParse(strictFromQuery, out var strictQ))
            {
                StrictMode = strictQ;
            }
        }

        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "Serializer.config.json not found (also checked ContentSync.config.json)" };

            // Phase 43 / DESER-04: config-free deserialize path. Predicates are no longer
            // consulted; the orchestrator reads the manifest from disk and dispatches per-entry.
            // Mode subfolder is the lowercased mode name (Phase 42 ManifestWriter convention).
            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = SerializerPathResolver.EnsureDirectories(systemDir);

            var modeName = deploymentMode.ToString().ToLowerInvariant();
            var modeRoot = Path.Combine(paths.SerializeRoot, modeName);

            // Conflict strategy is hardcoded per mode (Deploy=SourceWins, Seed=DestinationWins).
            // Pre-Phase-43 this lived on SerializerConfiguration; Phase 43 inlines it here.
            var modeStrategy = DefaultConflictStrategyForMode(deploymentMode);

            _logFile = LogFileWriter.CreateLogFile(paths.Log, "Deserialize");
            Log($"=== Serializer Deserialize (API) started [mode: {deploymentMode}] ===");

            if (!Directory.Exists(modeRoot))
                return new() { Status = CommandResult.ResultType.Error, Message = $"Mode subfolder not found: {modeRoot}" };

            var yamlCount = Directory.GetFiles(modeRoot, "*.yml", SearchOption.AllDirectories).Length;
            if (yamlCount == 0)
                return new() { Status = CommandResult.ResultType.Error, Message = $"{modeRoot} contains no YAML files" };

            // Phase 43 / DESER-05: strict-mode default sourced from entry-point + per-call request
            // override. The configValue: null literal is grep-friendly per CONTEXT line 52 — the
            // config.StrictMode setting is no longer consulted on the deserialize path. A one-time
            // WARNING fires (Task 7) when the legacy setting is still on disk.
            var entryPoint = IsAdminUiInvocation
                ? StrictModeResolver.EntryPoint.AdminUi
                : StrictModeResolver.EntryPoint.Api;
            var strict = StrictModeResolver.Resolve(entryPoint, configValue: null, requestValue: StrictMode);
            Log($"=== Strict mode: {strict} (entry-point: {entryPoint}) ===");

            // Phase 43 / DESER-05 final: emit a one-time WARNING when the on-disk config still
            // carries the legacy strictMode setting. Route through the same log channel as
            // everything else; once-per-run is naturally enforced by command-per-request lifecycle.
            StrictModeDeprecationWarning.EmitIfLegacyValueSet(configPath, Log);

            var escalator = new StrictModeEscalator(strict, Log);

            // Phase 43 / DESER-01: orchestrator reads the manifest itself; no predicates parameter.
            // The envelope's by-ItemType exclusion maps are consulted by the orchestrator (per
            // MANIFEST-05), so the caller no longer threads them in.
            var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
            var result = orchestrator.DeserializeAll(
                modeRoot,
                deploymentMode,
                modeStrategy,
                Log,
                isDryRun: false,
                providerFilter: null,
                escalator: escalator);

            // Build summary with advice and flush log. Phase 43 / REPORT-03: drive off
            // result.EntryOutcomes (canonical) instead of DeserializeResults (transient,
            // Phase 44 deletes).
            var advice = AdviceGenerator.GenerateAdvice(result);
            var summary = new LogFileSummary
            {
                Operation = "Deserialize",
                Timestamp = DateTime.UtcNow,
                DryRun = false,
                Predicates = result.EntryOutcomes.Select(o => new PredicateSummary
                {
                    Name = o.EntryId,
                    Table = o.EntryId,
                    Created = o.Counts.Created,
                    Updated = o.Counts.Updated,
                    Skipped = o.Counts.Skipped,
                    Failed = o.Counts.Failed,
                    Errors = o.Errors.ToList()
                }).ToList(),
                TotalCreated = result.EntryOutcomes.Sum(o => o.Counts.Created),
                TotalUpdated = result.EntryOutcomes.Sum(o => o.Counts.Updated),
                TotalSkipped = result.EntryOutcomes.Sum(o => o.Counts.Skipped),
                TotalFailed = result.EntryOutcomes.Sum(o => o.Counts.Failed),
                Errors = result.Errors.ToList(),
                Advice = advice
            };
            FlushLog(_logFile, summary);

            var message = $"[{deploymentMode}] {result.Summary}";
            if (result.HasErrors)
                message += $" Errors: {string.Join("; ", result.Errors)}";

            // D-38-12: HTTP status is driven by result.HasErrors. Zero-error result maps to Ok
            // regardless of Message content. Phase 43 / REPORT-04 / SC-3: HasErrors now aggregates
            // from EntryOutcomes (any EntryStatus.Failed → true). Test seam at InvokeMapStatusForTest.
            return MapStatusFromResult(result, message);
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Deserialization failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Phase 43 / DESER-04: per-mode default conflict strategy. Replaces
    /// <see cref="SerializerConfiguration.GetConflictStrategyForMode"/> on the deserialize path
    /// (config is no longer consulted). Hardcoded per mode: Deploy=SourceWins,
    /// Seed=DestinationWins. Per-call override is a Phase 44 candidate (D-38-11 ?strictMode=
    /// query-string precedent).
    /// </summary>
    private static ConflictStrategy DefaultConflictStrategyForMode(DeploymentMode mode) =>
        mode == DeploymentMode.Seed ? ConflictStrategy.DestinationWins : ConflictStrategy.SourceWins;

    /// <summary>
    /// D-38-12 test seam: exposes the status-mapping branch of <see cref="Handle"/> so
    /// <c>SerializerDeserializeCommandTests.Handle_ZeroErrors_SynthOrchestratorResult_ReturnsOk</c>
    /// can assert the zero-error == Ok invariant unconditionally against a synthetic
    /// <see cref="OrchestratorResult"/>, without running the full deserialize pipeline.
    /// </summary>
    internal static CommandResult InvokeMapStatusForTest(OrchestratorResult result)
        => MapStatusFromResult(result, result.Summary ?? string.Empty);

    /// <summary>
    /// D-38-12: HTTP status driven by <see cref="OrchestratorResult.HasErrors"/>.
    /// Zero-error result == Ok. Pure function; no side effects.
    /// </summary>
    private static CommandResult MapStatusFromResult(OrchestratorResult result, string message)
    {
        return new CommandResult
        {
            Status = result.HasErrors ? CommandResult.ResultType.Error : CommandResult.ResultType.Ok,
            Message = message
        };
    }
}

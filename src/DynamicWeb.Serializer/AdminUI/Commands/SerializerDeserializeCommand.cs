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

            var config = ConfigLoader.Load(configPath);
            var modeConfig = config.GetMode(deploymentMode);

            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            var modeRoot = Path.Combine(paths.SerializeRoot, modeConfig.OutputSubfolder);

            _logFile = LogFileWriter.CreateLogFile(paths.Log, "Deserialize");
            Log($"=== Serializer Deserialize (API) started [mode: {deploymentMode}] ===");

            if (!Directory.Exists(modeRoot))
                return new() { Status = CommandResult.ResultType.Error, Message = $"Mode subfolder not found: {modeRoot}" };

            var yamlCount = Directory.GetFiles(modeRoot, "*.yml", SearchOption.AllDirectories).Length;
            if (yamlCount == 0)
                return new() { Status = CommandResult.ResultType.Error, Message = $"{modeRoot} contains no YAML files" };

            // Phase 37-04: resolve strict-mode before orchestration.
            var entryPoint = IsAdminUiInvocation
                ? StrictModeResolver.EntryPoint.AdminUi
                : StrictModeResolver.EntryPoint.Api;
            var strict = StrictModeResolver.Resolve(entryPoint, config.StrictMode, StrictMode);
            Log($"=== Strict mode: {strict} (entry-point: {entryPoint}) ===");
            var escalator = new StrictModeEscalator(strict, Log);

            var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
            var result = orchestrator.DeserializeAll(
                modeConfig.Predicates,
                modeRoot,
                deploymentMode,
                modeConfig.ConflictStrategy,
                Log,
                config.DryRun,
                providerFilter: null,
                escalator: escalator,
                excludeFieldsByItemType: modeConfig.ExcludeFieldsByItemType,
                excludeXmlElementsByType: modeConfig.ExcludeXmlElementsByType);

            // Build summary with advice and flush log
            var advice = AdviceGenerator.GenerateAdvice(result);
            var summary = new LogFileSummary
            {
                Operation = "Deserialize",
                Timestamp = DateTime.UtcNow,
                DryRun = config.DryRun,
                Predicates = result.DeserializeResults.Select(r => new PredicateSummary
                {
                    Name = r.TableName,
                    Table = r.TableName,
                    Created = r.Created,
                    Updated = r.Updated,
                    Skipped = r.Skipped,
                    Failed = r.Failed,
                    Errors = r.Errors.ToList()
                }).ToList(),
                TotalCreated = result.DeserializeResults.Sum(r => r.Created),
                TotalUpdated = result.DeserializeResults.Sum(r => r.Updated),
                TotalSkipped = result.DeserializeResults.Sum(r => r.Skipped),
                TotalFailed = result.DeserializeResults.Sum(r => r.Failed),
                Errors = result.Errors.ToList(),
                Advice = advice
            };
            FlushLog(_logFile, summary);

            var message = $"[{deploymentMode}] {result.Summary}";
            if (result.HasErrors)
                message += $" Errors: {string.Join("; ", result.Errors)}";

            // D-38-12: HTTP status is driven by result.HasErrors. Zero-error result maps to Ok
            // regardless of Message content. Guard is in SerializerDeserializeCommandTests
            // (unconditional zero-error == Ok assertion via SynthOrchestratorResult).
            return MapStatusFromResult(result, message);
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Deserialization failed: {ex.Message}" };
        }
    }

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

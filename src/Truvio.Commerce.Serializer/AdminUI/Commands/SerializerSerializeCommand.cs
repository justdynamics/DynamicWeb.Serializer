using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Providers;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers serialization for ALL configured providers in the given
/// <see cref="Mode"/>. Phase 37-01 D-02/D-04: defaults to Deploy; Seed requires explicit opt-in
/// via <c>Mode="seed"</c> (query string, CLI arg, or admin UI action node).
///
/// Use via DW CLI: dw command SerializerSerialize [mode=seed]
/// Or via Management API: POST /Admin/Api/SerializerSerialize?mode=seed
/// </summary>
public sealed class SerializerSerializeCommand : CommandBase
{
    /// <summary>Deployment mode: "deploy" (default) or "seed". Case-insensitive.</summary>
    public string Mode { get; set; } = "deploy";

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
        // T-37-01-03: parse the mode string strictly; reject anything that isn't Deploy or Seed
        // BEFORE any path-interpolation so the string never reaches the filesystem.
        // DIST-04: replace/merge accepted as aliases for deploy/seed (alias-first). The enum
        // drives predicate filtering + conflict strategy; the on-disk subfolder/manifest name
        // is config-driven (GetSubfolderForMode) below, so both names round-trip.
        if (!DeploymentModeAlias.TryResolve(Mode, out var deploymentMode, out _))
        {
            return new()
            {
                Status = CommandResult.ResultType.Invalid,
                Message = $"Invalid mode '{Mode}'. Expected 'deploy'/'replace' or 'seed'/'merge' (case-insensitive)."
            };
        }

        // D-38-11: DW CommandBase does not bind query params by default for POST.
        // Fallback: if Mode stayed at the "deploy" default, check the query string.
        // The fallback ALWAYS lands regardless of local curl probe results — D-38-11 is
        // the locked decision that `?mode=seed` binding is broken today.
        if (string.Equals(Mode, "deploy", StringComparison.OrdinalIgnoreCase))
        {
            var fromQuery = Dynamicweb.Context.Current?.Request?["mode"];
            if (!string.IsNullOrEmpty(fromQuery))
            {
                Mode = fromQuery;
                if (!DeploymentModeAlias.TryResolve(Mode, out deploymentMode, out _))
                {
                    return new()
                    {
                        Status = CommandResult.ResultType.Invalid,
                        Message = $"Invalid mode '{Mode}'. Expected 'deploy'/'replace' or 'seed'/'merge' (case-insensitive)."
                    };
                }
            }
        }

        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "Serializer.config.json not found" };

            var config = ConfigLoader.Load(configPath);

            // Phase 40 D-07: mode-filter the flat predicate list. Replaces the legacy per-mode accessor.
            var modePredicates = config.Predicates.Where(p => p.Mode == deploymentMode).ToList();
            var modeSubfolder = config.GetSubfolderForMode(deploymentMode);
            var modeStrategy = config.GetConflictStrategyForMode(deploymentMode);

            if (modePredicates.Count == 0)
                return new()
                {
                    Status = CommandResult.ResultType.Error,
                    Message = $"No {deploymentMode} predicates configured"
                };

            var filesRoot = ConfigPathResolver.GetFilesRoot(configPath);
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            var modeRoot = Path.Combine(paths.SerializeRoot, modeSubfolder);
            Directory.CreateDirectory(modeRoot);

            _logFile = LogFileWriter.CreateLogFile(paths.Log, "Serialize", deploymentMode.ToString().ToLowerInvariant());
            Log($"=== Serializer Serialize (API) started [mode: {deploymentMode}] ===");

            var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
            var result = orchestrator.SerializeAll(
                modePredicates,
                modeRoot,
                deploymentMode,
                modeStrategy,
                Log,
                providerFilter: null,
                manifestWriter: new ManifestWriter(),
                manifestCleaner: new ManifestCleaner(),
                excludeFieldsByItemType: config.ExcludeFieldsByItemType,
                excludeXmlElementsByType: config.ExcludeXmlElementsByType,
                modeLabel: modeSubfolder);

            var fileCount = Directory.Exists(modeRoot)
                ? Directory.GetFiles(modeRoot, "*.yml", SearchOption.AllDirectories).Length
                : 0;

            // Build summary and flush log
            var summary = new LogFileSummary
            {
                Operation = "Serialize",
                Mode = deploymentMode.ToString().ToLowerInvariant(),
                Timestamp = DateTime.UtcNow,
                Predicates = result.SerializeResults.Select(r => new PredicateSummary
                {
                    Name = r.TableName,
                    Table = r.TableName,
                    Created = r.RowsSerialized
                }).ToList(),
                TotalCreated = result.SerializeResults.Sum(r => r.RowsSerialized),
                Errors = result.Errors.ToList()
            };
            FlushLog(_logFile, summary);

            var message = $"Serialization complete ({deploymentMode}). {fileCount} YAML files written to {modeRoot}. {result.Summary}";
            if (result.StaleFilesDeleted > 0)
                message += $" Cleaned {result.StaleFilesDeleted} stale file(s).";
            if (result.HasErrors)
            {
                // Per-predicate errors drive HasErrors too — surface them, not just the
                // run-level list, so the API response never says a bare "Errors: ".
                var allErrors = result.Errors
                    .Concat(result.SerializeResults
                        .Where(r => r.HasErrors)
                        .SelectMany(r => r.Errors.Select(e => $"{r.TableName}: {e}")));
                message += $" Errors: {string.Join("; ", allErrors)}";
            }

            // D-38-12: HTTP status is driven by result.HasErrors (Errors.Count > 0 ||
            // SerializeResults.Any(r => r.HasErrors)). A zero-error result MUST map to Ok
            // regardless of Message content. The test in SerializerSerializeCommandTests
            // (Handle_ZeroErrors_SynthOrchestratorResult_ReturnsOk) uses SynthOrchestratorResult
            // to assert this unconditionally — no environment-dependent branching.
            return MapStatusFromResult(result, message);
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Serialization failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// D-38-12 test seam: exposes the status-mapping branch of <see cref="Handle"/> so
    /// <c>SerializerSerializeCommandTests.Handle_ZeroErrors_SynthOrchestratorResult_ReturnsOk</c>
    /// can assert the zero-error == Ok invariant unconditionally against a synthetic
    /// <see cref="OrchestratorResult"/>, without running the full serialize pipeline.
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

using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Providers;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers serialization for ALL configured providers.
/// Use via DW CLI: dw command ContentSyncSerialize
/// Or via Management API: POST /Admin/Api/ContentSyncSerialize
///
/// Uses SerializerOrchestrator to dispatch predicates to correct providers (Content, SqlTable, etc.).
/// </summary>
public sealed class ContentSyncSerializeCommand : CommandBase
{
    private string? _logFile;

    private void Log(string message)
    {
        if (_logFile == null) return;
        try { File.AppendAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n"); } catch { }
    }

    public override CommandResult Handle()
    {
        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "ContentSync.config.json not found" };

            var config = ConfigLoader.Load(configPath);

            if (config.Predicates.Count == 0)
                return new() { Status = CommandResult.ResultType.Error, Message = "No predicates configured" };

            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            _logFile = Path.Combine(paths.Log, "ContentSync.log");
            Log("=== ContentSync Serialize (API) started ===");

            var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
            var result = orchestrator.SerializeAll(config.Predicates, paths.SerializeRoot, Log);

            var fileCount = Directory.Exists(paths.SerializeRoot)
                ? Directory.GetFiles(paths.SerializeRoot, "*.yml", SearchOption.AllDirectories).Length
                : 0;

            var message = $"Serialization complete. {fileCount} YAML files written to {config.SerializeRoot}. {result.Summary}";
            if (result.HasErrors)
                message += $" Errors: {string.Join("; ", result.Errors)}";

            return new CommandResult
            {
                Status = result.HasErrors ? CommandResult.ResultType.Error : CommandResult.ResultType.Ok,
                Message = message
            };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Serialization failed: {ex.Message}" };
        }
    }
}

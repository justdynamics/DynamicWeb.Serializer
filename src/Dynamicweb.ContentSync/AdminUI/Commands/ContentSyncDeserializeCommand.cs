using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Providers;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers deserialization for ALL configured providers.
/// Use via DW CLI: dw command ContentSyncDeserialize
/// Or via Management API: POST /Admin/Api/ContentSyncDeserialize
///
/// Uses SerializerOrchestrator to dispatch predicates to correct providers (Content, SqlTable, etc.).
/// </summary>
public sealed class ContentSyncDeserializeCommand : CommandBase
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

            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            _logFile = Path.Combine(paths.Log, "ContentSync.log");
            Log("=== ContentSync Deserialize (API) started ===");

            if (!Directory.Exists(paths.SerializeRoot))
                return new() { Status = CommandResult.ResultType.Error, Message = $"SerializeRoot not found: {paths.SerializeRoot}" };

            var yamlCount = Directory.GetFiles(paths.SerializeRoot, "*.yml", SearchOption.AllDirectories).Length;
            if (yamlCount == 0)
                return new() { Status = CommandResult.ResultType.Error, Message = "SerializeRoot contains no YAML files" };

            var registry = ProviderRegistry.CreateDefault(filesRoot);
            var orchestrator = new SerializerOrchestrator(registry);
            var result = orchestrator.DeserializeAll(config.Predicates, paths.SerializeRoot, Log, config.DryRun);

            var message = result.Summary;
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
            return new() { Status = CommandResult.ResultType.Error, Message = $"Deserialization failed: {ex.Message}" };
        }
    }
}

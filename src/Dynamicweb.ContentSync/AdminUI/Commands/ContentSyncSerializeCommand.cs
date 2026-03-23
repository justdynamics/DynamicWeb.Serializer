using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Serialization;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers ContentSync serialization immediately.
/// Use via DW CLI: dw command ContentSyncSerialize
/// Or via Management API: POST /Admin/Api/ContentSyncSerialize
///
/// Runs full serialization to SerializeRoot/ using all predicates from config.
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
            // Log file path set after config is loaded and paths resolved

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

            var serializeConfig = config with { OutputDirectory = paths.SerializeRoot };
            var serializer = new ContentSerializer(serializeConfig, log: Log);
            serializer.Serialize();

            var fileCount = Directory.GetFiles(paths.SerializeRoot, "*.yml", SearchOption.AllDirectories).Length;

            return new CommandResult
            {
                Status = CommandResult.ResultType.Ok,
                Message = $"Serialization complete. {fileCount} YAML files written to {config.SerializeRoot}"
            };
        }
        catch (Exception ex)
        {
            return new() { Status = CommandResult.ResultType.Error, Message = $"Serialization failed: {ex.Message}" };
        }
    }
}

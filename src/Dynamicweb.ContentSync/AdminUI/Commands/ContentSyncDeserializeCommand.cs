using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Serialization;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers ContentSync deserialization immediately.
/// Use via DW CLI: dw command ContentSyncDeserialize
/// Or via Management API: POST /Admin/Api/ContentSyncDeserialize
///
/// Runs folder-mode deserialization from SerializeRoot/ using the current config.
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

            var effectiveConfig = config with { OutputDirectory = paths.SerializeRoot };
            var deserializer = new ContentDeserializer(effectiveConfig, log: Log, isDryRun: config.DryRun, filesRoot: filesRoot);
            var result = deserializer.Deserialize();

            var message = $"{result.Created} created, {result.Updated} updated, {result.Skipped} skipped, {result.Failed} failed.";
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

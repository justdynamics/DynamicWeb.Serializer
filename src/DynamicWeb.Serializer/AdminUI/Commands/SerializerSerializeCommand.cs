using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Providers;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Commands;

/// <summary>
/// API-callable command that triggers serialization for ALL configured providers.
/// Use via DW CLI: dw command SerializerSerialize
/// Or via Management API: POST /Admin/Api/SerializerSerialize
///
/// Uses SerializerOrchestrator to dispatch predicates to correct providers (Content, SqlTable, etc.).
/// </summary>
public sealed class SerializerSerializeCommand : CommandBase
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
                return new() { Status = CommandResult.ResultType.Error, Message = "Serializer.config.json not found (also checked ContentSync.config.json)" };

            var config = ConfigLoader.Load(configPath);

            if (config.Predicates.Count == 0)
                return new() { Status = CommandResult.ResultType.Error, Message = "No predicates configured" };

            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            _logFile = Path.Combine(paths.Log, "Serializer.log");
            Log("=== Serializer Serialize (API) started ===");

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

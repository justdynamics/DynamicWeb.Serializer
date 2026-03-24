using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Providers;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Scheduling;

namespace Dynamicweb.ContentSync.ScheduledTasks;

[AddInName("ContentSync.Serialize")]
[AddInLabel("ContentSync - Serialize")]
[AddInDescription("Serializes DynamicWeb content and data to YAML files on disk based on ContentSync.config.json predicates. Dispatches all providers (Content, SqlTable) via orchestrator.")]
public class SerializeScheduledTask : BaseScheduledTaskAddIn
{
    private string? _logFile;

    public override bool Run()
    {
        try
        {
            // Log file set after config load and path resolution
            Log($"BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
            Log($"WorkingDirectory: {Directory.GetCurrentDirectory()}");

            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
            {
                Log("ERROR: ContentSync.config.json not found. Searched: application root, App_Data, working directory.");
                return false;
            }

            Log($"Config found: {configPath}");
            var config = ConfigLoader.Load(configPath);

            Log($"OutputDirectory: {config.OutputDirectory}");
            Log($"Predicates: {config.Predicates.Count}");
            foreach (var p in config.Predicates)
                Log($"  Predicate: name={p.Name}, providerType={p.ProviderType}");

            // Resolve and ensure all subdirectories exist
            var filesDir = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesDir, "System");
            var paths = config.EnsureDirectories(systemDir);
            _logFile = Path.Combine(paths.Log, "ContentSync.log");
            Log("=== ContentSync Serialize started ===");
            Log($"SerializeRoot: {paths.SerializeRoot}");

            // Use orchestrator to dispatch all predicates to correct providers
            var registry = ProviderRegistry.CreateDefault(filesDir);
            var orchestrator = new SerializerOrchestrator(registry);
            var result = orchestrator.SerializeAll(config.Predicates, paths.SerializeRoot, Log);

            // Report what was written
            var fileCount = Directory.Exists(paths.SerializeRoot)
                ? Directory.GetFiles(paths.SerializeRoot, "*.yml", SearchOption.AllDirectories).Length
                : 0;
            Log($"Serialization complete. Files written: {fileCount}. {result.Summary}");

            if (result.HasErrors)
            {
                foreach (var error in result.Errors)
                    Log($"ERROR: {error}");
            }

            return !result.HasErrors;
        }
        catch (Exception ex)
        {
            Log($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Log($"Stack: {ex.StackTrace}");
            if (ex.InnerException != null)
                Log($"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            return false;
        }
    }

    private void Log(string message)
    {
        if (_logFile == null) return;
        try
        {
            File.AppendAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
        }
        catch { /* swallow logging failures */ }
    }
}

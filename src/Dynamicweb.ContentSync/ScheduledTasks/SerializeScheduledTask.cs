using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Serialization;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Scheduling;

namespace Dynamicweb.ContentSync.ScheduledTasks;

[AddInName("ContentSync.Serialize")]
[AddInLabel("ContentSync - Serialize")]
[AddInDescription("Serializes DynamicWeb content trees to YAML files on disk based on ContentSync.config.json predicates.")]
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
                Log($"  Predicate: name={p.Name}, path={p.Path}, areaId={p.AreaId}");

            // Resolve and ensure all subdirectories exist
            var filesDir = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesDir, "System");
            var paths = config.EnsureDirectories(systemDir);
            _logFile = Path.Combine(paths.Log, "ContentSync.log");
            Log("=== ContentSync Serialize started ===");
            Log($"SerializeRoot: {paths.SerializeRoot}");

            // Use serializeRoot subfolder for YAML output
            var serializeConfig = config with { OutputDirectory = paths.SerializeRoot };

            var serializer = new ContentSerializer(serializeConfig, log: Log);
            serializer.Serialize();

            // Report what was written
            var fileCount = Directory.Exists(paths.SerializeRoot)
                ? Directory.GetFiles(paths.SerializeRoot, "*.yml", SearchOption.AllDirectories).Length
                : 0;
            Log($"Serialization complete. Files written: {fileCount}");

            return true;
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

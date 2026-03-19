using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Serialization;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Scheduling;

namespace Dynamicweb.ContentSync.ScheduledTasks;

[AddInName("ContentSync.Deserialize")]
[AddInLabel("ContentSync - Deserialize")]
[AddInDescription("Deserializes YAML content files to DynamicWeb database based on ContentSync.config.json predicates.")]
public class DeserializeScheduledTask : BaseScheduledTaskAddIn
{
    private string? _logFile;

    public override bool Run()
    {
        try
        {
            _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.log");
            Log("=== ContentSync Deserialize started ===");
            Log($"BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
            Log($"WorkingDirectory: {Directory.GetCurrentDirectory()}");

            var configPath = FindConfigFile();
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

            var deserializer = new ContentDeserializer(config, log: Log, isDryRun: false);
            var result = deserializer.Deserialize();

            Log(result.Summary);

            if (result.HasErrors)
            {
                foreach (var error in result.Errors)
                    Log(error);
                Log($"Total errors: {result.Errors.Count}");
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

    private string? FindConfigFile()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "wwwroot", "Files", "ContentSync.config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Files", "ContentSync.config.json"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "ContentSync.config.json")
        };

        foreach (var path in candidates)
        {
            Log($"  Checking: {path} -> {(File.Exists(path) ? "FOUND" : "not found")}");
            if (File.Exists(path))
                return path;
        }

        return null;
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

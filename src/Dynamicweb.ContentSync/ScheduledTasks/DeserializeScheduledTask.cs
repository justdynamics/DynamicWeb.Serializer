using System.IO.Compression;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Serialization;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Extensibility.Editors;
using Dynamicweb.Scheduling;

namespace Dynamicweb.ContentSync.ScheduledTasks;

[AddInName("ContentSync.Deserialize")]
[AddInLabel("ContentSync - Deserialize")]
[AddInDescription("Deserializes YAML content files to DynamicWeb database. Default: folder mode (reads from serializeRoot). Set Zip File to a filename in the upload folder for zip-based import.")]
public class DeserializeScheduledTask : BaseScheduledTaskAddIn
{
    private string? _logFile;

    [AddInParameter("Zip File"), AddInParameterEditor(typeof(TextParameterEditor), "inputClass=NewUIinput;infoText=Optional. Filename of a .zip in the upload folder (e.g. import.zip). Leave empty for folder mode.;")]
    public string ZipFileName { get; set; } = string.Empty;

    public override bool Run()
    {
        try
        {
            _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.log");
            Log("=== ContentSync Deserialize started ===");

            var configPath = FindConfigFile();
            if (configPath == null)
            {
                Log("ERROR: ContentSync.config.json not found.");
                return false;
            }

            Log($"Config found: {configPath}");
            var config = ConfigLoader.Load(configPath);

            Log($"OutputDirectory: {config.OutputDirectory}");
            Log($"Predicates: {config.Predicates.Count}");
            foreach (var p in config.Predicates)
                Log($"  Predicate: name={p.Name}, path={p.Path}, areaId={p.AreaId}");

            var filesRoot = Path.GetDirectoryName(configPath);
            var systemDir = Path.Combine(filesRoot ?? ".", "System");
            var paths = config.EnsureDirectories(systemDir);
            Log($"FilesRoot: {filesRoot}");
            Log($"SerializeRoot: {paths.SerializeRoot}");
            Log($"UploadDir: {paths.Upload}");

            bool isZipMode = !string.IsNullOrWhiteSpace(ZipFileName);
            string deserializeDir;
            string? tempExtractDir = null;

            if (isZipMode)
            {
                // Zip mode: resolve filename within the upload subfolder
                var zipPath = Path.Combine(paths.Upload, ZipFileName);

                Log($"Zip mode: {zipPath}");

                if (!File.Exists(zipPath))
                {
                    Log($"ERROR: Zip file not found: {zipPath} (looked in upload folder: {paths.Upload})");
                    return false;
                }

                tempExtractDir = Path.Combine(Path.GetTempPath(), "ContentSync", "import_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempExtractDir);
                Log($"Extracting zip to: {tempExtractDir}");

                ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

                var yamlCount = Directory.GetFiles(tempExtractDir, "*.yml", SearchOption.AllDirectories).Length;
                Log($"Extracted {yamlCount} YAML files from zip");

                if (yamlCount == 0)
                {
                    Log("ERROR: Zip contains no YAML files.");
                    try { Directory.Delete(tempExtractDir, true); } catch { }
                    return false;
                }

                deserializeDir = tempExtractDir;
            }
            else
            {
                // Folder mode (default): deserialize from serializeRoot subfolder
                deserializeDir = paths.SerializeRoot;
                Log($"Folder mode: deserializing from {deserializeDir}");
            }

            try
            {
                var effectiveConfig = config with { OutputDirectory = deserializeDir };

                var deserializer = new ContentDeserializer(effectiveConfig, log: Log, isDryRun: config.DryRun, filesRoot: filesRoot);
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
            finally
            {
                if (tempExtractDir != null)
                {
                    try
                    {
                        Directory.Delete(tempExtractDir, true);
                        Log("Cleaned up temp extraction directory");
                    }
                    catch (Exception ex)
                    {
                        Log($"Warning: Failed to clean up temp dir: {ex.Message}");
                    }
                }
            }
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
        catch { }
    }
}

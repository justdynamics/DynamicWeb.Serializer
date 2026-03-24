using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Serialization;
using Dynamicweb.CoreUI.Data;
using System.IO.Compression;

namespace DynamicWeb.Serializer.AdminUI.Commands;

/// <summary>
/// Command that imports content from an extracted zip directly into the target area.
/// Uses ContentDeserializer directly (not via orchestrator) since zip import is a
/// one-time content import, not a full multi-provider deserialization.
/// Zip is extracted to Files/System/Serializer/ZipImport/ and cleaned up after.
/// </summary>
public sealed class DeserializeFromZipCommand : CommandBase<DeserializeFromZipModel>
{
    public string FilePath { get; set; } = "";

    public int TargetAreaId { get; set; }

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
        try
        {
            if (TargetAreaId <= 0)
                return new() { Status = CommandResult.ResultType.Invalid, Message = "Target area is required" };

            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "Serializer.config.json not found" };

            var config = ConfigLoader.Load(configPath);
            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            // Use the ZipImport directory under System/Serializer/
            var zipImportDir = Path.Combine(filesRoot, "System", "Serializer", "ZipImport");

            // Clean and recreate
            if (Directory.Exists(zipImportDir))
                Directory.Delete(zipImportDir, recursive: true);
            Directory.CreateDirectory(zipImportDir);

            // Extract zip
            var physicalZipPath = Dynamicweb.Core.SystemInformation.MapPath(FilePath);
            if (!File.Exists(physicalZipPath))
                return new() { Status = CommandResult.ResultType.Error, Message = $"Zip file not found: {FilePath}" };

            ZipFile.ExtractToDirectory(physicalZipPath, zipImportDir);

            // Create log file
            var logFile = LogFileWriter.CreateLogFile(paths.Log, "ZipImport");
            Log("=== Serializer ZipImport started ===");
            Log($"Source zip: {FilePath}");
            Log($"Target area: {TargetAreaId}");

            // Build a synthetic predicate for the target area
            // This is a one-time content import — use ContentDeserializer directly
            var importPredicate = new ProviderPredicateDefinition
            {
                Name = "ZipImport",
                ProviderType = "Content",
                AreaId = TargetAreaId,
                Path = "/",
                PageId = 0,
                Excludes = new List<string>()
            };

            var importConfig = new SerializerConfiguration
            {
                OutputDirectory = zipImportDir,
                Predicates = new List<ProviderPredicateDefinition> { importPredicate }
            };

            var deserializer = new ContentDeserializer(importConfig, log: Log, isDryRun: false, filesRoot: filesRoot);
            var result = deserializer.Deserialize();

            // Build summary
            var summary = new LogFileSummary
            {
                Operation = "ZipImport",
                Timestamp = DateTime.UtcNow,
                DryRun = false,
                Predicates = new List<PredicateSummary>
                {
                    new()
                    {
                        Name = "Content Import",
                        Table = "Content",
                        Created = result.Created,
                        Updated = result.Updated,
                        Skipped = result.Skipped,
                        Failed = result.Failed,
                        Errors = result.Errors.ToList()
                    }
                },
                TotalCreated = result.Created,
                TotalUpdated = result.Updated,
                TotalSkipped = result.Skipped,
                TotalFailed = result.Failed,
                Errors = result.Errors.ToList()
            };
            FlushLog(logFile, summary);

            // Clean up ZipImport dir
            try { Directory.Delete(zipImportDir, recursive: true); }
            catch { /* best effort */ }

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
            return new() { Status = CommandResult.ResultType.Error, Message = $"Zip import failed: {ex.Message}" };
        }
    }
}

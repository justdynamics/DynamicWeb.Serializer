using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using Dynamicweb.CoreUI.Data;
using System.IO.Compression;

namespace DynamicWeb.Serializer.AdminUI.Commands;

/// <summary>
/// Command that performs actual deserialization from a zip file after user confirms the dry-run.
/// Called only from DeserializeFromZipScreen after user reviews the dry-run breakdown.
/// Creates a per-run log file with summary and advice, then returns success.
/// </summary>
public sealed class DeserializeFromZipCommand : CommandBase<DeserializeFromZipModel>
{
    public string FilePath { get; set; } = "";

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
        string? tempDir = null;
        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return new() { Status = CommandResult.ResultType.Error, Message = "Serializer.config.json not found" };

            var config = ConfigLoader.Load(configPath);
            var filesRoot = Path.GetDirectoryName(configPath)!;
            var systemDir = Path.Combine(filesRoot, "System");
            var paths = config.EnsureDirectories(systemDir);

            // Resolve physical zip path
            var physicalZipPath = Path.Combine(filesRoot, FilePath.TrimStart('/', '\\'));
            if (!File.Exists(physicalZipPath))
                return new() { Status = CommandResult.ResultType.Error, Message = $"Zip file not found: {FilePath}" };

            if (!string.Equals(Path.GetExtension(physicalZipPath), ".zip", StringComparison.OrdinalIgnoreCase))
                return new() { Status = CommandResult.ResultType.Error, Message = "File is not a .zip archive" };

            // Extract to temp directory
            tempDir = Path.Combine(Path.GetTempPath(), "Serializer_Import_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(physicalZipPath, tempDir);

            // Validate extracted content (D-21)
            var yamlFiles = Directory.GetFiles(tempDir, "*.yml", SearchOption.AllDirectories);
            if (yamlFiles.Length == 0)
            {
                return new()
                {
                    Status = CommandResult.ResultType.Error,
                    Message = "This zip doesn't contain valid serialization data. Expected YAML files matching configured predicates."
                };
            }

            // Create log file
            var logFile = LogFileWriter.CreateLogFile(paths.Log, "DeserializeZip");
            Log("=== Serializer DeserializeZip started ===");
            Log($"Source zip: {FilePath}");

            // Run actual deserialization (NOT dry-run)
            var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
            var result = orchestrator.DeserializeAll(config.Predicates, tempDir, Log, isDryRun: false);

            // Generate advice and build summary
            var advice = AdviceGenerator.GenerateAdvice(result);
            var summary = new LogFileSummary
            {
                Operation = "DeserializeZip",
                Timestamp = DateTime.UtcNow,
                DryRun = false,
                Predicates = result.DeserializeResults.Select(r => new PredicateSummary
                {
                    Name = r.TableName,
                    Table = r.TableName,
                    Created = r.Created,
                    Updated = r.Updated,
                    Skipped = r.Skipped,
                    Failed = r.Failed,
                    Errors = r.Errors.ToList()
                }).ToList(),
                TotalCreated = result.DeserializeResults.Sum(r => r.Created),
                TotalUpdated = result.DeserializeResults.Sum(r => r.Updated),
                TotalSkipped = result.DeserializeResults.Sum(r => r.Skipped),
                TotalFailed = result.DeserializeResults.Sum(r => r.Failed),
                Errors = result.Errors.ToList(),
                Advice = advice
            };
            FlushLog(logFile, summary);

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
            return new() { Status = CommandResult.ResultType.Error, Message = $"Zip deserialization failed: {ex.Message}" };
        }
        finally
        {
            // Clean up temp directory (D-20)
            if (tempDir != null && Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); }
                catch { /* best effort cleanup */ }
            }
        }
    }
}

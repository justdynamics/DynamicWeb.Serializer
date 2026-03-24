using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using Dynamicweb.CoreUI.Data;
using System.IO.Compression;

namespace DynamicWeb.Serializer.AdminUI.Models;

/// <summary>
/// Model for the DeserializeFromZipScreen dialog.
/// Populated on load with dry-run results from the selected zip file.
/// </summary>
public sealed class DeserializeFromZipModel : DataViewModelBase
{
    public string FilePath { get; set; } = "";

    public string FileName { get; set; } = "";

    public bool IsValid { get; set; }

    public string? ValidationError { get; set; }

    public LogFileSummary? DryRunSummary { get; set; }

    /// <summary>
    /// Loads the model by extracting the zip to a temp dir, running a dry-run deserialization,
    /// and building the summary. Cleans up the temp dir afterward.
    /// </summary>
    public static DeserializeFromZipModel LoadDryRun(string filePath)
    {
        var model = new DeserializeFromZipModel
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
            {
                model.ValidationError = "Serializer configuration not found.";
                return model;
            }

            var config = ConfigLoader.Load(configPath);

            // Resolve the physical path from the DW Files path
            var filesRoot = Path.GetDirectoryName(configPath)!;
            var physicalZipPath = Path.Combine(filesRoot, filePath.TrimStart('/', '\\'));

            if (!File.Exists(physicalZipPath))
            {
                model.ValidationError = $"Zip file not found: {filePath}";
                return model;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "Serializer_DryRun_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempDir);
                ZipFile.ExtractToDirectory(physicalZipPath, tempDir);

                // Validate extracted content has YAML files
                var yamlFiles = Directory.GetFiles(tempDir, "*.yml", SearchOption.AllDirectories);
                if (yamlFiles.Length == 0)
                {
                    model.ValidationError = "This zip doesn't contain valid serialization data. Expected YAML files matching configured predicates.";
                    return model;
                }

                model.IsValid = true;

                // Run dry-run deserialization
                var orchestrator = ProviderRegistry.CreateOrchestrator(filesRoot);
                var result = orchestrator.DeserializeAll(config.Predicates, tempDir, log: null, isDryRun: true);

                // Build summary from dry-run result
                model.DryRunSummary = new LogFileSummary
                {
                    Operation = "DeserializeZip (Dry Run)",
                    Timestamp = DateTime.UtcNow,
                    DryRun = true,
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
                    Errors = result.Errors.ToList()
                };
            }
            finally
            {
                // Clean up temp directory
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); }
                    catch { /* best effort cleanup */ }
                }
            }
        }
        catch (Exception ex)
        {
            model.ValidationError = $"Failed to process zip file: {ex.Message}";
        }

        return model;
    }
}

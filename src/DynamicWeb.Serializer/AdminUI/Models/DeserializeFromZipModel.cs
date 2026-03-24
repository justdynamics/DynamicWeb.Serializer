using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Serialization;
using Dynamicweb.CoreUI.Data;
using System.IO.Compression;

namespace DynamicWeb.Serializer.AdminUI.Models;

/// <summary>
/// Model for the DeserializeFromZipScreen.
/// Zip is extracted to Files/System/Serializer/ZipImport/ (cleaned before each import).
/// Dry-run scans the extracted content to show a preview.
/// Actual import uses ContentDeserializer directly with the target area.
/// </summary>
public sealed class DeserializeFromZipModel : DataViewModelBase
{
    public string FilePath { get; set; } = "";

    public string FileName { get; set; } = "";

    [ConfigurableProperty("Target Area", explanation: "DW area to import content into")]
    public string TargetAreaId { get; set; } = "";

    public int TargetAreaIdParsed => int.TryParse(TargetAreaId, out var id) ? id : 0;

    public bool IsValid { get; set; }

    public string? ValidationError { get; set; }

    [ConfigurableProperty("Import Preview", explanation: "Content found in the zip file")]
    public string DryRunText { get; set; } = "";

    /// <summary>
    /// Resolved path to the ZipImport temp directory under Files/System/Serializer/.
    /// </summary>
    public string? ZipImportDir { get; set; }

    /// <summary>
    /// Initial load: validates the zip file exists.
    /// </summary>
    public static DeserializeFromZipModel Load(string filePath)
    {
        var model = new DeserializeFromZipModel
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            var physicalZipPath = Dynamicweb.Core.SystemInformation.MapPath(filePath);
            if (!File.Exists(physicalZipPath))
            {
                model.ValidationError = $"Zip file not found: {filePath}";
                return model;
            }

            model.IsValid = true;
        }
        catch (Exception ex)
        {
            model.ValidationError = $"Failed to validate zip file: {ex.Message}";
        }

        return model;
    }

    /// <summary>
    /// Called by BuildEditScreen after ShadowEdit overlays TargetAreaId.
    /// Extracts zip to ZipImport dir and scans content for preview.
    /// </summary>
    public void ReloadWithArea()
    {
        if (!IsValid || TargetAreaIdParsed <= 0 || !string.IsNullOrEmpty(DryRunText))
            return;

        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
            {
                ValidationError = "Serializer configuration not found.";
                return;
            }

            var filesRoot = Path.GetDirectoryName(configPath)!;
            var zipImportDir = Path.Combine(filesRoot, "System", "Serializer", "ZipImport");
            ZipImportDir = zipImportDir;

            // Clean and recreate the ZipImport directory
            if (Directory.Exists(zipImportDir))
                Directory.Delete(zipImportDir, recursive: true);
            Directory.CreateDirectory(zipImportDir);

            // Extract zip
            var physicalZipPath = Dynamicweb.Core.SystemInformation.MapPath(FilePath);
            ZipFile.ExtractToDirectory(physicalZipPath, zipImportDir);

            // Scan extracted content for preview
            var store = new FileSystemStore();
            var areaDirs = Directory.GetDirectories(zipImportDir);
            if (areaDirs.Length == 0)
            {
                ValidationError = "No content found in zip file.";
                return;
            }

            var area = store.ReadTree(zipImportDir);
            var pageCount = CountPages(area);
            var gridRowCount = CountGridRows(area);

            var lines = new List<string>
            {
                $"Area: {area.Name ?? "Unknown"}",
                $"Pages: {pageCount}",
                $"Grid rows: {gridRowCount}",
                $"Target area: {TargetAreaIdParsed}",
                "",
                "Click 'Save' to import this content into the selected area."
            };

            DryRunText = string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            ValidationError = $"Failed to extract zip: {ex.Message}";
        }
    }

    private static int CountPages(SerializedArea area)
    {
        int count = 0;
        foreach (var page in area.Pages)
            count += CountPagesRecursive(page);
        return count;
    }

    private static int CountPagesRecursive(SerializedPage page)
    {
        int count = 1;
        foreach (var child in page.Children)
            count += CountPagesRecursive(child);
        return count;
    }

    private static int CountGridRows(SerializedArea area)
    {
        int count = 0;
        foreach (var page in area.Pages)
            count += CountGridRowsRecursive(page);
        return count;
    }

    private static int CountGridRowsRecursive(SerializedPage page)
    {
        int count = page.GridRows.Count;
        foreach (var child in page.Children)
            count += CountGridRowsRecursive(child);
        return count;
    }
}

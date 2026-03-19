using Dynamicweb.ContentSync.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Dynamicweb.ContentSync.Infrastructure;

public class FileSystemStore : IContentStore
{
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    // A separate serializer that also omits empty collections, used when writing
    // per-item YAML files (page.yml, area.yml) so that child collections stored
    // in subfolders don't appear as empty lists in the parent file.
    private readonly ISerializer _fileSerializer;

    public FileSystemStore()
    {
        _serializer = YamlConfiguration.BuildSerializer();
        _deserializer = YamlConfiguration.BuildDeserializer();
        _fileSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithEventEmitter(next => new ForceStringScalarEmitter(next))
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
            .Build();
    }

    // -------------------------------------------------------------------------
    // Write
    // -------------------------------------------------------------------------

    public void WriteTree(SerializedArea area, string rootDirectory)
    {
        var areaFolderName = SanitizeFolderName(area.Name);
        var areaDirectory = Path.Combine(rootDirectory, areaFolderName);
        Directory.CreateDirectory(areaDirectory);

        // Write area.yml — omit Pages collection
        var areaForYaml = area with { Pages = new List<SerializedPage>() };
        WriteYamlFile(Path.Combine(areaDirectory, "area.yml"), areaForYaml, omitEmptyCollections: true);

        var usedPageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sortedPages = area.Pages.OrderBy(p => p.SortOrder).ThenBy(p => p.Name);

        foreach (var page in sortedPages)
        {
            var sanitizedPageName = SanitizeFolderName(page.Name);
            var pageFolderName = GetPageFolderName(sanitizedPageName, page.PageUniqueId, usedPageNames);
            var pageDirectory = SafeGetDirectory(areaDirectory, pageFolderName, page.PageUniqueId);
            Directory.CreateDirectory(pageDirectory);

            // Write page.yml — omit GridRows collection; sort Fields keys
            var pageForYaml = page with
            {
                GridRows = new List<SerializedGridRow>(),
                Fields = SortFields(page.Fields)
            };
            WriteYamlFile(Path.Combine(pageDirectory, "page.yml"), pageForYaml, omitEmptyCollections: true);

            // Write grid rows
            var sortedGridRows = page.GridRows.OrderBy(gr => gr.SortOrder);
            foreach (var gridRow in sortedGridRows)
            {
                var gridRowFolderName = $"grid-row-{gridRow.SortOrder}";
                var gridRowDirectory = Path.Combine(pageDirectory, gridRowFolderName);
                Directory.CreateDirectory(gridRowDirectory);

                // Write grid-row.yml — include column metadata inline, but without paragraphs
                var columnsForYaml = gridRow.Columns.Select(col => col with
                {
                    Paragraphs = new List<SerializedParagraph>()
                }).ToList();
                var gridRowForYaml = gridRow with { Columns = columnsForYaml };
                WriteYamlFile(Path.Combine(gridRowDirectory, "grid-row.yml"), gridRowForYaml, omitEmptyCollections: true);

                // Write paragraphs from all columns
                foreach (var column in gridRow.Columns)
                {
                    var sortedParagraphs = column.Paragraphs.OrderBy(p => p.SortOrder);
                    foreach (var paragraph in sortedParagraphs)
                    {
                        var paragraphFileName = $"paragraph-{paragraph.SortOrder}.yml";
                        var paragraphPath = Path.Combine(gridRowDirectory, paragraphFileName);
                        var paragraphForYaml = paragraph with { Fields = SortFields(paragraph.Fields) };
                        WriteYamlFile(paragraphPath, paragraphForYaml);
                    }
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    public SerializedArea ReadTree(string rootDirectory)
    {
        // Find the single area subdirectory
        var areaDirs = Directory.GetDirectories(rootDirectory);
        if (areaDirs.Length == 0)
            throw new InvalidOperationException($"No area directory found in '{rootDirectory}'.");

        var areaDirectory = areaDirs[0];
        var areaYmlPath = Path.Combine(areaDirectory, "area.yml");
        var area = ReadYamlFile<SerializedArea>(areaYmlPath);

        // Find page subdirectories (those containing page.yml)
        var pages = new List<SerializedPage>();
        var subdirs = Directory.GetDirectories(areaDirectory);

        foreach (var subdir in subdirs)
        {
            var pageYmlPath = Path.Combine(subdir, "page.yml");
            if (!File.Exists(pageYmlPath))
                continue;

            var page = ReadYamlFile<SerializedPage>(pageYmlPath);

            // Find grid row subdirectories (those containing grid-row.yml)
            var gridRows = new List<SerializedGridRow>();
            var pageSubdirs = Directory.GetDirectories(subdir);

            foreach (var pageSubdir in pageSubdirs)
            {
                var gridRowYmlPath = Path.Combine(pageSubdir, "grid-row.yml");
                if (!File.Exists(gridRowYmlPath))
                    continue;

                var gridRow = ReadYamlFile<SerializedGridRow>(gridRowYmlPath);

                // Find paragraph files — paragraph-{N}.yml
                var paragraphFiles = Directory.GetFiles(pageSubdir, "paragraph-*.yml")
                    .OrderBy(f => f);

                // Reconstruct columns with paragraphs
                // Paragraphs are written flat in the grid-row folder; we re-attach them to the first column
                // (or recreate the column structure from grid-row.yml)
                var paragraphs = new List<SerializedParagraph>();
                foreach (var paragraphFile in paragraphFiles)
                {
                    var paragraph = ReadYamlFile<SerializedParagraph>(paragraphFile);
                    paragraphs.Add(paragraph);
                }

                // Rebuild columns: the grid-row.yml contains column metadata (without paragraphs)
                // We put all paragraphs back into the columns based on their original association
                // Since we wrote them flat (all paragraphs from all columns to the grid-row folder),
                // we reconstruct a single combined column or distribute by count
                var reconstructedColumns = gridRow.Columns.Count > 0
                    ? ReconstructColumns(gridRow.Columns, paragraphs)
                    : new List<SerializedGridColumn>();

                var fullGridRow = gridRow with { Columns = reconstructedColumns };
                gridRows.Add(fullGridRow);
            }

            var fullPage = page with { GridRows = gridRows };
            pages.Add(fullPage);
        }

        return area with { Pages = pages };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string SanitizeFolderName(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "_unnamed";

        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(trimmed.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static string GetPageFolderName(string sanitizedName, Guid pageGuid, HashSet<string> usedNames)
    {
        if (!usedNames.Contains(sanitizedName))
        {
            usedNames.Add(sanitizedName);
            return sanitizedName;
        }

        var suffix = pageGuid.ToString("N")[..6];
        var dedupedName = $"{sanitizedName} [{suffix}]";
        usedNames.Add(dedupedName);
        return dedupedName;
    }

    private static string SafeGetDirectory(string parentDirectory, string folderName, Guid guid)
    {
        var fullPath = Path.Combine(parentDirectory, folderName);

        // Check directory path length (must be < 248 chars for CreateDirectory on Windows)
        if (fullPath.Length > 247)
        {
            // Truncate folder name to fit within the limit
            var maxFolderLength = 247 - parentDirectory.Length - 1; // -1 for separator
            var suffix = $" [{guid.ToString("N")[..6]}]";
            var truncatedName = folderName.Length > maxFolderLength - suffix.Length
                ? folderName[..(maxFolderLength - suffix.Length)] + suffix
                : folderName;
            fullPath = Path.Combine(parentDirectory, truncatedName);
            Console.Error.WriteLine($"[ContentSync] Warning: Path truncated to fit OS limits: '{fullPath}'");
        }

        return fullPath;
    }

    private static Dictionary<string, object> SortFields(Dictionary<string, object> fields)
        => new(fields.OrderBy(kv => kv.Key, StringComparer.Ordinal));

    private void WriteYamlFile(string path, object value, bool omitEmptyCollections = false)
    {
        // Check full file path length
        if (path.Length > 259)
        {
            Console.Error.WriteLine($"[ContentSync] Warning: File path exceeds 260 chars and may fail: '{path}'");
        }

        var serializer = omitEmptyCollections ? _fileSerializer : _serializer;
        var yaml = serializer.Serialize(value);
        File.WriteAllText(path, yaml, System.Text.Encoding.UTF8);
    }

    private T ReadYamlFile<T>(string path)
    {
        var yaml = File.ReadAllText(path, System.Text.Encoding.UTF8);
        return _deserializer.Deserialize<T>(yaml);
    }

    private static List<SerializedGridColumn> ReconstructColumns(
        List<SerializedGridColumn> columnsWithoutParagraphs,
        List<SerializedParagraph> allParagraphs)
    {
        // When writing, we flatten paragraphs from all columns into the grid-row folder.
        // On read-back we can't tell which paragraph belonged to which column without
        // additional metadata. The simplest correct approach: put all paragraphs into
        // the first column, preserving the other columns (empty).
        // This is lossless for single-column layouts (the most common case).
        // Multi-column paragraph attribution is out of scope for this phase.
        if (columnsWithoutParagraphs.Count == 0)
            return new List<SerializedGridColumn>();

        var result = new List<SerializedGridColumn>();
        result.Add(columnsWithoutParagraphs[0] with { Paragraphs = allParagraphs });
        for (int i = 1; i < columnsWithoutParagraphs.Count; i++)
            result.Add(columnsWithoutParagraphs[i]);

        return result;
    }
}

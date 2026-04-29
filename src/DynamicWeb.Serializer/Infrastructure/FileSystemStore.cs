using DynamicWeb.Serializer.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DynamicWeb.Serializer.Infrastructure;

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
            WritePage(page, areaDirectory, usedPageNames);
        }
    }

    private void WritePage(SerializedPage page, string parentDirectory, HashSet<string> usedNames)
    {
        var sanitizedPageName = SanitizeFolderName(page.Name);
        var pageFolderName = GetPageFolderName(sanitizedPageName, page.PageUniqueId, usedNames);
        var pageDirectory = SafeGetDirectory(parentDirectory, pageFolderName, page.PageUniqueId);
        Directory.CreateDirectory(pageDirectory);

        // Write page.yml — omit GridRows and Children collections; sort Fields keys
        var pageForYaml = page with
        {
            GridRows = new List<SerializedGridRow>(),
            Children = new List<SerializedPage>(),
            Fields = SortFields(page.Fields)
        };
        WriteYamlFile(Path.Combine(pageDirectory, "page.yml"), pageForYaml, omitEmptyCollections: true);

        // Remove stale grid-row folders from prior serialize runs before writing fresh
        // ones. Child-page subdirectories are preserved (they contain a page.yml; grid-row
        // subdirectories contain a grid-row.yml).
        foreach (var existingSubdir in Directory.GetDirectories(pageDirectory))
        {
            if (File.Exists(Path.Combine(existingSubdir, "grid-row.yml")))
                Directory.Delete(existingSubdir, recursive: true);
        }

        // Write grid rows
        // DW allows multiple rows on the same page to share SortOrder (default is 0;
        // templates and manual ordering can collide). Dedupe folder names the same way
        // as sibling page names: append a short GUID suffix on collision to avoid
        // silently overwriting grid-row.yml across rows.
        var usedGridRowNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sortedGridRows = page.GridRows.OrderBy(gr => gr.SortOrder);
        foreach (var gridRow in sortedGridRows)
        {
            var gridRowFolderName = GetGridRowFolderName(gridRow.SortOrder, gridRow.Id, usedGridRowNames);
            var gridRowDirectory = Path.Combine(pageDirectory, gridRowFolderName);
            Directory.CreateDirectory(gridRowDirectory);

            // Write grid-row.yml — include column metadata inline, but without paragraphs
            var columnsForYaml = gridRow.Columns.Select(col => col with
            {
                Paragraphs = new List<SerializedParagraph>()
            }).ToList();
            var gridRowForYaml = gridRow with { Columns = columnsForYaml };
            WriteYamlFile(Path.Combine(gridRowDirectory, "grid-row.yml"), gridRowForYaml, omitEmptyCollections: true);

            // Write paragraphs from all columns — column-aware filenames prevent SortOrder collisions
            foreach (var column in gridRow.Columns)
            {
                var sortedParagraphs = column.Paragraphs.OrderBy(p => p.SortOrder);
                foreach (var paragraph in sortedParagraphs)
                {
                    var paragraphWithColumn = paragraph with { ColumnId = column.Id };
                    var paragraphFileName = $"paragraph-c{column.Id}-{paragraph.SortOrder}.yml";
                    var paragraphPath = Path.Combine(gridRowDirectory, paragraphFileName);
                    var paragraphForYaml = paragraphWithColumn with { Fields = SortFields(paragraphWithColumn.Fields) };
                    WriteYamlFile(paragraphPath, paragraphForYaml);
                }
            }
        }

        // Recursively write child pages — sibling dedup is per-level, not global
        var usedChildNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sortedChildren = page.Children.OrderBy(c => c.SortOrder).ThenBy(c => c.Name);
        foreach (var child in sortedChildren)
        {
            WritePage(child, pageDirectory, usedChildNames);
        }
    }

    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    public SerializedArea ReadTree(string rootDirectory, string? areaName = null)
    {
        var areaDirs = Directory.GetDirectories(rootDirectory);
        if (areaDirs.Length == 0)
            throw new InvalidOperationException($"No area directory found in '{rootDirectory}'.");

        // If area name specified, find the matching subdirectory
        string areaDirectory;
        if (!string.IsNullOrEmpty(areaName))
        {
            areaDirectory = areaDirs.FirstOrDefault(d =>
                Path.GetFileName(d).Equals(areaName, StringComparison.OrdinalIgnoreCase))
                ?? areaDirs[0]; // fall back to first if not found
        }
        else
        {
            areaDirectory = areaDirs[0];
        }
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

            pages.Add(ReadPage(subdir));
        }

        return area with { Pages = pages };
    }

    private SerializedPage ReadPage(string pageDirectory)
    {
        var page = ReadYamlFile<SerializedPage>(Path.Combine(pageDirectory, "page.yml"));

        // Find grid row subdirectories (those containing grid-row.yml)
        var gridRows = new List<SerializedGridRow>();
        // Find child page subdirectories (those containing page.yml)
        var childPages = new List<SerializedPage>();

        var pageSubdirs = Directory.GetDirectories(pageDirectory);

        foreach (var pageSubdir in pageSubdirs)
        {
            var gridRowYmlPath = Path.Combine(pageSubdir, "grid-row.yml");
            if (File.Exists(gridRowYmlPath))
            {
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

                gridRows.Add(gridRow with { Columns = reconstructedColumns });
            }
            else if (File.Exists(Path.Combine(pageSubdir, "page.yml")))
            {
                // Child page subfolder — recurse
                childPages.Add(ReadPage(pageSubdir));
            }
        }

        return page with { GridRows = gridRows, Children = childPages };
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

    private static string GetGridRowFolderName(int sortOrder, Guid gridRowGuid, HashSet<string> usedNames)
    {
        var baseName = $"grid-row-{sortOrder}";
        if (!usedNames.Contains(baseName))
        {
            usedNames.Add(baseName);
            return baseName;
        }

        var suffix = gridRowGuid.ToString("N")[..6];
        var dedupedName = $"{baseName}-{suffix}";
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
            // -1 for the path separator between parent and folder
            var maxFolderLength = 247 - parentDirectory.Length - 1;
            var suffix = $" [{guid.ToString("N")[..6]}]";

            string truncatedName;
            if (maxFolderLength <= suffix.Length)
            {
                // Parent path itself is too long; use only the GUID suffix as folder name
                truncatedName = guid.ToString("N")[..8];
            }
            else
            {
                truncatedName = folderName.Length > maxFolderLength - suffix.Length
                    ? folderName[..(maxFolderLength - suffix.Length)] + suffix
                    : folderName;
            }

            fullPath = Path.Combine(parentDirectory, truncatedName);
            Console.Error.WriteLine($"[Serializer] Warning: Path truncated to fit OS limits: '{fullPath}'");
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
            Console.Error.WriteLine($"[Serializer] Warning: File path exceeds 260 chars and may fail: '{path}'");
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
        // Distribute paragraphs to their original columns using ColumnId.
        // Legacy paragraphs (ColumnId == null) default to the first column.
        if (columnsWithoutParagraphs.Count == 0)
            return new List<SerializedGridColumn>();

        var columnParagraphs = new Dictionary<int, List<SerializedParagraph>>();
        foreach (var col in columnsWithoutParagraphs)
            columnParagraphs[col.Id] = new List<SerializedParagraph>();

        foreach (var para in allParagraphs)
        {
            var targetColumnId = para.ColumnId ?? columnsWithoutParagraphs[0].Id;
            if (columnParagraphs.ContainsKey(targetColumnId))
                columnParagraphs[targetColumnId].Add(para);
            else
                columnParagraphs[columnsWithoutParagraphs[0].Id].Add(para);
        }

        return columnsWithoutParagraphs
            .Select(col => col with { Paragraphs = columnParagraphs[col.Id] })
            .ToList();
    }
}

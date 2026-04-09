using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

public record SerializerConfiguration
{
    /// <summary>
    /// Top-level folder relative to Files/System. Subfolders are managed automatically:
    /// SerializeRoot/ (YAML files), Upload/ (zip imports), Download/ (zip exports).
    /// </summary>
    public required string OutputDirectory { get; init; }
    public string LogLevel { get; init; } = "info";
    public bool DryRun { get; init; } = false;
    public ConflictStrategy ConflictStrategy { get; init; } = ConflictStrategy.SourceWins;
    public required List<ProviderPredicateDefinition> Predicates { get; init; }

    /// <summary>
    /// Global field exclusions by item type name.
    /// Keys are item type system names; values are field name lists.
    /// Merged with per-predicate ExcludeFields at runtime (union).
    /// </summary>
    public Dictionary<string, List<string>> ExcludeFieldsByItemType { get; init; } = new();

    /// <summary>
    /// Global XML element exclusions by XML type name (module system name or URL provider type).
    /// Keys are type names; values are element name lists.
    /// Merged with per-predicate ExcludeXmlElements at runtime (union).
    /// </summary>
    public Dictionary<string, List<string>> ExcludeXmlElementsByType { get; init; } = new();

    /// <summary>Subfolder for YAML serialization files (scheduled tasks read/write here).</summary>
    public string SerializeRoot => Path.Combine(OutputDirectory, "SerializeRoot");

    /// <summary>Subfolder for zip files uploaded for import.</summary>
    public string UploadDir => Path.Combine(OutputDirectory, "Upload");

    /// <summary>Subfolder for zip files produced by ad-hoc serialize.</summary>
    public string DownloadDir => Path.Combine(OutputDirectory, "Download");

    /// <summary>Subfolder for log files.</summary>
    public string LogDir => Path.Combine(OutputDirectory, "Log");

    /// <summary>
    /// Resolves all subfolder paths relative to Files/System and ensures they exist on disk.
    /// Call once after loading config when you need physical paths.
    /// </summary>
    /// <param name="filesSystemDir">Absolute path to Files/System (e.g. wwwroot/Files/System)</param>
    /// <returns>Resolved absolute paths for serializeRoot, upload, download</returns>
    public ResolvedPaths EnsureDirectories(string filesSystemDir)
    {
        var resolved = new ResolvedPaths
        {
            Root = Path.GetFullPath(Path.Combine(filesSystemDir, OutputDirectory.TrimStart('\\', '/'))),
            SerializeRoot = Path.GetFullPath(Path.Combine(filesSystemDir, SerializeRoot.TrimStart('\\', '/'))),
            Upload = Path.GetFullPath(Path.Combine(filesSystemDir, UploadDir.TrimStart('\\', '/'))),
            Download = Path.GetFullPath(Path.Combine(filesSystemDir, DownloadDir.TrimStart('\\', '/'))),
            Log = Path.GetFullPath(Path.Combine(filesSystemDir, LogDir.TrimStart('\\', '/')))
        };

        Directory.CreateDirectory(resolved.Root);
        Directory.CreateDirectory(resolved.SerializeRoot);
        Directory.CreateDirectory(resolved.Upload);
        Directory.CreateDirectory(resolved.Download);
        Directory.CreateDirectory(resolved.Log);

        return resolved;
    }

    public record ResolvedPaths
    {
        public required string Root { get; init; }
        public required string SerializeRoot { get; init; }
        public required string Upload { get; init; }
        public required string Download { get; init; }
        public required string Log { get; init; }
    }
}

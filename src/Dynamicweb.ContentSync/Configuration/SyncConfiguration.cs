namespace Dynamicweb.ContentSync.Configuration;

public record SyncConfiguration
{
    /// <summary>
    /// Top-level folder relative to Files/System. Subfolders are managed automatically:
    /// SerializeRoot/ (YAML files), Upload/ (zip imports), Download/ (zip exports).
    /// </summary>
    public required string OutputDirectory { get; init; }
    public string LogLevel { get; init; } = "info";
    public bool DryRun { get; init; } = false;
    public ConflictStrategy ConflictStrategy { get; init; } = ConflictStrategy.SourceWins;
    public required List<PredicateDefinition> Predicates { get; init; }

    /// <summary>Subfolder for YAML serialization files (scheduled tasks read/write here).</summary>
    public string SerializeRoot => Path.Combine(OutputDirectory, "SerializeRoot");

    /// <summary>Subfolder for zip files uploaded for import.</summary>
    public string UploadDir => Path.Combine(OutputDirectory, "Upload");

    /// <summary>Subfolder for zip files produced by ad-hoc serialize.</summary>
    public string DownloadDir => Path.Combine(OutputDirectory, "Download");

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
            Download = Path.GetFullPath(Path.Combine(filesSystemDir, DownloadDir.TrimStart('\\', '/')))
        };

        Directory.CreateDirectory(resolved.Root);
        Directory.CreateDirectory(resolved.SerializeRoot);
        Directory.CreateDirectory(resolved.Upload);
        Directory.CreateDirectory(resolved.Download);

        return resolved;
    }

    public record ResolvedPaths
    {
        public required string Root { get; init; }
        public required string SerializeRoot { get; init; }
        public required string Upload { get; init; }
        public required string Download { get; init; }
    }
}

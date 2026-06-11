using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>
/// Resolves the on-disk location of Serializer.config.json. The config lives INSIDE the
/// serializer folder (<c>Files/System/Serializer/</c>) so the whole folder — config plus
/// YAML, Upload and Download — travels as one unit: copy it between environments, or upload
/// an example config (e.g. a Swift starter) through the file manager straight into the
/// folder the serializer reads from.
///
/// The location is convention-fixed relative to the Files root and is never derived from
/// the config's own <c>outputDirectory</c> value — that would be circular: the file would
/// define where to find itself. <c>outputDirectory</c> only governs where the data
/// subfolders (SerializeRoot/Upload/Download/Log) are created.
/// </summary>
public static class ConfigPathResolver
{
    public const string FileName = "Serializer.config.json";

    private static readonly string[] HeuristicCandidatePaths =
    {
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "wwwroot", "Files", "System", "Serializer", FileName),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Files", "System", "Serializer", FileName),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName),
        Path.Combine(Directory.GetCurrentDirectory(), FileName)
    };

    /// <summary>
    /// Physical root of the live DW Files archive via <c>SystemInformation.MapPath("/Files")</c>.
    /// This is the authoritative answer on any real host — cloud environments map /Files to a
    /// storage path that has no relationship to BaseDirectory or the working directory, and the
    /// mapped folder is not necessarily NAMED "Files". Null when the DW runtime isn't available
    /// (unit tests, bare processes) or the mapped directory doesn't exist.
    /// </summary>
    internal static string? TryGetDwFilesRoot()
    {
        try
        {
            var mapped = Dynamicweb.Core.SystemInformation.MapPath("/Files");
            return !string.IsNullOrWhiteSpace(mapped) && Directory.Exists(mapped)
                ? Path.GetFullPath(mapped)
                : null;
        }
        catch
        {
            // DependencyResolver not initialized — no DW runtime in this process.
            return null;
        }
    }

    /// <summary>
    /// All candidate config locations, most authoritative first: the DW-mapped Files archive
    /// (live hosts, incl. cloud), then the directory-layout heuristics (tests, bare processes).
    /// </summary>
    private static IEnumerable<string> GetCandidatePaths()
    {
        if (TryGetDwFilesRoot() is string dwRoot)
            yield return Path.Combine(dwRoot, "System", "Serializer", FileName);

        foreach (var path in HeuristicCandidatePaths)
            yield return path;
    }

    /// <summary>
    /// Test-only override, per-async-flow. When non-null, <see cref="FindConfigFile"/> returns this
    /// path directly (skipping the normal candidate-path scan). Uses <see cref="AsyncLocal{T}"/> so
    /// parallel xUnit test workers don't leak overrides into unrelated tests that check the real
    /// candidate-path resolution (e.g. <c>ConfigPathResolverTests</c>).
    /// </summary>
    private static readonly AsyncLocal<string?> _testOverridePath = new();
    public static string? TestOverridePath
    {
        get => _testOverridePath.Value;
        set => _testOverridePath.Value = value;
    }

    /// <summary>
    /// Where a NEW config is created. The DW-mapped Files archive wins when available;
    /// otherwise prefers the heuristic candidate whose Files root actually exists on disk:
    /// the base-directory candidate points into bin\ (AppDomain base = bin\Debug\net10.0\),
    /// while the working-directory candidate is the real wwwroot — creating the config in
    /// bin\ would shadow-resolve forever after. Falls back to the first candidate when no
    /// Files root exists (tests, bare processes).
    /// </summary>
    public static string DefaultPath
    {
        get
        {
            if (TryGetDwFilesRoot() is string dwRoot)
                return Path.GetFullPath(Path.Combine(dwRoot, "System", "Serializer", FileName));

            foreach (var candidate in HeuristicCandidatePaths)
            {
                var filesRoot = GetFilesRoot(candidate);
                if (string.Equals(Path.GetFileName(filesRoot), "Files", StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(filesRoot))
                    return Path.GetFullPath(candidate);
            }
            return Path.GetFullPath(HeuristicCandidatePaths[0]);
        }
    }

    public static string? FindConfigFile()
    {
        var overridePath = TestOverridePath;
        if (overridePath != null)
            return File.Exists(overridePath) ? Path.GetFullPath(overridePath) : null;

        foreach (var path in GetCandidatePaths())
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    /// <summary>
    /// Physical Files root for a resolved config path. The DW-mapped archive root wins when
    /// the config lives under it (cloud hosts map /Files to a folder that is not necessarily
    /// named "Files"); otherwise the nearest ancestor directory named <c>Files</c>. With the
    /// config inside <c>Files/System/Serializer/</c> the file's own directory is no longer
    /// the Files root, so every call site that derives system paths from the config location
    /// goes through this instead of <c>Path.GetDirectoryName</c>. Falls back to the config's
    /// own directory when neither applies (test overrides in temp dirs, bare base-directory
    /// fallback candidates).
    /// </summary>
    public static string GetFilesRoot(string configPath)
    {
        var fullConfigPath = Path.GetFullPath(configPath);
        if (TryGetDwFilesRoot() is string dwRoot
            && fullConfigPath.StartsWith(dwRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return dwRoot;

        var configDir = new DirectoryInfo(Path.GetDirectoryName(fullConfigPath)!);
        for (var dir = configDir; dir is not null; dir = dir.Parent)
        {
            if (string.Equals(dir.Name, "Files", StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
        }
        return configDir.FullName;
    }

    /// <summary>
    /// Returns the existing config, or creates one with an EMPTY predicate list. The
    /// default deliberately syncs nothing — predicates are an explicit decision made on
    /// the settings screen ("Get started") or by editing the file, never a silent
    /// whole-area default.
    /// </summary>
    public static string FindOrCreateConfigFile()
    {
        var existing = FindConfigFile();
        if (existing != null)
            return existing;

        var defaultPath = DefaultPath;
        var defaultConfig = new SerializerConfiguration
        {
            OutputDirectory = SerializerPathResolver.DefaultOutputDirectory,
            Predicates = new List<ProviderPredicateDefinition>()
        };

        ConfigWriter.Save(defaultConfig, defaultPath);
        return defaultPath;
    }
}

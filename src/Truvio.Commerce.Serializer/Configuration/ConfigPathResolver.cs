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

    private static readonly string[] CandidatePaths =
    {
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "wwwroot", "Files", "System", "Serializer", FileName),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Files", "System", "Serializer", FileName),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName),
        Path.Combine(Directory.GetCurrentDirectory(), FileName)
    };

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
    /// Where a NEW config is created. Prefers the candidate whose Files root actually
    /// exists on disk: on a live host the base-directory candidate points into bin\
    /// (AppDomain base = bin\Debug\net10.0\), while the working-directory candidate is the
    /// real wwwroot — creating the config in bin\ would shadow-resolve forever after.
    /// Falls back to the first candidate when no Files root exists (tests, bare processes).
    /// </summary>
    public static string DefaultPath
    {
        get
        {
            foreach (var candidate in CandidatePaths)
            {
                var filesRoot = GetFilesRoot(candidate);
                if (string.Equals(Path.GetFileName(filesRoot), "Files", StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(filesRoot))
                    return Path.GetFullPath(candidate);
            }
            return Path.GetFullPath(CandidatePaths[0]);
        }
    }

    public static string? FindConfigFile()
    {
        var overridePath = TestOverridePath;
        if (overridePath != null)
            return File.Exists(overridePath) ? Path.GetFullPath(overridePath) : null;

        foreach (var path in CandidatePaths)
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    /// <summary>
    /// Physical Files root for a resolved config path: the nearest ancestor directory named
    /// <c>Files</c>. With the config inside <c>Files/System/Serializer/</c> the file's own
    /// directory is no longer the Files root, so every call site that derives system paths
    /// from the config location goes through this instead of <c>Path.GetDirectoryName</c>.
    /// Falls back to the config's own directory when no <c>Files</c> ancestor exists
    /// (test overrides in temp dirs, bare base-directory fallback candidates).
    /// </summary>
    public static string GetFilesRoot(string configPath)
    {
        var configDir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(configPath))!);
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

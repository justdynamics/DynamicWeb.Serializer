using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Configuration;

public static class ConfigPathResolver
{
    private static readonly string[] CandidatePaths =
    {
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "wwwroot", "Files", "Serializer.config.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Files", "Serializer.config.json"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Serializer.config.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "Serializer.config.json"),
        // Backward compat: check old name as fallback
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "wwwroot", "Files", "ContentSync.config.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Files", "ContentSync.config.json"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ContentSync.config.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "ContentSync.config.json")
    };

    /// <summary>
    /// Test-only override. When non-null, <see cref="FindConfigFile"/> returns this path directly
    /// (skipping the normal candidate-path scan). Set by tests that exercise call sites which
    /// don't expose an explicit ConfigPath parameter (e.g. the admin tree node provider).
    /// </summary>
    public static string? TestOverridePath { get; set; }

    public static string DefaultPath => Path.GetFullPath(CandidatePaths[0]);

    public static string? FindConfigFile()
    {
        if (TestOverridePath != null)
            return File.Exists(TestOverridePath) ? Path.GetFullPath(TestOverridePath) : null;

        foreach (var path in CandidatePaths)
        {
            if (File.Exists(path))
                return Path.GetFullPath(path);
        }

        return null;
    }

    public static string FindOrCreateConfigFile()
    {
        var existing = FindConfigFile();
        if (existing != null)
            return existing;

        var defaultPath = DefaultPath;
        var defaultConfig = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            ConflictStrategy = ConflictStrategy.SourceWins,
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", ProviderType = "Content", Path = "/", AreaId = 1 }
            }
        };

        ConfigWriter.Save(defaultConfig, defaultPath);
        return defaultPath;
    }
}

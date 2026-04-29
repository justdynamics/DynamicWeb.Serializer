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

    public static string DefaultPath => Path.GetFullPath(CandidatePaths[0]);

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
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/", AreaId = 1 }
            }
        };

        ConfigWriter.Save(defaultConfig, defaultPath);
        return defaultPath;
    }
}

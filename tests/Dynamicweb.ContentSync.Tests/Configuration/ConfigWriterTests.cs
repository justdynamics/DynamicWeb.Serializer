using Dynamicweb.ContentSync.Configuration;
using Xunit;

namespace Dynamicweb.ContentSync.Tests.Configuration;

public class ConfigWriterTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ConfigWriterTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private SyncConfiguration CreateTestConfig() => new()
    {
        OutputDirectory = "/serialization",
        LogLevel = "debug",
        DryRun = true,
        ConflictStrategy = ConflictStrategy.SourceWins,
        Predicates = new List<PredicateDefinition>
        {
            new()
            {
                Name = "Customer Center",
                Path = "/Customer Center",
                AreaId = 1,
                Excludes = new List<string> { "/Customer Center/Archive" }
            }
        }
    };

    [Fact]
    public void Save_WritesValidJson_ConfigLoaderCanReadBack()
    {
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "roundtrip.json");

        ConfigWriter.Save(config, filePath);
        var loaded = ConfigLoader.Load(filePath);

        Assert.Equal(config.OutputDirectory, loaded.OutputDirectory);
        Assert.Equal(config.LogLevel, loaded.LogLevel);
        Assert.Equal(config.Predicates.Count, loaded.Predicates.Count);
        Assert.Equal(config.Predicates[0].Name, loaded.Predicates[0].Name);
        Assert.Equal(config.Predicates[0].Path, loaded.Predicates[0].Path);
        Assert.Equal(config.Predicates[0].AreaId, loaded.Predicates[0].AreaId);
    }

    [Fact]
    public void Save_UsesAtomicWrite_TempFileDoesNotRemain()
    {
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "atomic.json");

        ConfigWriter.Save(config, filePath);

        Assert.True(File.Exists(filePath));
        Assert.False(File.Exists(filePath + ".tmp"));
    }

    [Fact]
    public void Save_OutputIsCamelCase_MatchesExistingFormat()
    {
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "camelcase.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        Assert.Contains("outputDirectory", json);
        Assert.DoesNotContain("OutputDirectory", json);
        Assert.Contains("predicates", json);
        Assert.DoesNotContain("Predicates", json);
    }

    [Fact]
    public void Save_IndentedOutput_HumanReadable()
    {
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "indented.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        Assert.Contains("\n", json);
        Assert.Contains("  ", json);
    }

    [Fact]
    public void Save_WithExcludes_PreservesExcludeList()
    {
        var config = new SyncConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<PredicateDefinition>
            {
                new()
                {
                    Name = "Test",
                    Path = "/Test",
                    AreaId = 2,
                    Excludes = new List<string> { "/Test/Archive", "/Test/Temp" }
                }
            }
        };
        var filePath = Path.Combine(_tempDir, "excludes.json");

        ConfigWriter.Save(config, filePath);
        var loaded = ConfigLoader.Load(filePath);

        Assert.Equal(2, loaded.Predicates[0].Excludes.Count);
        Assert.Equal("/Test/Archive", loaded.Predicates[0].Excludes[0]);
        Assert.Equal("/Test/Temp", loaded.Predicates[0].Excludes[1]);
    }

    [Fact]
    public void Save_WritesNewFields_JsonContainsDryRunAndConflictStrategy()
    {
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "newfields.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        Assert.Contains("\"dryRun\": true", json);
        Assert.Contains("\"conflictStrategy\": \"source-wins\"", json);
    }

    [Fact]
    public void Save_RoundTrip_PreservesNewFields()
    {
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "roundtrip_new.json");

        ConfigWriter.Save(config, filePath);
        var loaded = ConfigLoader.Load(filePath);

        Assert.True(loaded.DryRun);
        Assert.Equal(ConflictStrategy.SourceWins, loaded.ConflictStrategy);
    }
}

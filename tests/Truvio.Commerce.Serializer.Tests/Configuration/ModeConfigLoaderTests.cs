using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Tests.TestHelpers;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Configuration;

/// <summary>
/// Flat config-shape tests: a single flat predicate list where each predicate carries its own
/// <c>mode</c> (Replace/Merge). A section-level shape (top-level <c>replace</c>/<c>merge</c>
/// objects) is hard-rejected.
/// </summary>
public class ModeConfigLoaderTests : ConfigLoaderValidatorFixtureBase
{
    private readonly string _tempDir;

    public ModeConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ModeConfigLoaderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteConfigFile(string json)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    // -------------------------------------------------------------------------
    // Hard-reject the section-level shape (top-level 'replace' / 'merge' object)
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_ReplaceSection_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "replace": {
                "predicates": [
                  { "name": "X", "path": "/Shop", "areaId": 1 }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("Section-level shape", ex.Message);
        Assert.Contains("'replace'", ex.Message);
        Assert.Contains("flat shape", ex.Message);
    }

    [Fact]
    public void Load_MergeSection_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "merge": {
                "predicates": [
                  { "name": "X", "path": "/Shop", "areaId": 1 }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("Section-level shape", ex.Message);
        Assert.Contains("'merge'", ex.Message);
    }

    [Fact]
    public void Load_ReplaceValue_AnyShape_Throws()
    {
        // Detection trap: object? on Replace/Merge catches any JSON shape — array, primitive, object.
        var json = """
            {
              "outputDirectory": "/serialization",
              "replace": []
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));
        Assert.Contains("Section-level shape", ex.Message);
    }

    // -------------------------------------------------------------------------
    // Per-predicate mode is required + must parse to SerializerMode
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_PredicateMissingMode_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "NoMode", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("missing required field 'mode'", ex.Message);
        Assert.Contains("NoMode", ex.Message);
        Assert.Contains("expected 'Replace' or 'Merge'", ex.Message);
    }

    [Fact]
    public void Load_PredicateInvalidMode_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "BadMode", "mode": "Garbage", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("Unknown mode 'Garbage'", ex.Message);
        Assert.Contains("BadMode", ex.Message);
        Assert.Contains("valid values: Replace, Merge", ex.Message);
    }

    [Fact]
    public void Load_PredicateInjectionMode_Throws()
    {
        // T-40-01-02: free-form mode strings cannot reach SerializerConfiguration. Closed-set parse.
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "Inj", "mode": "Replace; DROP TABLE X", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));
        Assert.Contains("Unknown mode", ex.Message);
    }

    [Fact]
    public void Load_LegacyDeployModeValue_Throws()
    {
        // The old 'Deploy' mode value is no longer accepted — only Replace/Merge.
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "Old", "mode": "Deploy", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));
        Assert.Contains("Unknown mode 'Deploy'", ex.Message);
        Assert.Contains("valid values: Replace, Merge", ex.Message);
    }

    [Fact]
    public void Load_LegacySeedModeValue_Throws()
    {
        // The old 'Seed' mode value is no longer accepted — only Replace/Merge.
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "Old", "mode": "Seed", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));
        Assert.Contains("Unknown mode 'Seed'", ex.Message);
        Assert.Contains("valid values: Replace, Merge", ex.Message);
    }

    [Fact]
    public void Load_LowercaseReplaceMode_AcceptedAsReplace()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "L", "mode": "replace", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Predicates);
        Assert.Equal(SerializerMode.Replace, config.Predicates[0].Mode);
    }

    [Fact]
    public void Load_UppercaseMergeMode_AcceptedAsMerge()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "U", "mode": "MERGE", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Predicates);
        Assert.Equal(SerializerMode.Merge, config.Predicates[0].Mode);
    }

    // -------------------------------------------------------------------------
    // Flat-shape success cases with mixed Replace/Merge predicates
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_NewFlatShape_MixedPredicates_LoadsCorrectModes()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "EcomShops",  "mode": "Replace", "providerType": "SqlTable", "table": "EcomShops" },
                { "name": "EcomOrderFlow", "mode": "Merge", "providerType": "SqlTable", "table": "EcomOrderFlow", "nameColumn": "OrderFlowName" },
                { "name": "ContentReplace", "mode": "Replace", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(3, config.Predicates.Count);
        Assert.Equal(2, config.Predicates.Count(p => p.Mode == SerializerMode.Replace));
        Assert.Equal(1, config.Predicates.Count(p => p.Mode == SerializerMode.Merge));
        Assert.Equal("EcomOrderFlow", config.Predicates.Single(p => p.Mode == SerializerMode.Merge).Name);
    }

    [Fact]
    public void Load_FlatShape_DefaultSubfolders_AreReplaceAndMerge()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal("replace", config.ReplaceOutputSubfolder);
        Assert.Equal("merge", config.MergeOutputSubfolder);
    }

    [Fact]
    public void Load_FlatShape_CustomSubfolders_RoundTrip()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "replaceOutputSubfolder": "shipped",
              "mergeOutputSubfolder": "fixtures",
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal("shipped", config.ReplaceOutputSubfolder);
        Assert.Equal("fixtures", config.MergeOutputSubfolder);
    }

    [Fact]
    public void Load_FlatShape_TopLevelExclusionDictionaries_RoundTrip()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "excludeFieldsByItemType": {
                "Swift_PageItemType": ["NavigationTag", "AreaDomain"]
              },
              "excludeXmlElementsByType": {
                "Dynamicweb.Frontend.ContentPage": ["sort"]
              },
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.ExcludeFieldsByItemType);
        Assert.Equal(new List<string> { "NavigationTag", "AreaDomain" }, config.ExcludeFieldsByItemType["Swift_PageItemType"]);
        Assert.Single(config.ExcludeXmlElementsByType);
        Assert.Equal(new List<string> { "sort" }, config.ExcludeXmlElementsByType["Dynamicweb.Frontend.ContentPage"]);
    }

    [Fact]
    public void Load_FlatShape_NoExclusionDicts_DefaultsEmpty()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.NotNull(config.ExcludeFieldsByItemType);
        Assert.Empty(config.ExcludeFieldsByItemType);
        Assert.NotNull(config.ExcludeXmlElementsByType);
        Assert.Empty(config.ExcludeXmlElementsByType);
    }

    // -------------------------------------------------------------------------
    // Round-trip via ConfigWriter — the writer never emits the section shape
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_FlatShape_RoundTrips_WithMixedModes()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "ReplaceP", Mode = SerializerMode.Replace, ProviderType = "Content", Path = "/Shop", AreaId = 1 },
                new() { Name = "MergeP",   Mode = SerializerMode.Merge,   ProviderType = "Content", Path = "/Customer", AreaId = 1 }
            },
            ExcludeFieldsByItemType = new Dictionary<string, List<string>>
            {
                ["Swift_PageItemType"] = new() { "NavigationTag" }
            }
        };
        var path = Path.Combine(_tempDir, "roundtrip.json");

        ConfigWriter.Save(config, path);
        var reloaded = ConfigLoader.Load(path);

        Assert.Equal(2, reloaded.Predicates.Count);
        Assert.Equal(SerializerMode.Replace, reloaded.Predicates.Single(p => p.Name == "ReplaceP").Mode);
        Assert.Equal(SerializerMode.Merge, reloaded.Predicates.Single(p => p.Name == "MergeP").Mode);
        Assert.Single(reloaded.ExcludeFieldsByItemType);
    }
}

using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Tests.TestHelpers;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

/// <summary>
/// Phase 40 (D-01..D-04) flat config-shape tests. Replaces the section-level Deploy/Seed split
/// with a single flat predicate list where each predicate carries its own <c>mode</c>.
/// Hard-rejects the legacy section shape (no backcompat per project policy).
///
/// Class name kept as <c>DeployModeConfigLoaderTests</c> to preserve the existing test-runner
/// identity / fixture inheritance; subject under test is the flat shape, not the legacy one.
/// </summary>
public class DeployModeConfigLoaderTests : ConfigLoaderValidatorFixtureBase
{
    private readonly string _tempDir;

    public DeployModeConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeployModeConfigLoaderTests_" + Guid.NewGuid().ToString("N")[..8]);
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
    // Phase 40 D-03: hard-reject the legacy section-level shape
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_LegacyDeploySection_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "predicates": [
                  { "name": "X", "path": "/Shop", "areaId": 1 }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("Legacy section-level shape", ex.Message);
        Assert.Contains("'deploy'", ex.Message);
        Assert.Contains("Phase 40", ex.Message);
        Assert.Contains("per-predicate mode", ex.Message);
    }

    [Fact]
    public void Load_LegacySeedSection_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "seed": {
                "predicates": [
                  { "name": "X", "path": "/Shop", "areaId": 1 }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("Legacy section-level shape", ex.Message);
        Assert.Contains("'seed'", ex.Message);
    }

    [Fact]
    public void Load_LegacyDeployValue_AnyShape_Throws()
    {
        // T-40-01-01 detection trap: object? on Deploy/Seed catches any JSON shape — array, primitive, object.
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": []
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));
        Assert.Contains("Legacy section-level shape", ex.Message);
    }

    // -------------------------------------------------------------------------
    // Phase 40 D-01: per-predicate mode is required + must parse to DeploymentMode
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
        Assert.Contains("expected 'Deploy' or 'Seed'", ex.Message);
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

        Assert.Contains("invalid mode 'Garbage'", ex.Message);
        Assert.Contains("BadMode", ex.Message);
        Assert.Contains("expected 'Deploy' or 'Seed'", ex.Message);
    }

    [Fact]
    public void Load_PredicateInjectionMode_Throws()
    {
        // T-40-01-02: free-form mode strings cannot reach SerializerConfiguration. Closed-set parse.
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "Inj", "mode": "Deploy; DROP TABLE X", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));
        Assert.Contains("invalid mode", ex.Message);
    }

    [Fact]
    public void Load_LowercaseDeployMode_AcceptedAsDeploy()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "L", "mode": "deploy", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Predicates);
        Assert.Equal(DeploymentMode.Deploy, config.Predicates[0].Mode);
    }

    [Fact]
    public void Load_UppercaseSeedMode_AcceptedAsSeed()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "U", "mode": "SEED", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Predicates);
        Assert.Equal(DeploymentMode.Seed, config.Predicates[0].Mode);
    }

    // -------------------------------------------------------------------------
    // Phase 40 D-02: new flat-shape success cases with mixed Deploy/Seed predicates
    // -------------------------------------------------------------------------

    [Fact]
    public void Load_NewFlatShape_MixedPredicates_LoadsCorrectModes()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "EcomShops",  "mode": "Deploy", "providerType": "SqlTable", "table": "EcomShops" },
                { "name": "EcomOrderFlow", "mode": "Seed", "providerType": "SqlTable", "table": "EcomOrderFlow", "nameColumn": "OrderFlowName" },
                { "name": "ContentDeploy", "mode": "Deploy", "path": "/Shop", "areaId": 1 }
              ]
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(3, config.Predicates.Count);
        Assert.Equal(2, config.Predicates.Count(p => p.Mode == DeploymentMode.Deploy));
        Assert.Equal(1, config.Predicates.Count(p => p.Mode == DeploymentMode.Seed));
        Assert.Equal("EcomOrderFlow", config.Predicates.Single(p => p.Mode == DeploymentMode.Seed).Name);
    }

    [Fact]
    public void Load_FlatShape_DefaultSubfolders_AreDeployAndSeed()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal("deploy", config.DeployOutputSubfolder);
        Assert.Equal("seed", config.SeedOutputSubfolder);
    }

    [Fact]
    public void Load_FlatShape_CustomSubfolders_RoundTrip()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deployOutputSubfolder": "shipped",
              "seedOutputSubfolder": "fixtures",
              "predicates": []
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal("shipped", config.DeployOutputSubfolder);
        Assert.Equal("fixtures", config.SeedOutputSubfolder);
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
    // Round-trip via ConfigWriter — the writer never emits the legacy shape
    // -------------------------------------------------------------------------

    [Fact]
    public void Write_FlatShape_RoundTrips_WithMixedModes()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "DeployP", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/Shop", AreaId = 1 },
                new() { Name = "SeedP",   Mode = DeploymentMode.Seed,   ProviderType = "Content", Path = "/Customer", AreaId = 1 }
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
        Assert.Equal(DeploymentMode.Deploy, reloaded.Predicates.Single(p => p.Name == "DeployP").Mode);
        Assert.Equal(DeploymentMode.Seed, reloaded.Predicates.Single(p => p.Name == "SeedP").Mode);
        Assert.Single(reloaded.ExcludeFieldsByItemType);
    }
}

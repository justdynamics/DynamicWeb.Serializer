using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

/// <summary>
/// Tests for the Deploy/Seed config structural split (Phase 37-01).
/// Covers D-01..D-06: top-level Deploy + Seed sections, legacy flat → Deploy migration,
/// destination-wins default for Seed, GetMode accessor.
/// </summary>
public class DeployModeConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public DeployModeConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeployModeConfigLoaderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteConfigFile(string json)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_DeploySeedConfig_BothSectionsPopulated()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "predicates": [
                  { "name": "Deploy1", "path": "/Shop", "areaId": 1 },
                  { "name": "Deploy2", "providerType": "SqlTable", "table": "EcomShops" }
                ]
              },
              "seed": {
                "predicates": [
                  { "name": "Seed1", "path": "/CustomerCenter", "areaId": 1 }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(2, config.Deploy.Predicates.Count);
        Assert.Equal("Deploy1", config.Deploy.Predicates[0].Name);
        Assert.Equal("Deploy2", config.Deploy.Predicates[1].Name);
        Assert.Single(config.Seed.Predicates);
        Assert.Equal("Seed1", config.Seed.Predicates[0].Name);
    }

    [Fact]
    public void Load_LegacyFlatConfig_MigratesToDeploy()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "Legacy", "path": "/Shop", "areaId": 1 }
              ],
              "excludeFieldsByItemType": {
                "Swift_PageItemType": ["NavigationTag"]
              },
              "excludeXmlElementsByType": {
                "Dynamicweb.Frontend.ContentPage": ["sort"]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Deploy.Predicates);
        Assert.Equal("Legacy", config.Deploy.Predicates[0].Name);
        Assert.Single(config.Deploy.ExcludeFieldsByItemType);
        Assert.Equal(new List<string> { "NavigationTag" }, config.Deploy.ExcludeFieldsByItemType["Swift_PageItemType"]);
        Assert.Single(config.Deploy.ExcludeXmlElementsByType);
        Assert.Empty(config.Seed.Predicates);
    }

    [Fact]
    public void Load_LegacyAndDeployBothPresent_Throws()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "predicates": [
                { "name": "Legacy", "path": "/Shop", "areaId": 1 }
              ],
              "deploy": {
                "predicates": [
                  { "name": "New", "path": "/Shop", "areaId": 1 }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfigLoader.Load(path));

        Assert.Contains("Both top-level 'Predicates' and 'Deploy.Predicates' are present", ex.Message);
        Assert.Contains("remove the legacy 'Predicates' field", ex.Message);
    }

    [Fact]
    public void Load_OnlyDeploy_SeedDefaultsEmpty()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": {
                "predicates": [
                  { "name": "Only", "path": "/Shop", "areaId": 1 }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Single(config.Deploy.Predicates);
        Assert.NotNull(config.Seed);
        Assert.Empty(config.Seed.Predicates);
        Assert.Equal(ConflictStrategy.DestinationWins, config.Seed.ConflictStrategy);
        Assert.Equal("seed", config.Seed.OutputSubfolder);
    }

    [Fact]
    public void Load_SeedDefault_ConflictStrategyIsDestinationWins()
    {
        var json = """
            {
              "outputDirectory": "/serialization",
              "deploy": { "predicates": [] },
              "seed": {
                "predicates": [
                  { "name": "Seed", "path": "/CustomerCenter", "areaId": 1 }
                ]
              }
            }
            """;
        var path = WriteConfigFile(json);

        var config = ConfigLoader.Load(path);

        Assert.Equal(ConflictStrategy.DestinationWins, config.Seed.ConflictStrategy);
        Assert.Equal(ConflictStrategy.SourceWins, config.Deploy.ConflictStrategy);
    }

    [Fact]
    public void Write_DeploySeedConfig_RoundTrips()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Deploy = new ModeConfig
            {
                OutputSubfolder = "deploy",
                ConflictStrategy = ConflictStrategy.SourceWins,
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "DeployP", ProviderType = "Content", Path = "/Shop", AreaId = 1 }
                },
                ExcludeFieldsByItemType = new Dictionary<string, List<string>>
                {
                    ["Swift_PageItemType"] = new() { "NavigationTag" }
                }
            },
            Seed = new ModeConfig
            {
                OutputSubfolder = "seed",
                ConflictStrategy = ConflictStrategy.DestinationWins,
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "SeedP", ProviderType = "Content", Path = "/Customer", AreaId = 1 }
                }
            }
        };
        var path = Path.Combine(_tempDir, "roundtrip.json");

        ConfigWriter.Save(config, path);
        var reloaded = ConfigLoader.Load(path);

        Assert.Single(reloaded.Deploy.Predicates);
        Assert.Equal("DeployP", reloaded.Deploy.Predicates[0].Name);
        Assert.Single(reloaded.Seed.Predicates);
        Assert.Equal("SeedP", reloaded.Seed.Predicates[0].Name);
        Assert.Equal(ConflictStrategy.DestinationWins, reloaded.Seed.ConflictStrategy);
        Assert.Equal("seed", reloaded.Seed.OutputSubfolder);
        Assert.Equal("deploy", reloaded.Deploy.OutputSubfolder);
        Assert.Single(reloaded.Deploy.ExcludeFieldsByItemType);
    }

    [Fact]
    public void GetMode_ReturnsMatchingModeConfig()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Deploy = new ModeConfig
            {
                OutputSubfolder = "deploy",
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "D", ProviderType = "Content", Path = "/d", AreaId = 1 }
                }
            },
            Seed = new ModeConfig
            {
                OutputSubfolder = "seed",
                ConflictStrategy = ConflictStrategy.DestinationWins,
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "S", ProviderType = "Content", Path = "/s", AreaId = 1 }
                }
            }
        };

        var deploy = config.GetMode(DeploymentMode.Deploy);
        var seed = config.GetMode(DeploymentMode.Seed);

        Assert.Same(config.Deploy, deploy);
        Assert.Same(config.Seed, seed);
        Assert.Equal("D", deploy.Predicates[0].Name);
        Assert.Equal("S", seed.Predicates[0].Name);
    }
}

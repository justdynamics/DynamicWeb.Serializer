using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Queries;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using DynamicWeb.Serializer.Tests.TestHelpers;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 37-01.1 Task 1 — <see cref="SaveXmlTypeCommand"/>, <see cref="ScanXmlTypesCommand"/>,
/// <see cref="XmlTypeByNameQuery"/> and <see cref="XmlTypeListQuery"/> carry a
/// <see cref="DeploymentMode"/> and route reads/writes to the correct
/// <see cref="ModeConfig.ExcludeXmlElementsByType"/> dictionary.
/// </summary>
public class XmlTypePerModeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public XmlTypePerModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "XmlTypePerModeTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreatePerModeConfig(
        Dictionary<string, List<string>>? deployXml = null,
        Dictionary<string, List<string>>? seedXml = null)
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            Deploy = new ModeConfig
            {
                OutputSubfolder = "deploy",
                ConflictStrategy = ConflictStrategy.SourceWins,
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "DeployDefault", ProviderType = "Content", Path = "/", AreaId = 1, PageId = 10 }
                },
                ExcludeXmlElementsByType = deployXml ?? new Dictionary<string, List<string>>()
            },
            Seed = new ModeConfig
            {
                OutputSubfolder = "seed",
                ConflictStrategy = ConflictStrategy.DestinationWins,
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "SeedDefault", ProviderType = "Content", Path = "/", AreaId = 1, PageId = 20 }
                },
                ExcludeXmlElementsByType = seedXml ?? new Dictionary<string, List<string>>()
            }
        };
        ConfigWriter.Save(config, _configPath);
    }

    private SerializerConfiguration LoadConfig() => ConfigLoader.Load(_configPath);

    // -------------------------------------------------------------------------
    // SaveXmlTypeCommand: routes by Mode
    // -------------------------------------------------------------------------

    [Fact]
    public void SaveXmlTypeCommand_WithDeployMode_WritesToDeployDictOnly()
    {
        CreatePerModeConfig(
            deployXml: new Dictionary<string, List<string>> { ["TypeX"] = new List<string>() },
            seedXml: new Dictionary<string, List<string>> { ["TypeX"] = new List<string> { "originalSeedEl" } });

        var cmd = new SaveXmlTypeCommand
        {
            ConfigPath = _configPath,
            Model = new XmlTypeEditModel
            {
                TypeName = "TypeX",
                Mode = DeploymentMode.Deploy,
                ExcludedElements = "dEl1\ndEl2"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "dEl1", "dEl2" }, config.Deploy.ExcludeXmlElementsByType["TypeX"]);
        Assert.Equal(new List<string> { "originalSeedEl" }, config.Seed.ExcludeXmlElementsByType["TypeX"]);
    }

    [Fact]
    public void SaveXmlTypeCommand_WithSeedMode_WritesToSeedDictOnly()
    {
        CreatePerModeConfig(
            deployXml: new Dictionary<string, List<string>> { ["TypeX"] = new List<string> { "originalDeployEl" } },
            seedXml: new Dictionary<string, List<string>> { ["TypeX"] = new List<string>() });

        var cmd = new SaveXmlTypeCommand
        {
            ConfigPath = _configPath,
            Model = new XmlTypeEditModel
            {
                TypeName = "TypeX",
                Mode = DeploymentMode.Seed,
                ExcludedElements = "sEl1"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "sEl1" }, config.Seed.ExcludeXmlElementsByType["TypeX"]);
        Assert.Equal(new List<string> { "originalDeployEl" }, config.Deploy.ExcludeXmlElementsByType["TypeX"]);
    }

    // -------------------------------------------------------------------------
    // ScanXmlTypesCommand: adds discovered types to the mode-scoped dict
    // -------------------------------------------------------------------------

    [Fact]
    public void ScanXmlTypesCommand_WithDeployMode_MergesIntoDeployDictOnly()
    {
        CreatePerModeConfig(
            deployXml: new Dictionary<string, List<string>> { ["Existing"] = new List<string> { "elX" } },
            seedXml: new Dictionary<string, List<string>> { ["SeedOnly"] = new List<string> { "elS" } });

        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProvider",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProvider", "Existing", "NewType"));
        executor.AddMapping("ParagraphModuleSystemName",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSystemName"));

        var cmd = new ScanXmlTypesCommand
        {
            ConfigPath = _configPath,
            Mode = DeploymentMode.Deploy,
            Discovery = new XmlTypeDiscovery(executor)
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        // Deploy gained the new type, preserved the existing one's excludes.
        Assert.Contains("Existing", config.Deploy.ExcludeXmlElementsByType.Keys);
        Assert.Contains("NewType", config.Deploy.ExcludeXmlElementsByType.Keys);
        Assert.Equal(new List<string> { "elX" }, config.Deploy.ExcludeXmlElementsByType["Existing"]);
        Assert.Empty(config.Deploy.ExcludeXmlElementsByType["NewType"]);
        // Seed untouched.
        Assert.DoesNotContain("NewType", config.Seed.ExcludeXmlElementsByType.Keys);
        Assert.Equal(new List<string> { "elS" }, config.Seed.ExcludeXmlElementsByType["SeedOnly"]);
    }

    [Fact]
    public void ScanXmlTypesCommand_WithSeedMode_MergesIntoSeedDictOnly()
    {
        CreatePerModeConfig(
            deployXml: new Dictionary<string, List<string>> { ["DeployOnly"] = new List<string> { "elD" } },
            seedXml: new Dictionary<string, List<string>> { ["Existing"] = new List<string> { "elX" } });

        var executor = new FakeSqlExecutor();
        executor.AddMapping("PageUrlDataProvider",
            TestTableHelper.CreateSingleColumnTable("PageUrlDataProvider", "Existing"));
        executor.AddMapping("ParagraphModuleSystemName",
            TestTableHelper.CreateSingleColumnTable("ParagraphModuleSystemName", "NewType"));

        var cmd = new ScanXmlTypesCommand
        {
            ConfigPath = _configPath,
            Mode = DeploymentMode.Seed,
            Discovery = new XmlTypeDiscovery(executor)
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Contains("Existing", config.Seed.ExcludeXmlElementsByType.Keys);
        Assert.Contains("NewType", config.Seed.ExcludeXmlElementsByType.Keys);
        Assert.Equal(new List<string> { "elX" }, config.Seed.ExcludeXmlElementsByType["Existing"]);
        Assert.Empty(config.Seed.ExcludeXmlElementsByType["NewType"]);
        Assert.DoesNotContain("NewType", config.Deploy.ExcludeXmlElementsByType.Keys);
        Assert.Equal(new List<string> { "elD" }, config.Deploy.ExcludeXmlElementsByType["DeployOnly"]);
    }

    // -------------------------------------------------------------------------
    // XmlTypeByNameQuery: reads from mode-scoped dict
    // -------------------------------------------------------------------------

    [Fact]
    public void XmlTypeByNameQuery_WithDeployMode_ReadsFromDeployDict()
    {
        CreatePerModeConfig(
            deployXml: new Dictionary<string, List<string>> { ["TypeX"] = new List<string> { "deployEl" } },
            seedXml: new Dictionary<string, List<string>> { ["TypeX"] = new List<string> { "seedEl" } });

        var query = new XmlTypeByNameQuery
        {
            ConfigPath = _configPath,
            Mode = DeploymentMode.Deploy,
            TypeName = "TypeX"
        };

        var model = query.GetModel();

        Assert.NotNull(model);
        Assert.Equal("TypeX", model!.TypeName);
        Assert.Equal(DeploymentMode.Deploy, model.Mode);
        var elements = model.ExcludedElements.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("deployEl", elements);
        Assert.DoesNotContain("seedEl", elements);
    }

    [Fact]
    public void XmlTypeByNameQuery_WithSeedMode_ReadsFromSeedDict()
    {
        CreatePerModeConfig(
            deployXml: new Dictionary<string, List<string>> { ["TypeX"] = new List<string> { "deployEl" } },
            seedXml: new Dictionary<string, List<string>> { ["TypeX"] = new List<string> { "seedEl" } });

        var query = new XmlTypeByNameQuery
        {
            ConfigPath = _configPath,
            Mode = DeploymentMode.Seed,
            TypeName = "TypeX"
        };

        var model = query.GetModel();

        Assert.NotNull(model);
        Assert.Equal(DeploymentMode.Seed, model!.Mode);
        var elements = model.ExcludedElements.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("seedEl", elements);
        Assert.DoesNotContain("deployEl", elements);
    }

    // -------------------------------------------------------------------------
    // XmlTypeListQuery: enumerates only the mode's dict keys
    // -------------------------------------------------------------------------

    [Fact]
    public void XmlTypeListQuery_WithDeployMode_ListsOnlyDeployKeys()
    {
        CreatePerModeConfig(
            deployXml: new Dictionary<string, List<string>>
            {
                ["DeployA"] = new List<string> { "a1", "a2" },
                ["DeployB"] = new List<string>()
            },
            seedXml: new Dictionary<string, List<string>>
            {
                ["SeedOnly"] = new List<string> { "sx" }
            });

        var query = new XmlTypeListQuery
        {
            ConfigPath = _configPath,
            Mode = DeploymentMode.Deploy
        };

        var model = query.GetModel();

        Assert.NotNull(model);
        var names = model!.Data.Select(d => d.TypeName).ToList();
        Assert.Contains("DeployA", names);
        Assert.Contains("DeployB", names);
        Assert.DoesNotContain("SeedOnly", names);
        // Excluded-count reflects Deploy's list length.
        var a = model.Data.First(d => d.TypeName == "DeployA");
        Assert.Equal(2, a.ExcludedElementCount);
    }

    [Fact]
    public void XmlTypeListQuery_WithSeedMode_ListsOnlySeedKeys()
    {
        CreatePerModeConfig(
            deployXml: new Dictionary<string, List<string>>
            {
                ["DeployOnly"] = new List<string> { "d1" }
            },
            seedXml: new Dictionary<string, List<string>>
            {
                ["SeedA"] = new List<string> { "s1", "s2", "s3" },
                ["SeedB"] = new List<string>()
            });

        var query = new XmlTypeListQuery
        {
            ConfigPath = _configPath,
            Mode = DeploymentMode.Seed
        };

        var model = query.GetModel();

        Assert.NotNull(model);
        var names = model!.Data.Select(d => d.TypeName).ToList();
        Assert.Contains("SeedA", names);
        Assert.Contains("SeedB", names);
        Assert.DoesNotContain("DeployOnly", names);
        var seedA = model.Data.First(d => d.TypeName == "SeedA");
        Assert.Equal(3, seedA.ExcludedElementCount);
    }

    // -------------------------------------------------------------------------
    // Default mode safety — models/queries/commands default to Deploy to match
    // the predicate path and prevent accidental Seed writes in pre-Phase-37 flows.
    // -------------------------------------------------------------------------

    [Fact]
    public void DefaultMode_OnAllXmlTypeSurfaces_IsDeploy()
    {
        Assert.Equal(DeploymentMode.Deploy, new XmlTypeEditModel().Mode);
        Assert.Equal(DeploymentMode.Deploy, new SaveXmlTypeCommand().Mode);
        Assert.Equal(DeploymentMode.Deploy, new ScanXmlTypesCommand().Mode);
        Assert.Equal(DeploymentMode.Deploy, new XmlTypeByNameQuery().Mode);
        Assert.Equal(DeploymentMode.Deploy, new XmlTypeListQuery().Mode);
    }
}

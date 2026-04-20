using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 37-01.1 Task 1 — <see cref="SaveItemTypeCommand"/> carries a <see cref="DeploymentMode"/>
/// and routes its write to the correct <see cref="ModeConfig.ExcludeFieldsByItemType"/> dictionary.
/// The sibling mode's dictionary must be left untouched (cross-mode isolation).
///
/// Query tests (ItemTypeListQuery / ItemTypeBySystemNameQuery) rely on DW's live ItemManager
/// metadata and cannot be exercised meaningfully without a running DW runtime; their mode routing
/// is covered indirectly by the admin UI's tree → edit-screen → save-command flow and by the
/// save-command tests below.
/// </summary>
public class ItemTypePerModeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ItemTypePerModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ItemTypePerModeTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreatePerModeConfig(
        Dictionary<string, List<string>>? deployFields = null,
        Dictionary<string, List<string>>? seedFields = null)
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
                ExcludeFieldsByItemType = deployFields ?? new Dictionary<string, List<string>>()
            },
            Seed = new ModeConfig
            {
                OutputSubfolder = "seed",
                ConflictStrategy = ConflictStrategy.DestinationWins,
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "SeedDefault", ProviderType = "Content", Path = "/", AreaId = 1, PageId = 20 }
                },
                ExcludeFieldsByItemType = seedFields ?? new Dictionary<string, List<string>>()
            }
        };
        ConfigWriter.Save(config, _configPath);
    }

    private SerializerConfiguration LoadConfig() => ConfigLoader.Load(_configPath);

    [Fact]
    public void SaveItemTypeCommand_WithDeployMode_WritesToDeployDictOnly()
    {
        CreatePerModeConfig(
            deployFields: new Dictionary<string, List<string>> { ["TypeA"] = new List<string>() },
            seedFields: new Dictionary<string, List<string>> { ["TypeA"] = new List<string> { "originalSeed" } });

        var cmd = new SaveItemTypeCommand
        {
            ConfigPath = _configPath,
            Model = new ItemTypeEditModel
            {
                SystemName = "TypeA",
                Mode = DeploymentMode.Deploy,
                ExcludedFields = "deployField1\ndeployField2"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "deployField1", "deployField2" }, config.Deploy.ExcludeFieldsByItemType["TypeA"]);
        Assert.Equal(new List<string> { "originalSeed" }, config.Seed.ExcludeFieldsByItemType["TypeA"]);
    }

    [Fact]
    public void SaveItemTypeCommand_WithSeedMode_WritesToSeedDictOnly()
    {
        CreatePerModeConfig(
            deployFields: new Dictionary<string, List<string>> { ["TypeA"] = new List<string> { "originalDeploy" } },
            seedFields: new Dictionary<string, List<string>> { ["TypeA"] = new List<string>() });

        var cmd = new SaveItemTypeCommand
        {
            ConfigPath = _configPath,
            Model = new ItemTypeEditModel
            {
                SystemName = "TypeA",
                Mode = DeploymentMode.Seed,
                ExcludedFields = "seedField1"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "seedField1" }, config.Seed.ExcludeFieldsByItemType["TypeA"]);
        Assert.Equal(new List<string> { "originalDeploy" }, config.Deploy.ExcludeFieldsByItemType["TypeA"]);
    }

    [Fact]
    public void SaveItemTypeCommand_InDeploy_PreservesSeedWhenSeedHasOtherKeys()
    {
        // Seed has TypeB; Deploy has TypeA. Saving Deploy.TypeA must leave Seed.TypeB fully intact.
        CreatePerModeConfig(
            deployFields: new Dictionary<string, List<string>> { ["TypeA"] = new List<string>() },
            seedFields: new Dictionary<string, List<string>> { ["TypeB"] = new List<string> { "seedOnlyField" } });

        var cmd = new SaveItemTypeCommand
        {
            ConfigPath = _configPath,
            Model = new ItemTypeEditModel
            {
                SystemName = "TypeA",
                Mode = DeploymentMode.Deploy,
                ExcludedFields = "newDeployField"
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        // Deploy was updated.
        Assert.Equal(new List<string> { "newDeployField" }, config.Deploy.ExcludeFieldsByItemType["TypeA"]);
        // Seed untouched — no TypeA leaked in, TypeB preserved as-is.
        Assert.False(config.Seed.ExcludeFieldsByItemType.ContainsKey("TypeA"));
        Assert.Equal(new List<string> { "seedOnlyField" }, config.Seed.ExcludeFieldsByItemType["TypeB"]);
    }

    [Fact]
    public void SaveItemTypeCommand_ModelMode_DefaultsToDeploy_ForBackwardCompatibility()
    {
        // A model constructed without explicitly setting Mode must default to Deploy — matches the
        // predicate path (PredicateEditModel.Mode default). This is a property-level safety net.
        var model = new ItemTypeEditModel { SystemName = "X", ExcludedFields = "" };
        Assert.Equal(DeploymentMode.Deploy, model.Mode);
    }
}

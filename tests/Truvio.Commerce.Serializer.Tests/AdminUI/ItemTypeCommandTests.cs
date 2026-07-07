using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.AdminUI;

public class ItemTypeCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ItemTypeCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ItemTypeCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateMergeConfig(Dictionary<string, List<string>>? excludeFieldsByItemType = null)
    {
        // Phase 40 D-04: ExcludeFieldsByItemType is a top-level dict on SerializerConfiguration.
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", Mode = SerializerMode.Replace, ProviderType = "Content", Path = "/", AreaId = 1 }
            },
            ExcludeFieldsByItemType = excludeFieldsByItemType ?? new()
        };
        ConfigWriter.Save(config, _configPath);
    }

    private SerializerConfiguration LoadConfig() => ConfigLoader.Load(_configPath);

    [Fact]
    public void Save_NullModel_ReturnsInvalid()
    {
        var cmd = new SaveItemTypeCommand { Model = null };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Model data must be given", result.Message);
    }

    [Fact]
    public void Save_EmptySystemName_ReturnsInvalid()
    {
        var cmd = new SaveItemTypeCommand
        {
            Model = new ItemTypeEditModel { SystemName = "", ExcludedFields = new List<string> { "field1" } }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("Item type system name is required", result.Message);
    }

    [Fact]
    public void Save_ValidModel_PersistsExclusions()
    {
        CreateMergeConfig(new Dictionary<string, List<string>>
        {
            ["TestType"] = new List<string>()
        });

        var cmd = new SaveItemTypeCommand
        {
            ConfigPath = _configPath,
            Model = new ItemTypeEditModel
            {
                SystemName = "TestType",
                ExcludedFields = new List<string> { "field1", "field2" }
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "field1", "field2" }, config.ExcludeFieldsByItemType["TestType"]);
    }

    [Fact]
    public void Save_UpdateExisting_ReplacesExclusions()
    {
        CreateMergeConfig(new Dictionary<string, List<string>>
        {
            ["TestType"] = new List<string> { "field1" }
        });

        var cmd = new SaveItemTypeCommand
        {
            ConfigPath = _configPath,
            Model = new ItemTypeEditModel
            {
                SystemName = "TestType",
                ExcludedFields = new List<string> { "field2", "field3" }
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "field2", "field3" }, config.ExcludeFieldsByItemType["TestType"]);
    }

    [Fact]
    public void Save_EmptyExclusions_PersistsEmptyList()
    {
        CreateMergeConfig(new Dictionary<string, List<string>>
        {
            ["TestType"] = new List<string> { "field1" }
        });

        var cmd = new SaveItemTypeCommand
        {
            ConfigPath = _configPath,
            Model = new ItemTypeEditModel
            {
                SystemName = "TestType",
                ExcludedFields = new List<string>()
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Empty(config.ExcludeFieldsByItemType["TestType"]);
    }

    [Fact]
    public void Save_PreservesOtherItemTypes()
    {
        CreateMergeConfig(new Dictionary<string, List<string>>
        {
            ["TypeA"] = new List<string> { "f1" },
            ["TypeB"] = new List<string> { "f2" }
        });

        var cmd = new SaveItemTypeCommand
        {
            ConfigPath = _configPath,
            Model = new ItemTypeEditModel
            {
                SystemName = "TypeA",
                ExcludedFields = new List<string> { "f3" }
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Ok, result.Status);
        var config = LoadConfig();
        Assert.Equal(new List<string> { "f3" }, config.ExcludeFieldsByItemType["TypeA"]);
        Assert.Equal(new List<string> { "f2" }, config.ExcludeFieldsByItemType["TypeB"]);
    }
}

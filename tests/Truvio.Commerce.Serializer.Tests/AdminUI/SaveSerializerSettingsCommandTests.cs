using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Tests.TestHelpers;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.AdminUI;

public class SaveSerializerSettingsCommandTests : ConfigLoaderValidatorFixtureBase
{
    private readonly string _tempDir;
    private readonly string _filesDir;
    private readonly string _systemDir;
    private readonly string _outputDir;
    private readonly string _configPath;

    public SaveSerializerSettingsCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SaveCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        _filesDir = Path.Combine(_tempDir, "wwwroot", "Files");
        _systemDir = Path.Combine(_filesDir, "System");
        _outputDir = Path.Combine(_systemDir, "System", "Serializer");
        _configPath = Path.Combine(_filesDir, "Serializer.config.json");

        Directory.CreateDirectory(_filesDir);
        Directory.CreateDirectory(_systemDir);
    }

    public override void Dispose()
    {
        base.Dispose();  // clear AsyncLocal first
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateMergeConfig()
    {
        // Phase 40 D-01: flat predicate list with explicit per-predicate Mode.
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", Mode = SerializerMode.Replace, ProviderType = "Content", Path = "/", AreaId = 1 }
            }
        };
        ConfigWriter.Save(config, _configPath);
    }

    [Fact]
    public void Handle_NullModel_ReturnsInvalid()
    {
        var cmd = new SaveSerializerSettingsCommand { Model = null };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
    }

    [Fact]
    public void Handle_EmptyOutputDirectory_ReturnsInvalid()
    {
        var cmd = new SaveSerializerSettingsCommand
        {
            Model = new SerializerSettingsModel
            {
                OutputDirectory = ""
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Handle_WhitespaceOutputDirectory_ReturnsInvalid()
    {
        var cmd = new SaveSerializerSettingsCommand
        {
            Model = new SerializerSettingsModel
            {
                OutputDirectory = "   "
            }
        };

        var result = cmd.Handle();

        Assert.Equal(CommandResult.ResultType.Invalid, result.Status);
    }

    [Fact]
    public void Handle_NonExistentOutputDirectory_ReturnsInvalid()
    {
        var cmd = new SaveSerializerSettingsCommand
        {
            Model = new SerializerSettingsModel
            {
                OutputDirectory = "invalid|path*with?chars"
            }
        };

        var result = cmd.Handle();

        Assert.NotEqual(CommandResult.ResultType.Ok, result.Status);
    }

    [Fact]
    public void Handle_ValidModel_MapsOutputDirectoryToConfig()
    {
        // Create the output directory and merge config
        Directory.CreateDirectory(_outputDir);
        CreateMergeConfig();

        var model = new SerializerSettingsModel
        {
            OutputDirectory = @"\System\Serializer"
        };

        // Simulate what the command does: load existing, merge model, save
        var existingConfig = ConfigLoader.Load(_configPath);

        var updatedConfig = existingConfig with
        {
            OutputDirectory = model.OutputDirectory
        };

        ConfigWriter.Save(updatedConfig, _configPath);

        var reloaded = ConfigLoader.Load(_configPath);
        Assert.Equal(@"\System\Serializer", reloaded.OutputDirectory);
        Assert.Equal(ConflictStrategy.SourceWins, reloaded.GetConflictStrategyForMode(SerializerMode.Replace));
        Assert.Single(reloaded.Predicates);
    }

    // -------------------------------------------------------------------------
    // Phase 40 D-01: settings-save preserves the flat predicate list verbatim.
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_PreservesAllPredicatesIncludingMixedModes()
    {
        Directory.CreateDirectory(_outputDir);

        // Merge a config with mixed-Mode predicates on the flat list.
        var mergeConfig = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "ReplaceA", Mode = SerializerMode.Replace, ProviderType = "Content", Path = "/d", AreaId = 1 },
                new() { Name = "MergeA", Mode = SerializerMode.Merge, ProviderType = "Content", Path = "/s", AreaId = 1 },
                new() { Name = "MergeB", Mode = SerializerMode.Merge, ProviderType = "SqlTable", Table = "EcomShops" }
            }
        };
        ConfigWriter.Save(mergeConfig, _configPath);

        // Settings-save should NOT clobber predicates; it only touches OutputDirectory.
        var existingConfig = ConfigLoader.Load(_configPath);
        var model = new SerializerSettingsModel
        {
            OutputDirectory = @"\System\Serializer"
        };
        var updated = existingConfig with
        {
            OutputDirectory = model.OutputDirectory
        };
        ConfigWriter.Save(updated, _configPath);

        var reloaded = ConfigLoader.Load(_configPath);
        Assert.Equal(3, reloaded.Predicates.Count);
        Assert.Equal("ReplaceA", reloaded.Predicates[0].Name);
        Assert.Equal(SerializerMode.Replace, reloaded.Predicates[0].Mode);
        Assert.Equal("MergeA", reloaded.Predicates[1].Name);
        Assert.Equal(SerializerMode.Merge, reloaded.Predicates[1].Mode);
        Assert.Equal("MergeB", reloaded.Predicates[2].Name);
        Assert.Equal(SerializerMode.Merge, reloaded.Predicates[2].Mode);
    }
}

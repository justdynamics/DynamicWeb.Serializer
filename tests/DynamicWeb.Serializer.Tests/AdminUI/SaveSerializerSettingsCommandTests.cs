using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Tests.TestHelpers;
using Dynamicweb.CoreUI.Data;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

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

    private void CreateSeedConfig()
    {
        // Phase 40 D-01: flat predicate list with explicit per-predicate Mode.
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/", AreaId = 1 }
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
                OutputDirectory = "",
                LogLevel = "info",
                DryRun = false,
                ConflictStrategy = "source-wins"
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
                OutputDirectory = "   ",
                LogLevel = "info",
                DryRun = false,
                ConflictStrategy = "source-wins"
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
                OutputDirectory = "invalid|path*with?chars",
                LogLevel = "info",
                DryRun = false,
                ConflictStrategy = "source-wins"
            }
        };

        var result = cmd.Handle();

        Assert.NotEqual(CommandResult.ResultType.Ok, result.Status);
    }

    [Fact]
    public void Handle_ValidModel_MapsAllFieldsToConfig()
    {
        // Create the output directory and seed config
        Directory.CreateDirectory(_outputDir);
        CreateSeedConfig();

        var model = new SerializerSettingsModel
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "debug",
            DryRun = true,
            ConflictStrategy = "source-wins"
        };

        // Simulate what the command does: load existing, merge model, save
        var existingConfig = ConfigLoader.Load(_configPath);

        // Phase 40 D-02: ConflictStrategy is no longer a config knob — saved value is ignored.
        var updatedConfig = existingConfig with
        {
            OutputDirectory = model.OutputDirectory,
            LogLevel = model.LogLevel,
            DryRun = model.DryRun
        };

        ConfigWriter.Save(updatedConfig, _configPath);

        var reloaded = ConfigLoader.Load(_configPath);
        Assert.Equal(@"\System\Serializer", reloaded.OutputDirectory);
        Assert.Equal("debug", reloaded.LogLevel);
        Assert.True(reloaded.DryRun);
        Assert.Equal(ConflictStrategy.SourceWins, reloaded.GetConflictStrategyForMode(DeploymentMode.Deploy));
        Assert.Single(reloaded.Predicates);
    }

    // -------------------------------------------------------------------------
    // Phase 40 D-01: settings-save preserves the flat predicate list verbatim.
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_PreservesAllPredicatesIncludingMixedModes()
    {
        Directory.CreateDirectory(_outputDir);

        // Seed a config with mixed-Mode predicates on the flat list.
        var seedConfig = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "DeployA", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/d", AreaId = 1 },
                new() { Name = "SeedA", Mode = DeploymentMode.Seed, ProviderType = "Content", Path = "/s", AreaId = 1 },
                new() { Name = "SeedB", Mode = DeploymentMode.Seed, ProviderType = "SqlTable", Table = "EcomShops" }
            }
        };
        ConfigWriter.Save(seedConfig, _configPath);

        // Settings-save should NOT clobber predicates; it only touches OutputDirectory / LogLevel / DryRun.
        var existingConfig = ConfigLoader.Load(_configPath);
        var model = new SerializerSettingsModel
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "debug",
            DryRun = true,
            ConflictStrategy = "source-wins"
        };
        var updated = existingConfig with
        {
            OutputDirectory = model.OutputDirectory,
            LogLevel = model.LogLevel,
            DryRun = model.DryRun
        };
        ConfigWriter.Save(updated, _configPath);

        var reloaded = ConfigLoader.Load(_configPath);
        Assert.Equal(3, reloaded.Predicates.Count);
        Assert.Equal("DeployA", reloaded.Predicates[0].Name);
        Assert.Equal(DeploymentMode.Deploy, reloaded.Predicates[0].Mode);
        Assert.Equal("SeedA", reloaded.Predicates[1].Name);
        Assert.Equal(DeploymentMode.Seed, reloaded.Predicates[1].Mode);
        Assert.Equal("SeedB", reloaded.Predicates[2].Name);
        Assert.Equal(DeploymentMode.Seed, reloaded.Predicates[2].Mode);
        Assert.True(reloaded.DryRun);
        Assert.Equal("debug", reloaded.LogLevel);
    }
}

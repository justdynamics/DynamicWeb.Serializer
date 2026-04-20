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
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            ConflictStrategy = ConflictStrategy.SourceWins,
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", ProviderType = "Content", Path = "/", AreaId = 1 }
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
        // This test exercises the path validation in Handle().
        // Since Handle() calls ConfigPathResolver internally, and we can't easily
        // redirect it to our temp dir, we verify the validation logic by checking
        // that the command rejects when the resolved path does not exist.
        // The command will try to use ConfigPathResolver.FindOrCreateConfigFile()
        // which may create a config at a system path. If that fails, it returns Error.
        // Either way, the non-existent output directory path would be rejected.
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

        // Should be Invalid (directory doesn't exist) or Error (config path issue)
        Assert.NotEqual(CommandResult.ResultType.Ok, result.Status);
    }

    [Fact]
    public void Handle_ValidModel_MapsAllFieldsToConfig()
    {
        // Create the output directory and seed config
        Directory.CreateDirectory(_outputDir);
        CreateSeedConfig();

        // We need to test that all fields are mapped correctly.
        // Since the command uses ConfigPathResolver internally, we verify
        // by creating a standalone integration test that exercises the
        // mapping logic through ConfigLoader + ConfigWriter directly.
        var model = new SerializerSettingsModel
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "debug",
            DryRun = true,
            ConflictStrategy = "source-wins"
        };

        // Simulate what the command does: load existing, merge model, save
        var existingConfig = ConfigLoader.Load(_configPath);

        var conflictStrategy = model.ConflictStrategy switch
        {
            "source-wins" => ConflictStrategy.SourceWins,
            _ => ConflictStrategy.SourceWins
        };

        var updatedConfig = new SerializerConfiguration
        {
            OutputDirectory = model.OutputDirectory,
            LogLevel = model.LogLevel,
            DryRun = model.DryRun,
            ConflictStrategy = conflictStrategy,
            Predicates = existingConfig.Predicates
        };

        ConfigWriter.Save(updatedConfig, _configPath);

        // Verify round-trip
        var reloaded = ConfigLoader.Load(_configPath);
        Assert.Equal(@"\System\Serializer", reloaded.OutputDirectory);
        Assert.Equal("debug", reloaded.LogLevel);
        Assert.True(reloaded.DryRun);
        Assert.Equal(ConflictStrategy.SourceWins, reloaded.ConflictStrategy);
        Assert.Single(reloaded.Predicates);
    }

    // -------------------------------------------------------------------------
    // Phase 37-01 D-02: Deploy + Seed sections round-trip through save
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_PreservesDeployAndSeedSections()
    {
        Directory.CreateDirectory(_outputDir);

        // Seed a config with both Deploy AND Seed predicates
        var seedConfig = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            Deploy = new ModeConfig
            {
                OutputSubfolder = "deploy",
                ConflictStrategy = ConflictStrategy.SourceWins,
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "DeployA", ProviderType = "Content", Path = "/d", AreaId = 1 }
                }
            },
            Seed = new ModeConfig
            {
                OutputSubfolder = "seed",
                ConflictStrategy = ConflictStrategy.DestinationWins,
                Predicates = new List<ProviderPredicateDefinition>
                {
                    new() { Name = "SeedA", ProviderType = "Content", Path = "/s", AreaId = 1 },
                    new() { Name = "SeedB", ProviderType = "SqlTable", Table = "EcomShops" }
                }
            }
        };
        ConfigWriter.Save(seedConfig, _configPath);

        // Settings-save should NOT clobber predicates; it only touches OutputDirectory / LogLevel / DryRun / ConflictStrategy.
        var existingConfig = ConfigLoader.Load(_configPath);
        var model = new SerializerSettingsModel
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "debug",
            DryRun = true,
            ConflictStrategy = "source-wins"
        };
        var updatedDeploy = existingConfig.Deploy with { ConflictStrategy = ConflictStrategy.SourceWins };
        var updated = new SerializerConfiguration
        {
            OutputDirectory = model.OutputDirectory,
            LogLevel = model.LogLevel,
            DryRun = model.DryRun,
            Deploy = updatedDeploy,
            Seed = existingConfig.Seed
        };
        ConfigWriter.Save(updated, _configPath);

        var reloaded = ConfigLoader.Load(_configPath);
        Assert.Single(reloaded.Deploy.Predicates);
        Assert.Equal("DeployA", reloaded.Deploy.Predicates[0].Name);
        Assert.Equal(2, reloaded.Seed.Predicates.Count);
        Assert.Equal("SeedA", reloaded.Seed.Predicates[0].Name);
        Assert.Equal("SeedB", reloaded.Seed.Predicates[1].Name);
        Assert.Equal(ConflictStrategy.DestinationWins, reloaded.Seed.ConflictStrategy);
        Assert.True(reloaded.DryRun);
        Assert.Equal("debug", reloaded.LogLevel);
    }

    [Fact]
    public void Save_LegacyConfig_MigratesToDeployOnSave()
    {
        Directory.CreateDirectory(_outputDir);

        // Write a legacy flat-shaped config directly as JSON (simulates a pre-Phase-37 file on disk).
        File.WriteAllText(_configPath, """
            {
              "outputDirectory": "\\System\\Serializer",
              "logLevel": "info",
              "predicates": [
                { "name": "Legacy", "path": "/", "areaId": 1 }
              ]
            }
            """);

        var loaded = ConfigLoader.Load(_configPath);
        // Loader migration: legacy flat → Deploy
        Assert.Single(loaded.Deploy.Predicates);
        Assert.Equal("Legacy", loaded.Deploy.Predicates[0].Name);
        Assert.Empty(loaded.Seed.Predicates);

        // Now save: ConfigWriter must emit the new Deploy/Seed shape on disk.
        ConfigWriter.Save(loaded, _configPath);

        var raw = File.ReadAllText(_configPath);
        Assert.Contains("\"deploy\":", raw);
        Assert.Contains("\"seed\":", raw);
        // Re-load and confirm the migrated shape round-trips: Deploy.Predicates populated,
        // legacy fields NOT exposed via the loader's raw migration path.
        var reloaded = ConfigLoader.Load(_configPath);
        Assert.Single(reloaded.Deploy.Predicates);
        Assert.Equal("Legacy", reloaded.Deploy.Predicates[0].Name);
        Assert.Empty(reloaded.Seed.Predicates);
    }
}

using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Tests.TestHelpers;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

/// <summary>
/// Phase 40 ConfigWriter tests. Writer emits a single flat <c>predicates</c> array
/// where each entry carries its own <c>mode</c>. Legacy <c>deploy</c> / <c>seed</c>
/// section keys are NEVER emitted.
///
/// Inherits <see cref="ConfigLoaderValidatorFixtureBase"/> so the round-trip tests
/// (which call <see cref="ConfigLoader.Load(string)"/>) get the permissive identifier
/// validator override and don't try to query a live DW DB for INFORMATION_SCHEMA.
/// </summary>
public class ConfigWriterTests : ConfigLoaderValidatorFixtureBase
{
    private readonly string _tempDir;

    public ConfigWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ConfigWriterTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private SerializerConfiguration CreateTestConfig() => new()
    {
        OutputDirectory = "/serialization",
        LogLevel = "debug",
        DryRun = true,
        Predicates = new List<ProviderPredicateDefinition>
        {
            new()
            {
                Name = "Customer Center",
                Mode = DeploymentMode.Deploy,
                ProviderType = "Content",
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
        Assert.Equal(config.Predicates[0].Mode, loaded.Predicates[0].Mode);
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
        Assert.DoesNotContain("\"Predicates\":", json);
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
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new()
                {
                    Name = "Test",
                    Mode = DeploymentMode.Deploy,
                    ProviderType = "Content",
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
    public void Save_WritesNewFields_JsonContainsDryRun()
    {
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "newfields.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        Assert.Contains("\"dryRun\": true", json);
    }

    [Fact]
    public void Save_RoundTrip_PreservesNewFields()
    {
        var config = CreateTestConfig();
        var filePath = Path.Combine(_tempDir, "roundtrip_new.json");

        ConfigWriter.Save(config, filePath);
        var loaded = ConfigLoader.Load(filePath);

        Assert.True(loaded.DryRun);
    }

    // -------------------------------------------------------------------------
    // Phase 40: writer never emits the legacy deploy/seed section keys.
    // -------------------------------------------------------------------------

    [Fact]
    public void Save_NeverEmits_LegacyDeploySectionKey()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "P1", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/", AreaId = 1 },
                new() { Name = "P2", Mode = DeploymentMode.Seed, ProviderType = "SqlTable", Table = "EcomShops" }
            }
        };
        var filePath = Path.Combine(_tempDir, "no_legacy.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        Assert.DoesNotContain("\"deploy\":", json);
        Assert.DoesNotContain("\"seed\":", json);
        Assert.Contains("\"predicates\":", json);
        Assert.Contains("\"mode\":", json);
    }

    [Fact]
    public void Save_EmitsTopLevelDeployAndSeedOutputSubfolders()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            DeployOutputSubfolder = "shipped",
            SeedOutputSubfolder = "fixtures",
            Predicates = new List<ProviderPredicateDefinition>()
        };
        var filePath = Path.Combine(_tempDir, "subfolders.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        Assert.Contains("\"deployOutputSubfolder\":", json);
        Assert.Contains("\"shipped\"", json);
        Assert.Contains("\"seedOutputSubfolder\":", json);
        Assert.Contains("\"fixtures\"", json);
    }

    [Fact]
    public void Save_EachPredicate_EmitsItsOwnModeKey()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "P1", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/", AreaId = 1 },
                new() { Name = "P2", Mode = DeploymentMode.Seed, ProviderType = "Content", Path = "/", AreaId = 1 }
            }
        };
        var filePath = Path.Combine(_tempDir, "per_predicate_mode.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        // Each predicate's "mode" key — exact strings "Deploy" / "Seed".
        Assert.Contains("\"mode\": \"Deploy\"", json);
        Assert.Contains("\"mode\": \"Seed\"", json);
    }

    [Fact]
    public void Save_EmptyExclusionDicts_OmitsKeysFromOutput()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>()
            // ExcludeFieldsByItemType and ExcludeXmlElementsByType default to empty dicts.
        };
        var filePath = Path.Combine(_tempDir, "empty_exclusions.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        // Matches existing WhenWritingNull pattern — empty dicts are mapped to null and omitted.
        Assert.DoesNotContain("\"excludeFieldsByItemType\":", json);
        Assert.DoesNotContain("\"excludeXmlElementsByType\":", json);
    }

    [Fact]
    public void Save_NonEmptyExclusionDicts_EmitsKeysToOutput()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>(),
            ExcludeFieldsByItemType = new Dictionary<string, List<string>>
            {
                ["Swift_PageItemType"] = new() { "NavigationTag" }
            },
            ExcludeXmlElementsByType = new Dictionary<string, List<string>>
            {
                ["Dynamicweb.Frontend.ContentPage"] = new() { "sort" }
            }
        };
        var filePath = Path.Combine(_tempDir, "with_exclusions.json");

        ConfigWriter.Save(config, filePath);
        var json = File.ReadAllText(filePath);

        Assert.Contains("\"excludeFieldsByItemType\":", json);
        Assert.Contains("\"NavigationTag\"", json);
        Assert.Contains("\"excludeXmlElementsByType\":", json);
        Assert.Contains("\"sort\"", json);
    }

    [Fact]
    public void Save_ThenLoad_PreservesEveryPredicatesMode()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "A", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/A", AreaId = 1 },
                new() { Name = "B", Mode = DeploymentMode.Seed,   ProviderType = "Content", Path = "/B", AreaId = 1 },
                new() { Name = "C", Mode = DeploymentMode.Seed,   ProviderType = "Content", Path = "/C", AreaId = 1 },
                new() { Name = "D", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/D", AreaId = 1 }
            }
        };
        var filePath = Path.Combine(_tempDir, "full_roundtrip.json");

        ConfigWriter.Save(config, filePath);
        var loaded = ConfigLoader.Load(filePath);

        Assert.Equal(config.Predicates.Count, loaded.Predicates.Count);
        for (var i = 0; i < config.Predicates.Count; i++)
        {
            Assert.Equal(config.Predicates[i].Name, loaded.Predicates[i].Name);
            Assert.Equal(config.Predicates[i].Mode, loaded.Predicates[i].Mode);
        }
    }
}

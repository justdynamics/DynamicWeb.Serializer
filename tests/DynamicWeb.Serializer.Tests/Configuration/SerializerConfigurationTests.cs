using System.Text.Json;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

/// <summary>
/// Tests for the Phase 40 flat <see cref="SerializerConfiguration"/> shape and the
/// per-predicate <see cref="ProviderPredicateDefinition.Mode"/> field. Replaces the
/// section-level Deploy/Seed split (D-01..D-04). No backcompat per project policy.
/// </summary>
public class SerializerConfigurationTests : IDisposable
{
    private readonly string _tempDir;

    public SerializerConfigurationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SerializerConfigurationTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // -------------------------------------------------------------------------
    // ProviderPredicateDefinition.Mode default + JSON round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public void Predicate_DefaultMode_IsDeploy()
    {
        var p = new ProviderPredicateDefinition
        {
            Name = "X",
            ProviderType = "Content",
            AreaId = 1,
            Path = "/"
        };

        Assert.Equal(DeploymentMode.Deploy, p.Mode);
    }

    [Fact]
    public void Predicate_SeedMode_RoundTripsThroughSystemTextJson()
    {
        var p = new ProviderPredicateDefinition
        {
            Name = "X",
            ProviderType = "Content",
            AreaId = 1,
            Path = "/",
            Mode = DeploymentMode.Seed
        };

        var json = JsonSerializer.Serialize(p, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Contains("\"mode\":\"Seed\"", json);

        var roundTripped = JsonSerializer.Deserialize<ProviderPredicateDefinition>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(roundTripped);
        Assert.Equal(DeploymentMode.Seed, roundTripped!.Mode);
    }

    [Fact]
    public void Predicate_DeployMode_SerializesAsDeployString()
    {
        var p = new ProviderPredicateDefinition
        {
            Name = "X",
            ProviderType = "Content",
            AreaId = 1,
            Path = "/",
            Mode = DeploymentMode.Deploy
        };

        var json = JsonSerializer.Serialize(p, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Contains("\"mode\":\"Deploy\"", json);
    }

    // -------------------------------------------------------------------------
    // SerializerConfiguration defaults
    // -------------------------------------------------------------------------

    [Fact]
    public void Config_Default_PredicatesIsEmpty()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.NotNull(config.Predicates);
        Assert.Empty(config.Predicates);
    }

    [Fact]
    public void Config_Default_DeployOutputSubfolderIsDeploy()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.Equal("deploy", config.DeployOutputSubfolder);
    }

    [Fact]
    public void Config_Default_SeedOutputSubfolderIsSeed()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.Equal("seed", config.SeedOutputSubfolder);
    }

    [Fact]
    public void Config_Default_ExclusionDictsAreEmpty()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.NotNull(config.ExcludeFieldsByItemType);
        Assert.Empty(config.ExcludeFieldsByItemType);
        Assert.NotNull(config.ExcludeXmlElementsByType);
        Assert.Empty(config.ExcludeXmlElementsByType);
    }

    // -------------------------------------------------------------------------
    // GetSubfolderForMode + GetConflictStrategyForMode
    // -------------------------------------------------------------------------

    [Fact]
    public void GetSubfolderForMode_Seed_ReturnsSeedOutputSubfolder()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            SeedOutputSubfolder = "my-seed"
        };

        Assert.Equal("my-seed", config.GetSubfolderForMode(DeploymentMode.Seed));
    }

    [Fact]
    public void GetSubfolderForMode_Deploy_ReturnsDeployOutputSubfolder()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            DeployOutputSubfolder = "my-deploy"
        };

        Assert.Equal("my-deploy", config.GetSubfolderForMode(DeploymentMode.Deploy));
    }

    [Fact]
    public void GetConflictStrategyForMode_Seed_ReturnsDestinationWins()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.Equal(ConflictStrategy.DestinationWins, config.GetConflictStrategyForMode(DeploymentMode.Seed));
    }

    [Fact]
    public void GetConflictStrategyForMode_Deploy_ReturnsSourceWins()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.Equal(ConflictStrategy.SourceWins, config.GetConflictStrategyForMode(DeploymentMode.Deploy));
    }

    // -------------------------------------------------------------------------
    // EnsureDirectories creates per-mode subfolders
    // -------------------------------------------------------------------------

    [Fact]
    public void EnsureDirectories_CreatesDeployAndSeedSubfolders()
    {
        var config = new SerializerConfiguration { OutputDirectory = "Out_" + Guid.NewGuid().ToString("N")[..8] };

        var resolved = config.EnsureDirectories(_tempDir);

        Assert.True(Directory.Exists(Path.Combine(resolved.SerializeRoot, "deploy")));
        Assert.True(Directory.Exists(Path.Combine(resolved.SerializeRoot, "seed")));
    }

    [Fact]
    public void EnsureDirectories_CustomSubfolders_CreatesNamedFolders()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "Out_" + Guid.NewGuid().ToString("N")[..8],
            DeployOutputSubfolder = "shipped",
            SeedOutputSubfolder = "fixtures"
        };

        var resolved = config.EnsureDirectories(_tempDir);

        Assert.True(Directory.Exists(Path.Combine(resolved.SerializeRoot, "shipped")));
        Assert.True(Directory.Exists(Path.Combine(resolved.SerializeRoot, "fixtures")));
    }

    // -------------------------------------------------------------------------
    // Removed legacy surface — reflection-based negative assertions (T-40-01-03 tripwire)
    // -------------------------------------------------------------------------

    [Fact]
    public void Config_HasNo_DeployProperty()
    {
        var t = typeof(SerializerConfiguration);
        Assert.Null(t.GetProperty("Deploy"));
    }

    [Fact]
    public void Config_HasNo_SeedProperty()
    {
        var t = typeof(SerializerConfiguration);
        Assert.Null(t.GetProperty("Seed"));
    }

    [Fact]
    public void Config_HasNo_GetModeMethod()
    {
        var t = typeof(SerializerConfiguration);
        Assert.Null(t.GetMethod("GetMode"));
    }

    [Fact]
    public void Config_HasNo_GetModeSerializeRootMethod()
    {
        var t = typeof(SerializerConfiguration);
        Assert.Null(t.GetMethod("GetModeSerializeRoot"));
    }

    [Fact]
    public void Config_HasNo_ConflictStrategyProperty()
    {
        // Phase 40 D-02: ConflictStrategy is hardcoded per mode and exposed only via
        // GetConflictStrategyForMode. The legacy top-level [JsonIgnore] alias is removed.
        var t = typeof(SerializerConfiguration);
        Assert.Null(t.GetProperty("ConflictStrategy"));
    }
}

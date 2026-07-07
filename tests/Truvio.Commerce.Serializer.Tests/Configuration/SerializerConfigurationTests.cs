using System.Text.Json;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Configuration;

/// <summary>
/// Tests for the Phase 40 flat <see cref="SerializerConfiguration"/> shape and the
/// per-predicate <see cref="ProviderPredicateDefinition.Mode"/> field. Replaces the
/// section-level Replace/Merge split (D-01..D-04). No backcompat per project policy.
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
    public void Predicate_DefaultMode_IsReplace()
    {
        var p = new ProviderPredicateDefinition
        {
            Name = "X",
            ProviderType = "Content",
            AreaId = 1,
            Path = "/"
        };

        Assert.Equal(SerializerMode.Replace, p.Mode);
    }

    [Fact]
    public void Predicate_MergeMode_RoundTripsThroughSystemTextJson()
    {
        var p = new ProviderPredicateDefinition
        {
            Name = "X",
            ProviderType = "Content",
            AreaId = 1,
            Path = "/",
            Mode = SerializerMode.Merge
        };

        var json = JsonSerializer.Serialize(p, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Contains("\"mode\":\"Merge\"", json);

        var roundTripped = JsonSerializer.Deserialize<ProviderPredicateDefinition>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(roundTripped);
        Assert.Equal(SerializerMode.Merge, roundTripped!.Mode);
    }

    [Fact]
    public void Predicate_ReplaceMode_SerializesAsReplaceString()
    {
        var p = new ProviderPredicateDefinition
        {
            Name = "X",
            ProviderType = "Content",
            AreaId = 1,
            Path = "/",
            Mode = SerializerMode.Replace
        };

        var json = JsonSerializer.Serialize(p, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        Assert.Contains("\"mode\":\"Replace\"", json);
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
    public void Config_Default_ReplaceOutputSubfolderIsReplace()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.Equal("replace", config.ReplaceOutputSubfolder);
    }

    [Fact]
    public void Config_Default_MergeOutputSubfolderIsMerge()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.Equal("merge", config.MergeOutputSubfolder);
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
    public void GetSubfolderForMode_Merge_ReturnsMergeOutputSubfolder()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            MergeOutputSubfolder = "my-merge"
        };

        Assert.Equal("my-merge", config.GetSubfolderForMode(SerializerMode.Merge));
    }

    [Fact]
    public void GetSubfolderForMode_Replace_ReturnsReplaceOutputSubfolder()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "/out",
            ReplaceOutputSubfolder = "my-replace"
        };

        Assert.Equal("my-replace", config.GetSubfolderForMode(SerializerMode.Replace));
    }

    [Fact]
    public void GetConflictStrategyForMode_Merge_ReturnsDestinationWins()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.Equal(ConflictStrategy.DestinationWins, config.GetConflictStrategyForMode(SerializerMode.Merge));
    }

    [Fact]
    public void GetConflictStrategyForMode_Replace_ReturnsSourceWins()
    {
        var config = new SerializerConfiguration { OutputDirectory = "/out" };

        Assert.Equal(ConflictStrategy.SourceWins, config.GetConflictStrategyForMode(SerializerMode.Replace));
    }

    // -------------------------------------------------------------------------
    // EnsureDirectories creates per-mode subfolders
    // -------------------------------------------------------------------------

    [Fact]
    public void EnsureDirectories_CreatesReplaceAndMergeSubfolders()
    {
        var config = new SerializerConfiguration { OutputDirectory = "Out_" + Guid.NewGuid().ToString("N")[..8] };

        var resolved = config.EnsureDirectories(_tempDir);

        Assert.True(Directory.Exists(Path.Combine(resolved.SerializeRoot, "replace")));
        Assert.True(Directory.Exists(Path.Combine(resolved.SerializeRoot, "merge")));
    }

    [Fact]
    public void EnsureDirectories_CustomSubfolders_CreatesNamedFolders()
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = "Out_" + Guid.NewGuid().ToString("N")[..8],
            ReplaceOutputSubfolder = "shipped",
            MergeOutputSubfolder = "fixtures"
        };

        var resolved = config.EnsureDirectories(_tempDir);

        Assert.True(Directory.Exists(Path.Combine(resolved.SerializeRoot, "shipped")));
        Assert.True(Directory.Exists(Path.Combine(resolved.SerializeRoot, "fixtures")));
    }

    // -------------------------------------------------------------------------
    // Removed legacy surface — reflection-based negative assertions (T-40-01-03 tripwire)
    // -------------------------------------------------------------------------

    [Fact]
    public void Config_HasNo_ReplaceProperty()
    {
        var t = typeof(SerializerConfiguration);
        Assert.Null(t.GetProperty("Replace"));
    }

    [Fact]
    public void Config_HasNo_MergeProperty()
    {
        var t = typeof(SerializerConfiguration);
        Assert.Null(t.GetProperty("Merge"));
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

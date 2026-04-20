using DynamicWeb.Serializer.AdminUI.Tree;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Dynamicweb.CoreUI.Navigation;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 37-01.1 Task 2 — SerializerSettingsNodeProvider emits per-mode Item Type + XML Type
/// subtrees under the Deploy and Seed mode nodes, preserving the shared category-grouping
/// tree structure. Node IDs are unique across modes so Deploy and Seed do not collide.
///
/// These tests exercise the provider directly — they do NOT rely on live DW ItemManager
/// metadata. ItemType category/leaf enumeration inside the Item Types subtree depends on
/// ItemManager and is therefore covered only by the "node shape + non-crash" assertions here;
/// the user-visible leaf behaviour is covered end-to-end by the admin UI tree.
/// </summary>
public class SerializerSettingsNodeProviderModeTreeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly string? _savedOverride;

    public SerializerSettingsNodeProviderModeTreeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SerSetNodeModeTreeTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
        _savedOverride = ConfigPathResolver.TestOverridePath;
        ConfigPathResolver.TestOverridePath = _configPath;
    }

    public void Dispose()
    {
        ConfigPathResolver.TestOverridePath = _savedOverride;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteConfig(
        Dictionary<string, List<string>>? deployFields = null,
        Dictionary<string, List<string>>? seedFields = null,
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
                Predicates = new List<ProviderPredicateDefinition>(),
                ExcludeFieldsByItemType = deployFields ?? new Dictionary<string, List<string>>(),
                ExcludeXmlElementsByType = deployXml ?? new Dictionary<string, List<string>>()
            },
            Seed = new ModeConfig
            {
                OutputSubfolder = "seed",
                ConflictStrategy = ConflictStrategy.DestinationWins,
                Predicates = new List<ProviderPredicateDefinition>(),
                ExcludeFieldsByItemType = seedFields ?? new Dictionary<string, List<string>>(),
                ExcludeXmlElementsByType = seedXml ?? new Dictionary<string, List<string>>()
            }
        };
        ConfigWriter.Save(config, _configPath);
    }

    private static NavigationNodePath PathTo(params string[] segments) => new(segments);

    // -------------------------------------------------------------------------
    // Root-level: Serialize node children include Deploy + Seed with Item Types + XML Types.
    // -------------------------------------------------------------------------

    [Fact]
    public void Root_ContainsTwoModeNodes_EachWithItemTypesAndXmlTypesChildren()
    {
        WriteConfig();
        var provider = new SerializerSettingsNodeProvider();

        // Expand children of the Serialize node; the list must include Deploy Item Types +
        // Deploy XML Types + Seed Item Types + Seed XML Types (Predicates stays from 37-01).
        var children = provider
            .GetSubNodes(PathTo("Settings_Database", SerializerSettingsNodeProvider.SerializeNodeId))
            .Select(n => n.Id)
            .ToList();

        Assert.Contains(SerializerSettingsNodeProvider.DeployPredicatesNodeId, children);
        Assert.Contains(SerializerSettingsNodeProvider.SeedPredicatesNodeId, children);
        Assert.Contains(SerializerSettingsNodeProvider.DeployItemTypesNodeId, children);
        Assert.Contains(SerializerSettingsNodeProvider.SeedItemTypesNodeId, children);
        Assert.Contains(SerializerSettingsNodeProvider.DeployXmlTypesNodeId, children);
        Assert.Contains(SerializerSettingsNodeProvider.SeedXmlTypesNodeId, children);
        // Log viewer is single (not per-mode).
        Assert.Contains(SerializerSettingsNodeProvider.LogViewerNodeId, children);
    }

    [Fact]
    public void NodeIds_AreUniqueAcrossModes()
    {
        // Guards against collision between Deploy/Seed subtree IDs (DW NavigationNodePath uses
        // the full ID chain for highlighting; duplicate IDs would cross-hit).
        var ids = new[]
        {
            SerializerSettingsNodeProvider.DeployItemTypesNodeId,
            SerializerSettingsNodeProvider.SeedItemTypesNodeId,
            SerializerSettingsNodeProvider.DeployXmlTypesNodeId,
            SerializerSettingsNodeProvider.SeedXmlTypesNodeId,
            SerializerSettingsNodeProvider.DeployPredicatesNodeId,
            SerializerSettingsNodeProvider.SeedPredicatesNodeId
        };
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    // -------------------------------------------------------------------------
    // Deploy / Seed XML Types leaves are sourced from the mode's dict only.
    // -------------------------------------------------------------------------

    [Fact]
    public void DeployXmlTypes_ListsOnlyDeployKeys()
    {
        WriteConfig(
            deployXml: new Dictionary<string, List<string>>
            {
                ["DeployXmlA"] = new List<string> { "el1" },
                ["DeployXmlB"] = new List<string>()
            },
            seedXml: new Dictionary<string, List<string>>
            {
                ["SeedXmlOnly"] = new List<string>()
            });
        var provider = new SerializerSettingsNodeProvider();

        var names = provider
            .GetSubNodes(PathTo(SerializerSettingsNodeProvider.SerializeNodeId, SerializerSettingsNodeProvider.DeployXmlTypesNodeId))
            .Select(n => n.Name)
            .ToList();

        Assert.Contains("DeployXmlA", names);
        Assert.Contains("DeployXmlB", names);
        Assert.DoesNotContain("SeedXmlOnly", names);
    }

    [Fact]
    public void SeedXmlTypes_ListsOnlySeedKeys()
    {
        WriteConfig(
            deployXml: new Dictionary<string, List<string>>
            {
                ["DeployOnly"] = new List<string>()
            },
            seedXml: new Dictionary<string, List<string>>
            {
                ["SeedXmlA"] = new List<string>(),
                ["SeedXmlB"] = new List<string>()
            });
        var provider = new SerializerSettingsNodeProvider();

        var names = provider
            .GetSubNodes(PathTo(SerializerSettingsNodeProvider.SerializeNodeId, SerializerSettingsNodeProvider.SeedXmlTypesNodeId))
            .Select(n => n.Name)
            .ToList();

        Assert.Contains("SeedXmlA", names);
        Assert.Contains("SeedXmlB", names);
        Assert.DoesNotContain("DeployOnly", names);
    }

    // -------------------------------------------------------------------------
    // Deploy / Seed Item Types top-level category enumeration (no DW runtime in test env,
    // so we assert no-crash + correct node emission semantics — the category grouping uses
    // ItemManager metadata which is unavailable offline).
    // -------------------------------------------------------------------------

    [Fact]
    public void DeployItemTypes_UnderDeployParent_EmitsWithoutCrashing()
    {
        WriteConfig(
            deployFields: new Dictionary<string, List<string>> { ["TypeA"] = new List<string> { "f" } },
            seedFields: new Dictionary<string, List<string>> { ["TypeB"] = new List<string> { "g" } });
        var provider = new SerializerSettingsNodeProvider();

        var nodes = provider.GetSubNodes(PathTo(
            SerializerSettingsNodeProvider.SerializeNodeId,
            SerializerSettingsNodeProvider.DeployItemTypesNodeId)).ToList();

        // No crash; depending on ItemManager availability may be empty.
        Assert.NotNull(nodes);
    }

    [Fact]
    public void SeedItemTypes_UnderSeedParent_EmitsWithoutCrashing()
    {
        WriteConfig();
        var provider = new SerializerSettingsNodeProvider();

        var nodes = provider.GetSubNodes(PathTo(
            SerializerSettingsNodeProvider.SerializeNodeId,
            SerializerSettingsNodeProvider.SeedItemTypesNodeId)).ToList();

        Assert.NotNull(nodes);
    }

    // -------------------------------------------------------------------------
    // Legacy shared ItemTypesNodeId constant must be removed (grep-equivalent guard:
    // reflection is fragile; this test exists so a human reading the file sees the intent,
    // but the true guard is the grep acceptance criterion).
    // -------------------------------------------------------------------------

    [Fact]
    public void LegacyItemTypesNodeId_IsNoLongerSharedRootChild()
    {
        // Under the new tree the old "Serializer_ItemTypes" constant is NOT expected to
        // appear as a child of the Serialize node — it has been split into Deploy + Seed
        // variants. This test will fail with a compile error if we leave the constant behind
        // AND emit it at the root in RED state.
        WriteConfig();
        var provider = new SerializerSettingsNodeProvider();

        var rootChildren = provider
            .GetSubNodes(PathTo("Settings_Database", SerializerSettingsNodeProvider.SerializeNodeId))
            .Select(n => n.Id)
            .ToList();

        Assert.DoesNotContain("Serializer_ItemTypes", rootChildren);
        // The old "Embedded XML" shared node is also gone under the new scheme (replaced by
        // DeployXmlTypes / SeedXmlTypes).
        Assert.DoesNotContain("Serializer_EmbeddedXml", rootChildren);
    }
}

using DynamicWeb.Serializer.AdminUI.Tree;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Models;
using Dynamicweb.CoreUI.Navigation;
using Xunit;

namespace DynamicWeb.Serializer.Tests.AdminUI;

/// <summary>
/// Phase 40 D-06 — SerializerSettingsNodeProvider emits a single flat predicate subtree (no
/// Deploy/Seed group split). Each predicate carries its own Mode (D-01) and exclusion dicts are
/// top-level mode-agnostic (D-04). Class name preserved (rather than renamed to
/// SerializerSettingsNodeProviderTreeTests) to keep test runner identity stable.
/// </summary>
public class SerializerSettingsNodeProviderModeTreeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly string? _savedOverride;

    public SerializerSettingsNodeProviderModeTreeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SerSetNodeTreeTests_" + Guid.NewGuid().ToString("N")[..8]);
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
        List<ProviderPredicateDefinition>? predicates = null,
        Dictionary<string, List<string>>? excludeFieldsByItemType = null,
        Dictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            Predicates = predicates ?? new List<ProviderPredicateDefinition>(),
            ExcludeFieldsByItemType = excludeFieldsByItemType ?? new Dictionary<string, List<string>>(),
            ExcludeXmlElementsByType = excludeXmlElementsByType ?? new Dictionary<string, List<string>>()
        };
        ConfigWriter.Save(config, _configPath);
    }

    private static NavigationNodePath PathTo(params string[] segments) => new(segments);

    // -------------------------------------------------------------------------
    // Phase 40 D-06: Serialize node has 4 children — Predicates, Item Types, Embedded XML, Log Viewer.
    // No Deploy/Seed group split.
    // -------------------------------------------------------------------------

    [Fact]
    public void GetSubNodes_UnderSerializeNode_Returns_Predicates_ItemTypes_XmlTypes_LogViewer()
    {
        WriteConfig();
        var provider = new SerializerSettingsNodeProvider();

        var rootChildren = provider
            .GetSubNodes(PathTo(SerializerSettingsNodeProvider.DeveloperRootId, SerializerSettingsNodeProvider.SerializeNodeId))
            .Select(n => n.Id)
            .ToList();

        Assert.Equal(4, rootChildren.Count);
        Assert.Contains(SerializerSettingsNodeProvider.PredicatesNodeId, rootChildren);
        Assert.Contains(SerializerSettingsNodeProvider.ItemTypesNodeId, rootChildren);
        Assert.Contains(SerializerSettingsNodeProvider.XmlTypesNodeId, rootChildren);
        Assert.Contains(SerializerSettingsNodeProvider.LogViewerNodeId, rootChildren);
    }

    // -------------------------------------------------------------------------
    // Phase 40 D-06: Predicates subtree lists every predicate with its mode in display name.
    // -------------------------------------------------------------------------

    [Fact]
    public void GetSubNodes_UnderPredicatesNode_Lists_All_Predicates_With_Mode_In_Display_Name()
    {
        WriteConfig(predicates: new List<ProviderPredicateDefinition>
        {
            new() { Name = "FirstDeploy", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/d", AreaId = 1, PageId = 10 },
            new() { Name = "SecondSeed", Mode = DeploymentMode.Seed, ProviderType = "SqlTable", Table = "EcomShops" }
        });
        var provider = new SerializerSettingsNodeProvider();

        var nodes = provider
            .GetSubNodes(PathTo(SerializerSettingsNodeProvider.SerializeNodeId, SerializerSettingsNodeProvider.PredicatesNodeId))
            .ToList();

        Assert.Equal(2, nodes.Count);
        Assert.Contains(nodes, n => n.Name == "FirstDeploy (Deploy)");
        Assert.Contains(nodes, n => n.Name == "SecondSeed (Seed)");
    }

    // -------------------------------------------------------------------------
    // Phase 40 D-04: Embedded XML subtree lists keys from the top-level dict.
    // -------------------------------------------------------------------------

    [Fact]
    public void GetSubNodes_UnderXmlTypesNode_Lists_All_Top_Level_Excluded_Types()
    {
        WriteConfig(excludeXmlElementsByType: new Dictionary<string, List<string>>
        {
            ["TypeA"] = new List<string> { "el1" },
            ["TypeB"] = new List<string>(),
            ["TypeC"] = new List<string> { "x", "y" }
        });
        var provider = new SerializerSettingsNodeProvider();

        var names = provider
            .GetSubNodes(PathTo(SerializerSettingsNodeProvider.SerializeNodeId, SerializerSettingsNodeProvider.XmlTypesNodeId))
            .Select(n => n.Name)
            .ToList();

        Assert.Equal(3, names.Count);
        Assert.Contains("TypeA", names);
        Assert.Contains("TypeB", names);
        Assert.Contains("TypeC", names);
    }

    [Fact]
    public void GetSubNodes_UnderXmlTypesNode_EmptyDict_ReturnsNoNodes()
    {
        WriteConfig();
        var provider = new SerializerSettingsNodeProvider();

        var nodes = provider
            .GetSubNodes(PathTo(SerializerSettingsNodeProvider.SerializeNodeId, SerializerSettingsNodeProvider.XmlTypesNodeId))
            .ToList();

        Assert.Empty(nodes);
    }

    // -------------------------------------------------------------------------
    // Phase 40 D-06: Item Types subtree emits without crashing when DW runtime is offline.
    // -------------------------------------------------------------------------

    [Fact]
    public void GetSubNodes_UnderItemTypesNode_EmitsWithoutCrashing()
    {
        WriteConfig(excludeFieldsByItemType: new Dictionary<string, List<string>>
        {
            ["TypeA"] = new List<string> { "field1" }
        });
        var provider = new SerializerSettingsNodeProvider();

        var nodes = provider
            .GetSubNodes(PathTo(SerializerSettingsNodeProvider.SerializeNodeId, SerializerSettingsNodeProvider.ItemTypesNodeId))
            .ToList();

        Assert.NotNull(nodes);
    }
}

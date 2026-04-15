using DynamicWeb.Serializer.Configuration;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

public class ExclusionMergerTests
{
    // -------------------------------------------------------------------------
    // MergeFieldExclusions
    // -------------------------------------------------------------------------

    [Fact]
    public void MergeFieldExclusions_EmptyFlatAndEmptyDict_ReturnsNull()
    {
        var flat = new List<string>();
        var dict = new Dictionary<string, List<string>>();

        var result = ExclusionMerger.MergeFieldExclusions(flat, dict, "Swift_PageItemType");

        Assert.Null(result);
    }

    [Fact]
    public void MergeFieldExclusions_FlatListOnly_ReturnsHashSetWithFlatItems()
    {
        var flat = new List<string> { "NavigationTag", "AreaDomain" };
        var dict = new Dictionary<string, List<string>>();

        var result = ExclusionMerger.MergeFieldExclusions(flat, dict, "Swift_PageItemType");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Contains("NavigationTag", result);
        Assert.Contains("AreaDomain", result);
    }

    [Fact]
    public void MergeFieldExclusions_DictEntryOnly_ReturnsHashSetWithTypedItems()
    {
        var flat = new List<string>();
        var dict = new Dictionary<string, List<string>>
        {
            ["Swift_PageItemType"] = new List<string> { "Hidden", "TreeSection" }
        };

        var result = ExclusionMerger.MergeFieldExclusions(flat, dict, "Swift_PageItemType");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Contains("Hidden", result);
        Assert.Contains("TreeSection", result);
    }

    [Fact]
    public void MergeFieldExclusions_BothFlatAndTyped_ReturnsUnion()
    {
        var flat = new List<string> { "NavigationTag", "AreaDomain" };
        var dict = new Dictionary<string, List<string>>
        {
            ["Swift_PageItemType"] = new List<string> { "Hidden", "TreeSection" }
        };

        var result = ExclusionMerger.MergeFieldExclusions(flat, dict, "Swift_PageItemType");

        Assert.NotNull(result);
        Assert.Equal(4, result!.Count);
        Assert.Contains("NavigationTag", result);
        Assert.Contains("AreaDomain", result);
        Assert.Contains("Hidden", result);
        Assert.Contains("TreeSection", result);
    }

    [Fact]
    public void MergeFieldExclusions_DifferentItemType_ReturnsFlatOnly()
    {
        var flat = new List<string> { "NavigationTag", "AreaDomain" };
        var dict = new Dictionary<string, List<string>>
        {
            ["Swift_PageItemType"] = new List<string> { "Hidden", "TreeSection" }
        };

        var result = ExclusionMerger.MergeFieldExclusions(flat, dict, "Other_ItemType");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Contains("NavigationTag", result);
        Assert.Contains("AreaDomain", result);
    }

    [Fact]
    public void MergeFieldExclusions_CaseInsensitiveDictKeyLookup()
    {
        var flat = new List<string>();
        var dict = new Dictionary<string, List<string>>
        {
            ["Swift_PageItemType"] = new List<string> { "Hidden", "TreeSection" }
        };

        // Lookup with lowercase key
        var result = ExclusionMerger.MergeFieldExclusions(flat, dict, "swift_pageitemtype");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Contains("Hidden", result);
        Assert.Contains("TreeSection", result);
    }

    // -------------------------------------------------------------------------
    // MergeXmlExclusions
    // -------------------------------------------------------------------------

    [Fact]
    public void MergeXmlExclusions_EmptyFlatAndEmptyDict_ReturnsNull()
    {
        var flat = new List<string>();
        var dict = new Dictionary<string, List<string>>();

        var result = ExclusionMerger.MergeXmlExclusions(flat, dict, "SomeModule");

        Assert.Null(result);
    }

    [Fact]
    public void MergeXmlExclusions_BothFlatAndTyped_ReturnsUnionList()
    {
        var flat = new List<string> { "Settings", "Cache" };
        var dict = new Dictionary<string, List<string>>
        {
            ["ProductListModule"] = new List<string> { "Sorting", "Paging" }
        };

        var result = ExclusionMerger.MergeXmlExclusions(flat, dict, "ProductListModule");

        Assert.NotNull(result);
        Assert.Equal(4, result!.Count);
        Assert.Contains("Settings", result);
        Assert.Contains("Cache", result);
        Assert.Contains("Sorting", result);
        Assert.Contains("Paging", result);
    }

    [Fact]
    public void MergeXmlExclusions_DifferentXmlType_ReturnsFlatOnly()
    {
        var flat = new List<string> { "Settings", "Cache" };
        var dict = new Dictionary<string, List<string>>
        {
            ["ProductListModule"] = new List<string> { "Sorting", "Paging" }
        };

        var result = ExclusionMerger.MergeXmlExclusions(flat, dict, "OtherModule");

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Contains("Settings", result);
        Assert.Contains("Cache", result);
    }
}

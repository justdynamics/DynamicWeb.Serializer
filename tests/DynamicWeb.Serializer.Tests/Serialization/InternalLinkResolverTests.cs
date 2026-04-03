using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Serialization;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Serialization;

public class InternalLinkResolverTests
{
    private readonly Dictionary<int, int> _map = new()
    {
        { 123, 456 },
        { 1, 901 },
        { 12, 902 },
        { 200, 300 },
        { 50, 60 }
    };

    private InternalLinkResolver CreateResolver(Action<string>? log = null)
        => new(_map, log);

    // -------------------------------------------------------------------------
    // Test 1: Simple rewrite
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_SimpleRewrite_ReplacesSourceWithTarget()
    {
        var resolver = CreateResolver();
        var result = resolver.ResolveLinks("Default.aspx?ID=123");
        Assert.Equal("Default.aspx?ID=456", result);
    }

    // -------------------------------------------------------------------------
    // Test 2: Multiple links in one string
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_MultipleLinks_RewritesBoth()
    {
        var resolver = CreateResolver();
        var input = "Link1: Default.aspx?ID=123 and Link2: Default.aspx?ID=200";
        var result = resolver.ResolveLinks(input);
        Assert.Equal("Link1: Default.aspx?ID=456 and Link2: Default.aspx?ID=300", result);
    }

    // -------------------------------------------------------------------------
    // Test 3: Rich text HTML
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_RichTextHtml_RewritesIdInsideHref()
    {
        var resolver = CreateResolver();
        var input = "<a href=\"Default.aspx?ID=123\">Click here</a>";
        var result = resolver.ResolveLinks(input);
        Assert.Equal("<a href=\"Default.aspx?ID=456\">Click here</a>", result);
    }

    // -------------------------------------------------------------------------
    // Test 4: Boundary safety - ID=1 does not corrupt ID=12
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_BoundarySafety_DoesNotCorruptSimilarIds()
    {
        var resolver = CreateResolver();
        var input = "Default.aspx?ID=1 and Default.aspx?ID=12";
        var result = resolver.ResolveLinks(input);
        Assert.Equal("Default.aspx?ID=901 and Default.aspx?ID=902", result);
    }

    // -------------------------------------------------------------------------
    // Test 5: Unresolvable link - source ID not in map
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_UnresolvableLink_PreservesOriginalAndLogsWarning()
    {
        var warnings = new List<string>();
        var resolver = CreateResolver(msg => warnings.Add(msg));
        var input = "Default.aspx?ID=999";
        var result = resolver.ResolveLinks(input);

        Assert.Equal("Default.aspx?ID=999", result);
        Assert.Single(warnings);
        Assert.Contains("999", warnings[0]);
    }

    // -------------------------------------------------------------------------
    // Test 6: No links - plain text unchanged
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_NoLinks_ReturnsUnchanged()
    {
        var resolver = CreateResolver();
        var result = resolver.ResolveLinks("Hello world");
        Assert.Equal("Hello world", result);
    }

    // -------------------------------------------------------------------------
    // Test 7: Case insensitivity
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_CaseInsensitive_RewritesLowercaseVariant()
    {
        var resolver = CreateResolver();
        var result = resolver.ResolveLinks("default.aspx?id=123");
        Assert.Equal("default.aspx?id=456", result);
    }

    // -------------------------------------------------------------------------
    // Test 8: Query parameter preservation
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_QueryParamPreservation_PreservesOtherParams()
    {
        var resolver = CreateResolver();
        var result = resolver.ResolveLinks("Default.aspx?ID=123&GroupID=G1");
        Assert.Equal("Default.aspx?ID=456&GroupID=G1", result);
    }

    // -------------------------------------------------------------------------
    // Test 9: Empty/null input
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_NullInput_ReturnsNull()
    {
        var resolver = CreateResolver();
        Assert.Null(resolver.ResolveLinks(null));
    }

    [Fact]
    public void ResolveLinks_EmptyInput_ReturnsEmpty()
    {
        var resolver = CreateResolver();
        Assert.Equal("", resolver.ResolveLinks(""));
    }

    // -------------------------------------------------------------------------
    // Test 10: Paragraph anchor preservation
    // -------------------------------------------------------------------------

    [Fact]
    public void ResolveLinks_AnchorFragment_PreservesFragment()
    {
        var resolver = CreateResolver();
        var result = resolver.ResolveLinks("Default.aspx?ID=123#456");
        Assert.Equal("Default.aspx?ID=456#456", result);
    }

    // -------------------------------------------------------------------------
    // Test 11: BuildSourceToTargetMap from flat page list
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildSourceToTargetMap_FlatList_BuildsCorrectDictionary()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var pages = new List<SerializedPage>
        {
            new()
            {
                PageUniqueId = guid1, SourcePageId = 100,
                Name = "Page1", MenuText = "", UrlName = "p1", SortOrder = 1
            },
            new()
            {
                PageUniqueId = guid2, SourcePageId = 200,
                Name = "Page2", MenuText = "", UrlName = "p2", SortOrder = 2
            }
        };
        var cache = new Dictionary<Guid, int> { { guid1, 501 }, { guid2, 502 } };

        var map = InternalLinkResolver.BuildSourceToTargetMap(pages, cache);

        Assert.Equal(2, map.Count);
        Assert.Equal(501, map[100]);
        Assert.Equal(502, map[200]);
    }

    // -------------------------------------------------------------------------
    // Test 12: Skips pages where SourcePageId is null
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildSourceToTargetMap_NullSourcePageId_SkipsPage()
    {
        var guid1 = Guid.NewGuid();
        var pages = new List<SerializedPage>
        {
            new()
            {
                PageUniqueId = guid1, SourcePageId = null,
                Name = "OldPage", MenuText = "", UrlName = "old", SortOrder = 1
            }
        };
        var cache = new Dictionary<Guid, int> { { guid1, 501 } };

        var map = InternalLinkResolver.BuildSourceToTargetMap(pages, cache);

        Assert.Empty(map);
    }

    // -------------------------------------------------------------------------
    // Test 13: Skips pages where GUID not in cache
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildSourceToTargetMap_GuidNotInCache_SkipsPage()
    {
        var pages = new List<SerializedPage>
        {
            new()
            {
                PageUniqueId = Guid.NewGuid(), SourcePageId = 100,
                Name = "Orphan", MenuText = "", UrlName = "orph", SortOrder = 1
            }
        };
        var cache = new Dictionary<Guid, int>(); // empty cache

        var map = InternalLinkResolver.BuildSourceToTargetMap(pages, cache);

        Assert.Empty(map);
    }

    // -------------------------------------------------------------------------
    // Test 14: Handles nested children (recursive tree flattening)
    // -------------------------------------------------------------------------

    [Fact]
    public void BuildSourceToTargetMap_NestedChildren_FlattensRecursively()
    {
        var guidParent = Guid.NewGuid();
        var guidChild = Guid.NewGuid();
        var pages = new List<SerializedPage>
        {
            new()
            {
                PageUniqueId = guidParent, SourcePageId = 10,
                Name = "Parent", MenuText = "", UrlName = "parent", SortOrder = 1,
                Children = new List<SerializedPage>
                {
                    new()
                    {
                        PageUniqueId = guidChild, SourcePageId = 20,
                        Name = "Child", MenuText = "", UrlName = "child", SortOrder = 1
                    }
                }
            }
        };
        var cache = new Dictionary<Guid, int>
        {
            { guidParent, 801 },
            { guidChild, 802 }
        };

        var map = InternalLinkResolver.BuildSourceToTargetMap(pages, cache);

        Assert.Equal(2, map.Count);
        Assert.Equal(801, map[10]);
        Assert.Equal(802, map[20]);
    }

    // -------------------------------------------------------------------------
    // Test 15: GetStats returns resolved/unresolved counts
    // -------------------------------------------------------------------------

    [Fact]
    public void GetStats_AfterMultipleCalls_ReturnsCorrectCounts()
    {
        var warnings = new List<string>();
        var resolver = CreateResolver(msg => warnings.Add(msg));

        resolver.ResolveLinks("Default.aspx?ID=123"); // resolved
        resolver.ResolveLinks("Default.aspx?ID=200"); // resolved
        resolver.ResolveLinks("Default.aspx?ID=999"); // unresolved

        var (resolved, unresolved) = resolver.GetStats();
        Assert.Equal(2, resolved);
        Assert.Equal(1, unresolved);
    }
}

using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class TemplateReferenceScannerTests
{
    private static SerializedPage MakePage(string name, string? layout = null, string? itemType = null,
        List<SerializedGridRow>? gridRows = null, List<SerializedPage>? children = null)
    {
        return new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            SourcePageId = 100,
            Name = name,
            MenuText = name,
            UrlName = name,
            SortOrder = 1,
            Layout = layout,
            ItemType = itemType,
            GridRows = gridRows ?? new List<SerializedGridRow>(),
            Children = children ?? new List<SerializedPage>()
        };
    }

    [Fact]
    public void Scan_EmptyTree_ReturnsEmpty()
    {
        var scanner = new TemplateReferenceScanner();
        var refs = scanner.Scan(new List<SerializedPage>());
        Assert.Empty(refs);
    }

    [Fact]
    public void Scan_PageWithLayout_EmitsPageLayoutRef()
    {
        var scanner = new TemplateReferenceScanner();
        var pages = new List<SerializedPage>
        {
            MakePage("Home", layout: "Swift-v2/Swift-v2_Page.cshtml")
        };

        var refs = scanner.Scan(pages);

        Assert.Single(refs);
        var r = refs[0];
        Assert.Equal("page-layout", r.Kind);
        Assert.Equal("Swift-v2/Swift-v2_Page.cshtml", r.Path);
        Assert.Single(r.ReferencedBy);
    }

    [Fact]
    public void Scan_PageWithItemType_EmitsItemTypeRef()
    {
        var scanner = new TemplateReferenceScanner();
        var pages = new List<SerializedPage>
        {
            MakePage("Blog", itemType: "BlogPost")
        };

        var refs = scanner.Scan(pages);

        Assert.Contains(refs, r => r.Kind == "item-type" && r.Path == "BlogPost");
    }

    [Fact]
    public void Scan_GridRowWithDefinitionId_EmitsGridRowRef()
    {
        var scanner = new TemplateReferenceScanner();
        var pages = new List<SerializedPage>
        {
            MakePage("Page1", gridRows: new List<SerializedGridRow>
            {
                new() { Id = Guid.NewGuid(), SortOrder = 1, DefinitionId = "2ColsEqual" }
            })
        };

        var refs = scanner.Scan(pages);

        Assert.Contains(refs, r => r.Kind == "grid-row" && r.Path == "2ColsEqual");
    }

    [Fact]
    public void Scan_MultiplePagesSameLayout_CoalescesToOneRefWithCombinedReferencedBy()
    {
        var scanner = new TemplateReferenceScanner();
        var layout = "Shared/Page.cshtml";
        var pages = new List<SerializedPage>
        {
            MakePage("One", layout: layout),
            MakePage("Two", layout: layout),
            MakePage("Three", layout: layout)
        };

        var refs = scanner.Scan(pages);

        var layoutRefs = refs.Where(r => r.Kind == "page-layout" && r.Path == layout).ToList();
        Assert.Single(layoutRefs);
        Assert.Equal(3, layoutRefs[0].ReferencedBy.Count);
    }

    [Fact]
    public void Scan_NestedChildren_Traversed()
    {
        var scanner = new TemplateReferenceScanner();
        var child = MakePage("Child", layout: "child.cshtml");
        var parent = MakePage("Parent", layout: "parent.cshtml",
            children: new List<SerializedPage> { child });

        var refs = scanner.Scan(new List<SerializedPage> { parent });

        Assert.Contains(refs, r => r.Path == "parent.cshtml");
        Assert.Contains(refs, r => r.Path == "child.cshtml");
    }

    [Fact]
    public void Scan_NullOrEmptyLayoutAndItemType_NotEmitted()
    {
        var scanner = new TemplateReferenceScanner();
        var pages = new List<SerializedPage>
        {
            MakePage("Bare", layout: null, itemType: null)
        };

        var refs = scanner.Scan(pages);
        Assert.Empty(refs);
    }
}

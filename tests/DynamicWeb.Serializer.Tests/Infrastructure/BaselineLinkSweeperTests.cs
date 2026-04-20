using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class BaselineLinkSweeperTests
{
    private static SerializedPage MakePage(
        int? sourceId = 100,
        string? shortCut = null,
        string? productPage = null,
        Dictionary<string, object>? fields = null,
        Dictionary<string, object>? propertyFields = null,
        List<SerializedGridRow>? gridRows = null,
        List<SerializedPage>? children = null,
        string menuText = "P")
    {
        return new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            SourcePageId = sourceId,
            Name = menuText,
            MenuText = menuText,
            UrlName = menuText,
            SortOrder = 1,
            ShortCut = shortCut,
            Fields = fields ?? new(),
            PropertyFields = propertyFields ?? new(),
            NavigationSettings = productPage != null
                ? new SerializedNavigationSettings { UseEcomGroups = true, ProductPage = productPage }
                : null,
            GridRows = gridRows ?? new(),
            Children = children ?? new()
        };
    }

    [Fact]
    public void Sweep_EmptyTree_ReturnsZeroResolvedZeroUnresolved()
    {
        var sweeper = new BaselineLinkSweeper();
        var result = sweeper.Sweep(new List<SerializedPage>());

        Assert.Empty(result.Unresolved);
        Assert.Equal(0, result.ResolvedCount);
    }

    [Fact]
    public void Sweep_AllLinksResolvable_ReturnsZeroUnresolved()
    {
        var page1 = MakePage(sourceId: 100, shortCut: "Default.aspx?ID=200");
        var page2 = MakePage(sourceId: 200);

        var sweeper = new BaselineLinkSweeper();
        var result = sweeper.Sweep(new List<SerializedPage> { page1, page2 });

        Assert.Empty(result.Unresolved);
        Assert.Equal(1, result.ResolvedCount);
    }

    [Fact]
    public void Sweep_OneUnresolvableLink_Reported()
    {
        var page1 = MakePage(sourceId: 100, shortCut: "Default.aspx?ID=99999");

        var sweeper = new BaselineLinkSweeper();
        var result = sweeper.Sweep(new List<SerializedPage> { page1 });

        Assert.Single(result.Unresolved);
        Assert.Equal(99999, result.Unresolved[0].UnresolvablePageId);
        Assert.Contains("ShortCut", result.Unresolved[0].FieldName);
    }

    [Fact]
    public void Sweep_LinkInShortCut_Checked()
    {
        var page = MakePage(sourceId: 1, shortCut: "Default.aspx?ID=777");
        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { page });

        Assert.Single(result.Unresolved);
        Assert.Equal("ShortCut", result.Unresolved[0].FieldName);
    }

    [Fact]
    public void Sweep_LinkInProductPage_Checked()
    {
        var page = MakePage(sourceId: 1, productPage: "Default.aspx?ID=777");
        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { page });

        Assert.Single(result.Unresolved);
        Assert.Contains("ProductPage", result.Unresolved[0].FieldName);
    }

    [Fact]
    public void Sweep_LinkInItemField_Checked()
    {
        var page = MakePage(sourceId: 1,
            fields: new Dictionary<string, object> { ["LinkedPage"] = "Default.aspx?ID=888" });
        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { page });

        Assert.Single(result.Unresolved);
        Assert.Contains("LinkedPage", result.Unresolved[0].FieldName);
    }

    [Fact]
    public void Sweep_LinkInPropertyField_Checked()
    {
        var page = MakePage(sourceId: 1,
            propertyFields: new Dictionary<string, object> { ["ButtonLink"] = "Default.aspx?ID=321" });
        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { page });

        Assert.Single(result.Unresolved);
        Assert.Contains("ButtonLink", result.Unresolved[0].FieldName);
    }

    [Fact]
    public void Sweep_LinkInParagraphField_Checked()
    {
        var row = new SerializedGridRow
        {
            Id = Guid.NewGuid(),
            SortOrder = 1,
            Columns = new()
            {
                new SerializedGridColumn
                {
                    Id = 1, Width = 12,
                    Paragraphs = new()
                    {
                        new SerializedParagraph
                        {
                            ParagraphUniqueId = Guid.NewGuid(),
                            SortOrder = 1,
                            Fields = new() { ["Target"] = "Default.aspx?ID=9999" }
                        }
                    }
                }
            }
        };
        var page = MakePage(sourceId: 1, gridRows: new() { row });

        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { page });

        Assert.Single(result.Unresolved);
        Assert.Contains("Target", result.Unresolved[0].FieldName);
        Assert.Contains("paragraph", result.Unresolved[0].SourcePageIdentifier, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sweep_MultipleUnresolved_AllReported()
    {
        var page = MakePage(sourceId: 1,
            fields: new Dictionary<string, object>
            {
                ["A"] = "Default.aspx?ID=1001",
                ["B"] = "Default.aspx?ID=1002",
                ["C"] = "Default.aspx?ID=1003"
            });
        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { page });

        Assert.Equal(3, result.Unresolved.Count);
        Assert.Contains(result.Unresolved, u => u.UnresolvablePageId == 1001);
        Assert.Contains(result.Unresolved, u => u.UnresolvablePageId == 1002);
        Assert.Contains(result.Unresolved, u => u.UnresolvablePageId == 1003);
    }

    [Fact]
    public void Sweep_NestedChildren_Traversed()
    {
        var child = MakePage(sourceId: 2, shortCut: "Default.aspx?ID=99999", menuText: "Child");
        var parent = MakePage(sourceId: 1, children: new() { child }, menuText: "Parent");

        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { parent });

        Assert.Single(result.Unresolved);
        Assert.Contains("Child", result.Unresolved[0].SourcePageIdentifier);
    }

    [Fact]
    public void Sweep_SelectedValueJson_Checked()
    {
        var page = MakePage(sourceId: 1,
            fields: new Dictionary<string, object>
            {
                ["Button"] = "{\"SelectedValue\": \"8888\"}"
            });
        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { page });

        Assert.Single(result.Unresolved);
        Assert.Equal(8888, result.Unresolved[0].UnresolvablePageId);
    }

    [Fact]
    public void Sweep_AnchorFragment_StripsFragment_AndResolvesPage()
    {
        var page1 = MakePage(sourceId: 100, shortCut: "Default.aspx?ID=200#42");
        var page2 = MakePage(sourceId: 200);

        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { page1, page2 });

        Assert.Empty(result.Unresolved);
        Assert.Equal(1, result.ResolvedCount);
    }

    [Fact]
    public void Sweep_SourceIdNull_PageNotIncludedInValidIds()
    {
        // A page with no SourcePageId cannot be a valid link target (the ID map is built from
        // SourcePageId values). Link referencing 300 should therefore remain unresolvable.
        var referencingPage = MakePage(sourceId: 100, shortCut: "Default.aspx?ID=300");
        var orphanPage = MakePage(sourceId: null, menuText: "NoSource");

        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { referencingPage, orphanPage });

        Assert.Single(result.Unresolved);
        Assert.Equal(300, result.Unresolved[0].UnresolvablePageId);
    }
}

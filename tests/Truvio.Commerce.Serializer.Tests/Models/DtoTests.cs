using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Tests.Fixtures;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Models;

public class DtoTests
{
    [Fact]
    public void SerializedPage_CanBeConstructed_WithRequiredFields()
    {
        var page = new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "Test",
            MenuText = "Test",
            UrlName = "test",
            SortOrder = 1
        };
        Assert.NotNull(page);
        Assert.Equal("Test", page.Name);
    }

    [Fact]
    public void SerializedPage_Fields_DefaultsToEmptyDictionary()
    {
        var page = new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "Test", MenuText = "Test",
            UrlName = "test", SortOrder = 1
        };
        Assert.NotNull(page.Fields);
        Assert.Empty(page.Fields);
    }

    [Fact]
    public void SerializedArea_CanHoldChildPages()
    {
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Website",
            SortOrder = 1,
            Pages = new List<SerializedPage>
            {
                ContentTreeBuilder.BuildSinglePage("Page 1"),
                ContentTreeBuilder.BuildSinglePage("Page 2")
            }
        };
        Assert.Equal(2, area.Pages.Count);
    }

    [Fact]
    public void SerializedParagraph_CanBeConstructed_WithRequiredFields()
    {
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = Guid.NewGuid(),
            SortOrder = 1
        };
        Assert.NotNull(para);
        Assert.NotNull(para.Fields);
        Assert.Empty(para.Fields);
    }

    [Fact]
    public void SerializedPage_Permissions_DefaultsToEmptyList()
    {
        var page = new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "Test", MenuText = "Test",
            UrlName = "test", SortOrder = 1
        };
        Assert.NotNull(page.Permissions);
        Assert.Empty(page.Permissions);
    }

    [Fact]
    public void SerializedGridRow_Permissions_DefaultsToEmptyList()
    {
        var row = new SerializedGridRow
        {
            Id = Guid.NewGuid(),
            SortOrder = 1
        };
        Assert.NotNull(row.Permissions);
        Assert.Empty(row.Permissions);
    }

    [Fact]
    public void SerializedParagraph_Permissions_DefaultsToEmptyList()
    {
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = Guid.NewGuid(),
            SortOrder = 1
        };
        Assert.NotNull(para.Permissions);
        Assert.Empty(para.Permissions);
    }

    [Fact]
    public void SerializedPermission_SubName_DefaultsToNull()
    {
        var perm = new SerializedPermission
        {
            Owner = "Anonymous",
            OwnerType = "role",
            Level = "none",
            LevelValue = 1
        };
        Assert.Null(perm.SubName);
    }

    [Fact]
    public void SerializedPermission_SubName_CanBeSet()
    {
        var perm = new SerializedPermission
        {
            Owner = "Anonymous",
            OwnerType = "role",
            SubName = "Paragraph",
            Level = "none",
            LevelValue = 1
        };
        Assert.Equal("Paragraph", perm.SubName);
    }

    [Fact]
    public void SerializedPermission_CanBeConstructed()
    {
        var perm = new SerializedPermission
        {
            Owner = "Anonymous",
            OwnerType = "role",
            Level = "none",
            LevelValue = 1
        };
        Assert.Equal("Anonymous", perm.Owner);
        Assert.Equal("role", perm.OwnerType);
        Assert.Null(perm.OwnerId);
        Assert.Equal("none", perm.Level);
        Assert.Equal(1, perm.LevelValue);
    }

    [Fact]
    public void SerializedPage_SourcePageId_DefaultsToNull()
    {
        var page = new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "Test", MenuText = "Test",
            UrlName = "test", SortOrder = 1
        };
        Assert.Null(page.SourcePageId);
    }

    [Fact]
    public void SerializedPage_SourcePageId_CanBeSet()
    {
        var page = new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "Test", MenuText = "Test",
            UrlName = "test", SortOrder = 1,
            SourcePageId = 42
        };
        Assert.Equal(42, page.SourcePageId);
    }

    [Fact]
    public void SerializedParagraph_SourceParagraphId_DefaultsToNull()
    {
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = Guid.NewGuid(),
            SortOrder = 1
        };
        Assert.Null(para.SourceParagraphId);
    }

    [Fact]
    public void SerializedParagraph_SourceParagraphId_CanBeSet()
    {
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = Guid.NewGuid(),
            SortOrder = 1,
            SourceParagraphId = 99
        };
        Assert.Equal(99, para.SourceParagraphId);
    }

    [Fact]
    public void SerializedPage_BooleanDefaults_MatchDynamicWebDefaults()
    {
        var page = new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "Test", MenuText = "Test",
            UrlName = "test", SortOrder = 1
        };

        // DW defaults these to true
        Assert.True(page.Allowclick);
        Assert.True(page.Allowsearch);
        Assert.True(page.ShowInSitemap);
        Assert.True(page.ShowInLegend);

        // DW defaults these to false
        Assert.False(page.Hidden);
    }

    [Fact]
    public void SerializedPage_ActiveFromActiveTo_DefaultToNull()
    {
        var page = new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "Test", MenuText = "Test",
            UrlName = "test", SortOrder = 1
        };
        Assert.Null(page.ActiveFrom);
        Assert.Null(page.ActiveTo);
    }

    [Fact]
    public void SerializedPage_SubObjects_DefaultToNull()
    {
        var page = new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "Test", MenuText = "Test",
            UrlName = "test", SortOrder = 1
        };
        Assert.Null(page.Seo);
        Assert.Null(page.UrlSettings);
        Assert.Null(page.Visibility);
        Assert.Null(page.NavigationSettings);
    }

    [Fact]
    public void ContentHierarchy_FullDepth_CanBeConstructed()
    {
        var tree = ContentTreeBuilder.BuildSampleTree();
        Assert.NotNull(tree);
        Assert.NotEmpty(tree.Pages);
        Assert.NotEmpty(tree.Pages[0].GridRows);
        Assert.NotEmpty(tree.Pages[0].GridRows[0].Columns);
        Assert.NotEmpty(tree.Pages[0].GridRows[0].Columns[0].Paragraphs);
    }
}

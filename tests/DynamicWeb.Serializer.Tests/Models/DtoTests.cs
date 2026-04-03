using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Tests.Fixtures;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Models;

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

using Dynamicweb.ContentSync.Models;
using Dynamicweb.ContentSync.Tests.Fixtures;
using Xunit;

namespace Dynamicweb.ContentSync.Tests.Models;

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

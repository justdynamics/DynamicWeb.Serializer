using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Serialization;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Serialization;

/// <summary>
/// Regression tests for the per-entry tree pruning key comparison. Full-pipeline manifests
/// key files mode-root-relative ("_content/&lt;Area&gt;/.../page.yml"); zip-import entries key
/// them zip-root-relative ("&lt;Area&gt;/.../page.yml"). FileSystemStore tags pages with the
/// "_content/"-prefixed form unconditionally — comparing raw keys pruned EVERY page on zip
/// import, so an uploaded package silently wrote nothing (found live on the cloud env:
/// download Posts zip, edit a title, re-upload → no-op).
/// </summary>
public class PruneToEntryFilesTests
{
    private static SerializedPage MakePage(string sourceFile, params SerializedPage[] children)
    {
        return new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            Name = "P",
            MenuText = "P",
            UrlName = "p",
            SortOrder = 1,
            SourceFile = sourceFile,
            Fields = new Dictionary<string, object>(),
            PropertyFields = new Dictionary<string, object>(),
            GridRows = new List<SerializedGridRow>(),
            Children = children.ToList()
        };
    }

    private static HashSet<string> EntrySet(params string[] files) =>
        new(files.Select(ContentDeserializer.NormalizeFileKey), StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void FullPipelineKeys_MatchingPage_IsKept()
    {
        var pages = new List<SerializedPage> { MakePage("_content/Swift 2/Posts/page.yml") };
        var kept = ContentDeserializer.PruneToEntryFiles(pages, EntrySet("_content/Swift 2/Posts/page.yml"));
        Assert.Single(kept);
    }

    [Fact]
    public void ZipImportKeys_WithoutContentPrefix_StillMatch()
    {
        // Zip-import entry files lack the "_content/" prefix while the store tags pages with it.
        var pages = new List<SerializedPage> { MakePage("_content/Swift 2/Posts/page.yml") };
        var kept = ContentDeserializer.PruneToEntryFiles(pages, EntrySet("Swift 2/Posts/page.yml"));
        Assert.Single(kept);
    }

    [Fact]
    public void NonMatchingPage_IsPruned()
    {
        var pages = new List<SerializedPage> { MakePage("_content/Swift 2/Home/page.yml") };
        var kept = ContentDeserializer.PruneToEntryFiles(pages, EntrySet("Swift 2/Posts/page.yml"));
        Assert.Empty(kept);
    }

    [Fact]
    public void UnmatchedParentWithMatchedChild_IsKeptAsAncestor()
    {
        var child = MakePage("_content/Swift 2/Posts/Article/page.yml");
        var parent = MakePage("_content/Swift 2/Posts/page.yml", child);
        var pages = new List<SerializedPage> { parent };

        var kept = ContentDeserializer.PruneToEntryFiles(pages, EntrySet("Swift 2/Posts/Article/page.yml"));

        Assert.Single(kept);
        Assert.Single(kept[0].Children);
    }
}

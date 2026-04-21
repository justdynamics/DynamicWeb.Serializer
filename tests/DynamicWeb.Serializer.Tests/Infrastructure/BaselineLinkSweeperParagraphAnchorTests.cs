using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Phase 38 B.5 (D-38-09): paragraph-anchor validation.
/// Default.aspx?ID=X#Y now validates BOTH X (against page source IDs) and
/// Y (against paragraph source IDs). Fixes the 15717 false-positive in the
/// Swift 2.2 baseline, where 15717 was a paragraph source ID but the sweep
/// treated it as a page ID and flagged it as orphan.
/// </summary>
[Trait("Category", "Phase38")]
public class BaselineLinkSweeperParagraphAnchorTests
{
    private static SerializedPage MakePage(
        int sourceId,
        string? shortcut = null,
        List<int>? paragraphSourceIds = null)
    {
        var paragraphs = new List<SerializedParagraph>();
        if (paragraphSourceIds != null)
        {
            foreach (var pid in paragraphSourceIds)
            {
                paragraphs.Add(new SerializedParagraph
                {
                    ParagraphUniqueId = Guid.NewGuid(),
                    SourceParagraphId = pid,
                    SortOrder = 1,
                    Fields = new Dictionary<string, object>()
                });
            }
        }

        return new SerializedPage
        {
            PageUniqueId = Guid.NewGuid(),
            SourcePageId = sourceId,
            Name = $"P{sourceId}",
            MenuText = $"P{sourceId}",
            UrlName = $"p{sourceId}",
            SortOrder = 1,
            ShortCut = shortcut,
            Fields = new Dictionary<string, object>(),
            PropertyFields = new Dictionary<string, object>(),
            GridRows = paragraphs.Count > 0
                ? new List<SerializedGridRow>
                {
                    new SerializedGridRow
                    {
                        Id = Guid.NewGuid(),
                        SortOrder = 1,
                        Columns = new List<SerializedGridColumn>
                        {
                            new SerializedGridColumn { Id = 1, Width = 12, Paragraphs = paragraphs }
                        }
                    }
                }
                : new List<SerializedGridRow>(),
            Children = new List<SerializedPage>()
        };
    }

    [Fact]
    public void Sweep_PageAndParagraph_BothResolve_CountsAsResolved()
    {
        // Page 4897 exists and contains paragraph 15717. Another page refs
        // Default.aspx?ID=4897#15717 → both parts resolve, no unresolved.
        var host = MakePage(sourceId: 4897, paragraphSourceIds: new List<int> { 15717 });
        var refr = MakePage(sourceId: 100, shortcut: "Default.aspx?ID=4897#15717");

        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { host, refr });

        Assert.Empty(result.Unresolved);
        Assert.Equal(1, result.ResolvedCount);
    }

    [Fact]
    public void Sweep_PageResolves_ParagraphDoesNot_Unresolved()
    {
        // Page 4897 exists but NO paragraph 99999. Ref Default.aspx?ID=4897#99999
        // → sweep reports 1 unresolved (the anchor).
        var host = MakePage(sourceId: 4897, paragraphSourceIds: new List<int> { 15717 });
        var refr = MakePage(sourceId: 100, shortcut: "Default.aspx?ID=4897#99999");

        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { host, refr });

        Assert.Single(result.Unresolved);
        Assert.Equal(99999, result.Unresolved[0].UnresolvablePageId);
        Assert.Contains("4897#99999", result.Unresolved[0].Context);
    }

    [Fact]
    public void Sweep_PageDoesNotResolve_AnchorNotChecked()
    {
        // Page 9999 does NOT exist. Ref Default.aspx?ID=9999#15717.
        // Sweep reports 1 unresolved (the page). Anchor validation is skipped
        // since page failed first.
        var refr = MakePage(sourceId: 100, shortcut: "Default.aspx?ID=9999#15717");

        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { refr });

        Assert.Single(result.Unresolved);
        Assert.Equal(9999, result.Unresolved[0].UnresolvablePageId);
    }

    [Fact]
    public void Sweep_NoAnchor_Unchanged()
    {
        // Guard existing behavior: a page ref with no #anchor resolves iff the page exists.
        var host = MakePage(sourceId: 4897);
        var refr = MakePage(sourceId: 100, shortcut: "Default.aspx?ID=4897");

        var result = new BaselineLinkSweeper().Sweep(new List<SerializedPage> { host, refr });

        Assert.Empty(result.Unresolved);
        Assert.Equal(1, result.ResolvedCount);
    }
}

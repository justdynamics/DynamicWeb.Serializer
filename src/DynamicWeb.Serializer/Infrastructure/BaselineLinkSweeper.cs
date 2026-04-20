using System.Text.RegularExpressions;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Phase 37-05 / LINK-02 pass 1 (D-22): describes a single <c>Default.aspx?ID=N</c>
/// or <c>"SelectedValue": "N"</c> reference whose target page is NOT present in the
/// serialized baseline. Emitted by <see cref="BaselineLinkSweeper.Sweep"/> so the
/// serializer can fail with an actionable, source-locatable error.
/// </summary>
public record UnresolvedLink(
    string SourcePageIdentifier,
    string FieldName,
    int UnresolvablePageId,
    string Context);

/// <summary>
/// Result of a <see cref="BaselineLinkSweeper.Sweep"/> run.
/// </summary>
public record SweepResult(IReadOnlyList<UnresolvedLink> Unresolved, int ResolvedCount);

/// <summary>
/// Phase 37-05 / LINK-02 pass 1 (D-22): post-serialize sweep. Walks every page (and
/// its nested children / grid rows / paragraphs) in a freshly-serialized tree,
/// extracts every <c>Default.aspx?ID=N</c> and <c>"SelectedValue": "N"</c>
/// reference, and verifies the target page's <see cref="SerializedPage.SourcePageId"/>
/// is present in the same tree. Unresolved references are returned for the caller
/// to surface as a hard error — a baseline with orphan links is not safe to commit.
/// </summary>
public class BaselineLinkSweeper
{
    private static readonly Regex InternalLinkPattern = new(
        @"(Default\.aspx\?ID=)(\d+)(#(\d+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SelectedValuePattern = new(
        @"(""SelectedValue"":\s*"")(\d+)("")",
        RegexOptions.Compiled);

    public SweepResult Sweep(List<SerializedPage> allPages)
    {
        var validSourceIds = new HashSet<int>();
        CollectSourceIds(allPages, validSourceIds);

        var unresolved = new List<UnresolvedLink>();
        int resolved = 0;

        foreach (var page in allPages)
            WalkPage(page, PageIdent(page), validSourceIds, unresolved, ref resolved);

        return new SweepResult(unresolved, resolved);
    }

    private static string PageIdent(SerializedPage p) =>
        $"page {p.PageUniqueId} ({p.MenuText ?? p.UrlName ?? "unnamed"})";

    private static void CollectSourceIds(IEnumerable<SerializedPage> pages, HashSet<int> acc)
    {
        foreach (var p in pages)
        {
            if (p.SourcePageId.HasValue) acc.Add(p.SourcePageId.Value);
            CollectSourceIds(p.Children, acc);
        }
    }

    private void WalkPage(
        SerializedPage page,
        string ident,
        HashSet<int> validIds,
        List<UnresolvedLink> unresolved,
        ref int resolved)
    {
        CheckField(page.ShortCut, ident, "ShortCut", validIds, unresolved, ref resolved);

        if (page.NavigationSettings != null)
            CheckField(page.NavigationSettings.ProductPage, ident,
                "NavigationSettings.ProductPage", validIds, unresolved, ref resolved);

        foreach (var kvp in page.Fields)
            if (kvp.Value is string s)
                CheckField(s, ident, $"Fields.{kvp.Key}", validIds, unresolved, ref resolved);

        foreach (var kvp in page.PropertyFields)
            if (kvp.Value is string s)
                CheckField(s, ident, $"PropertyFields.{kvp.Key}", validIds, unresolved, ref resolved);

        // Walk paragraphs
        foreach (var row in page.GridRows)
        {
            foreach (var col in row.Columns)
            {
                foreach (var para in col.Paragraphs)
                {
                    var paraIdent = $"{ident}/paragraph {para.ParagraphUniqueId}";
                    foreach (var kvp in para.Fields)
                        if (kvp.Value is string s)
                            CheckField(s, paraIdent, $"Fields.{kvp.Key}",
                                validIds, unresolved, ref resolved);
                }
            }
        }

        foreach (var c in page.Children)
            WalkPage(c, PageIdent(c), validIds, unresolved, ref resolved);
    }

    private static void CheckField(
        string? value,
        string sourceIdent,
        string fieldName,
        HashSet<int> validIds,
        List<UnresolvedLink> unresolved,
        ref int resolved)
    {
        if (string.IsNullOrEmpty(value)) return;

        foreach (Match m in InternalLinkPattern.Matches(value))
        {
            if (!int.TryParse(m.Groups[2].Value, out var id)) continue;
            if (validIds.Contains(id)) { resolved++; continue; }
            unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, id, m.Value));
        }

        foreach (Match m in SelectedValuePattern.Matches(value))
        {
            if (!int.TryParse(m.Groups[2].Value, out var id)) continue;
            if (validIds.Contains(id)) { resolved++; continue; }
            unresolved.Add(new UnresolvedLink(sourceIdent, fieldName, id, m.Value));
        }

        // Note: raw-numeric references (plain "121" strings that should resolve to a page ID)
        // are NOT swept here — too many false positives on ordinary numeric fields (sort orders,
        // widths, etc.). The deserialize-time InternalLinkResolver still handles them via its
        // "entire string is a pure number AND is in the source-to-target map" check.
    }
}

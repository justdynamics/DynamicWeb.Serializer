using System.Text.RegularExpressions;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Serialization;

/// <summary>
/// Stateless helper that rewrites Default.aspx?ID=NNN patterns in strings
/// using a source-to-target page ID map. Boundary-aware regex ensures
/// ID=1 does not corrupt ID=12 (greedy \d+ captures full number).
/// Case-insensitive to handle default.aspx?id=NNN variants.
/// </summary>
public class InternalLinkResolver
{
    private readonly Dictionary<int, int> _sourceToTargetPageIds;
    private readonly Dictionary<int, int> _sourceToTargetParagraphIds;
    private readonly Action<string>? _log;
    private int _resolvedCount;
    private int _unresolvedCount;
    private int _paragraphResolvedCount;
    private int _paragraphUnresolvedCount;

    /// <summary>
    /// Boundary-aware regex: matches Default.aspx?ID=NNN optionally followed by #PPP.
    /// Group 1 = prefix (Default.aspx?ID=), Group 2 = page ID digits,
    /// Group 3 = full fragment (#PPP), Group 4 = paragraph ID digits.
    /// Greedy \d+ naturally captures the full number.
    /// IgnoreCase handles default.aspx?id= variants.
    /// </summary>
    private static readonly Regex InternalLinkPattern = new(
        @"(Default\.aspx\?ID=)(\d+)(#(\d+))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public InternalLinkResolver(
        Dictionary<int, int> sourceToTargetPageIds,
        Action<string>? log = null,
        Dictionary<int, int>? sourceToTargetParagraphIds = null)
    {
        _sourceToTargetPageIds = sourceToTargetPageIds;
        _log = log;
        _sourceToTargetParagraphIds = sourceToTargetParagraphIds ?? new Dictionary<int, int>();
    }

    /// <summary>
    /// Scans the input string for Default.aspx?ID=NNN patterns and rewrites
    /// source page IDs to target page IDs using the injected map.
    /// Unresolvable IDs are preserved unchanged and a warning is logged.
    /// Returns null for null input, empty for empty input.
    /// </summary>
    public string? ResolveLinks(string? fieldValue)
    {
        if (string.IsNullOrEmpty(fieldValue))
            return fieldValue;

        return InternalLinkPattern.Replace(fieldValue, match =>
        {
            var sourcePageId = int.Parse(match.Groups[2].Value);
            var hasFragment = match.Groups[4].Success;

            if (_sourceToTargetPageIds.TryGetValue(sourcePageId, out var targetPageId))
            {
                _resolvedCount++;
                var result = match.Groups[1].Value + targetPageId.ToString();

                if (hasFragment)
                {
                    var sourceParagraphId = int.Parse(match.Groups[4].Value);
                    if (_sourceToTargetParagraphIds.TryGetValue(sourceParagraphId, out var targetParagraphId))
                    {
                        _paragraphResolvedCount++;
                        result += "#" + targetParagraphId.ToString();
                    }
                    else
                    {
                        _log?.Invoke($"  WARNING: Unresolvable paragraph ID {sourceParagraphId} in anchor link");
                        _paragraphUnresolvedCount++;
                        result += "#" + sourceParagraphId.ToString();
                    }
                }

                return result;
            }
            else
            {
                _log?.Invoke($"  WARNING: Unresolvable page ID {sourcePageId} in link");
                _unresolvedCount++;
                return match.Value;
            }
        });
    }

    /// <summary>
    /// Builds a source-to-target page ID mapping from serialized pages and
    /// the PageGuidCache. Combines: SourcePageId (from YAML) -> PageUniqueId (GUID)
    /// -> target page ID (from PageGuidCache). Recursively flattens children.
    /// Pages without SourcePageId or not found in cache are skipped.
    /// </summary>
    public static Dictionary<int, int> BuildSourceToTargetMap(
        List<SerializedPage> pages,
        Dictionary<Guid, int> pageGuidCache)
    {
        var map = new Dictionary<int, int>();
        CollectSourcePageIds(pages, pageGuidCache, map);
        return map;
    }

    /// <summary>
    /// Returns cumulative (resolved, unresolved, paragraphResolved, paragraphUnresolved)
    /// link counts across all ResolveLinks calls on this instance.
    /// </summary>
    public (int resolved, int unresolved, int paragraphResolved, int paragraphUnresolved) GetStats() =>
        (_resolvedCount, _unresolvedCount, _paragraphResolvedCount, _paragraphUnresolvedCount);

    /// <summary>
    /// Builds a source-to-target paragraph ID mapping by recursively walking
    /// pages -> GridRows -> Columns -> Paragraphs. For each paragraph with
    /// SourceParagraphId and ParagraphUniqueId found in the cache:
    /// map[SourceParagraphId] = paragraphGuidCache[ParagraphUniqueId].
    /// </summary>
    public static Dictionary<int, int> BuildSourceToTargetParagraphMap(
        List<SerializedPage> pages,
        Dictionary<Guid, int> paragraphGuidCache)
    {
        var map = new Dictionary<int, int>();
        CollectSourceParagraphIds(pages, paragraphGuidCache, map);
        return map;
    }

    private static void CollectSourceParagraphIds(
        List<SerializedPage> pages,
        Dictionary<Guid, int> paragraphGuidCache,
        Dictionary<int, int> map)
    {
        foreach (var page in pages)
        {
            foreach (var row in page.GridRows)
            {
                foreach (var column in row.Columns)
                {
                    foreach (var para in column.Paragraphs)
                    {
                        if (para.SourceParagraphId.HasValue &&
                            paragraphGuidCache.TryGetValue(para.ParagraphUniqueId, out var targetId))
                        {
                            map[para.SourceParagraphId.Value] = targetId;
                        }
                    }
                }
            }

            if (page.Children.Count > 0)
            {
                CollectSourceParagraphIds(page.Children, paragraphGuidCache, map);
            }
        }
    }

    private static void CollectSourcePageIds(
        List<SerializedPage> pages,
        Dictionary<Guid, int> pageGuidCache,
        Dictionary<int, int> map)
    {
        foreach (var page in pages)
        {
            if (page.SourcePageId.HasValue &&
                pageGuidCache.TryGetValue(page.PageUniqueId, out var targetId))
            {
                map[page.SourcePageId.Value] = targetId;
            }

            if (page.Children.Count > 0)
            {
                CollectSourcePageIds(page.Children, pageGuidCache, map);
            }
        }
    }
}

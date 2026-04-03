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
    private readonly Action<string>? _log;
    private int _resolvedCount;
    private int _unresolvedCount;

    /// <summary>
    /// Boundary-aware regex: matches Default.aspx?ID=NNN where NNN is
    /// a sequence of digits. Greedy \d+ naturally captures the full number,
    /// so ID=12 matches as "12" not "1" followed by "2".
    /// IgnoreCase handles default.aspx?id= variants.
    /// </summary>
    private static readonly Regex InternalLinkPattern = new(
        @"(Default\.aspx\?ID=)(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public InternalLinkResolver(
        Dictionary<int, int> sourceToTargetPageIds,
        Action<string>? log = null)
    {
        _sourceToTargetPageIds = sourceToTargetPageIds;
        _log = log;
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
            var sourceId = int.Parse(match.Groups[2].Value);
            if (_sourceToTargetPageIds.TryGetValue(sourceId, out var targetId))
            {
                _resolvedCount++;
                return match.Groups[1].Value + targetId.ToString();
            }
            else
            {
                _log?.Invoke($"  WARNING: Unresolvable page ID {sourceId} in link");
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
    /// Returns cumulative (resolved, unresolved) link counts across all
    /// ResolveLinks calls on this instance.
    /// </summary>
    public (int resolved, int unresolved) GetStats() =>
        (_resolvedCount, _unresolvedCount);

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

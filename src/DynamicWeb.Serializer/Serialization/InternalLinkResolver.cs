using System.Text.RegularExpressions;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Serialization;

/// <summary>
/// Stateless helper that rewrites Default.aspx?ID=NNN patterns in strings
/// using a source-to-target page ID map. Boundary-aware regex ensures
/// ID=1 does not corrupt ID=12.
/// </summary>
public class InternalLinkResolver
{
    private readonly Dictionary<int, int> _sourceToTargetPageIds;
    private readonly Action<string>? _log;
    private int _resolvedCount;
    private int _unresolvedCount;

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

    public string? ResolveLinks(string? fieldValue)
    {
        // Stub: return as-is for RED phase
        return fieldValue;
    }

    public static Dictionary<int, int> BuildSourceToTargetMap(
        List<SerializedPage> pages,
        Dictionary<Guid, int> pageGuidCache)
    {
        // Stub: return empty for RED phase
        return new Dictionary<int, int>();
    }

    public (int resolved, int unresolved) GetStats() => (0, 0);
}

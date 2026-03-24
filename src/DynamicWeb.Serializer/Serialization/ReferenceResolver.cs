using Dynamicweb.Content;

namespace DynamicWeb.Serializer.Serialization;

/// <summary>
/// Resolves numeric DW content IDs to GUIDs for cross-reference fields.
/// Caches lookups to avoid repeated service calls. Call Clear() between serialization runs.
/// </summary>
public class ReferenceResolver
{
    private readonly Dictionary<int, Guid> _pageGuidCache = new();
    private readonly Dictionary<int, Guid> _paragraphGuidCache = new();

    /// <summary>
    /// Resolves a numeric page ID to its GUID. Returns null if the ID is invalid or the page is not found.
    /// </summary>
    public Guid? ResolvePageGuid(int numericId)
    {
        if (numericId <= 0)
            return null;

        if (_pageGuidCache.TryGetValue(numericId, out var cached))
            return cached;

        var page = Services.Pages.GetPage(numericId);
        if (page != null)
        {
            _pageGuidCache[numericId] = page.UniqueId;
            return page.UniqueId;
        }

        Console.Error.WriteLine($"[Serializer] Warning: Could not resolve page ID {numericId} to GUID");
        return null;
    }

    /// <summary>
    /// Registers a paragraph's numeric ID to GUID mapping. Called by ContentMapper during paragraph mapping
    /// so that ResolveParagraphGuid can look up paragraphs encountered during the traversal.
    /// </summary>
    public void RegisterParagraph(int numericId, Guid uniqueId)
    {
        if (numericId > 0)
            _paragraphGuidCache[numericId] = uniqueId;
    }

    /// <summary>
    /// Resolves a numeric paragraph ID to its GUID. Returns null if the ID is invalid or not yet registered.
    /// Paragraphs are registered during traversal via RegisterParagraph().
    /// </summary>
    public Guid? ResolveParagraphGuid(int numericId)
    {
        if (numericId <= 0)
            return null;

        if (_paragraphGuidCache.TryGetValue(numericId, out var cached))
            return cached;

        Console.Error.WriteLine($"[Serializer] Warning: Could not resolve paragraph ID {numericId} to GUID (not yet registered)");
        return null;
    }

    /// <summary>
    /// Clears all caches. Call between serialization runs to release memory and avoid stale entries.
    /// </summary>
    public void Clear()
    {
        _pageGuidCache.Clear();
        _paragraphGuidCache.Clear();
    }
}

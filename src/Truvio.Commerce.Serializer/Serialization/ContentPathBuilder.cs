using Dynamicweb.Content;

namespace Truvio.Commerce.Serializer.Serialization;

/// <summary>
/// Builds the predicate-space content path ("/Parent/Child") for a DW page by walking its
/// parent chain. This is the path predicates and excludes are authored against.
/// </summary>
internal static class ContentPathBuilder
{
    public static string BuildContentPath(Page page)
    {
        var segments = new List<string>();
        var current = page;
        while (current != null)
        {
            segments.Insert(0, current.MenuText ?? string.Empty);
            current = current.ParentPageId > 0
                ? Services.Pages.GetPage(current.ParentPageId)
                : null;
        }
        return "/" + string.Join("/", segments);
    }
}

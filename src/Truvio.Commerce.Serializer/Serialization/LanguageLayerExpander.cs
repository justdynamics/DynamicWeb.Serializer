using Dynamicweb.Content;
using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Serialization;

/// <summary>
/// Expands Content predicates flagged with <see cref="ProviderPredicateDefinition.IncludeLanguageLayers"/>
/// into one synthetic predicate per language-layer area (Area.MasterAreaId == predicate.AreaId).
/// Runs at serialize time only — the deserialize side is manifest-driven, so each expanded
/// predicate produces its own manifest entry and round-trips like any other area.
/// </summary>
public static class LanguageLayerExpander
{
    /// <summary>
    /// Pure expansion: each flagged Content predicate is followed by synthetic copies for the
    /// language-area ids supplied by <paramref name="getLanguageAreaIds"/>. The synthetic
    /// predicates keep the master's Path (language pages are matched in master-path space via
    /// their master-page chain) and never re-expand.
    /// </summary>
    public static List<ProviderPredicateDefinition> Expand(
        IEnumerable<ProviderPredicateDefinition> predicates,
        Func<int, IReadOnlyList<int>> getLanguageAreaIds,
        Action<string>? log = null)
    {
        var result = new List<ProviderPredicateDefinition>();
        foreach (var predicate in predicates)
        {
            result.Add(predicate);

            if (!predicate.IncludeLanguageLayers)
                continue;
            if (!string.Equals(predicate.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
                continue;

            var languageAreaIds = getLanguageAreaIds(predicate.AreaId);
            if (languageAreaIds.Count == 0)
            {
                log?.Invoke($"Predicate '{predicate.Name}': includeLanguageLayers is set but area {predicate.AreaId} has no language layers.");
                continue;
            }

            foreach (var languageAreaId in languageAreaIds)
            {
                result.Add(predicate with
                {
                    Name = $"{predicate.Name}-lang-area-{languageAreaId}",
                    AreaId = languageAreaId,
                    IncludeLanguageLayers = false
                });
                log?.Invoke($"Predicate '{predicate.Name}': expanded to language-layer area {languageAreaId}.");
            }
        }

        return result;
    }

    /// <summary>
    /// DW-backed lookup of language-layer area ids for a master area.
    /// Returns empty when the DW runtime is unavailable (unit tests).
    /// </summary>
    public static IReadOnlyList<int> GetLanguageAreaIdsFromDw(int masterAreaId)
    {
        try
        {
            return Services.Areas.GetAreas()
                .Where(a => a.MasterAreaId == masterAreaId)
                .Select(a => a.ID)
                .OrderBy(id => id)
                .ToList();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }
}

using Dynamicweb.ContentSync.Models;

namespace Dynamicweb.ContentSync.Configuration;

public class ContentPredicate
{
    private readonly ProviderPredicateDefinition _definition;

    public ContentPredicate(ProviderPredicateDefinition definition)
    {
        _definition = definition;
    }

    /// <summary>
    /// Returns true if the given content path and areaId should be included according to this predicate.
    /// Exclude rules override include rules.
    /// </summary>
    public bool ShouldInclude(string contentPath, int areaId)
    {
        if (areaId != _definition.AreaId)
            return false;

        if (!IsUnderPath(contentPath, _definition.Path))
            return false;

        foreach (var exclude in _definition.Excludes)
        {
            if (IsUnderPath(contentPath, exclude))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if candidatePath equals basePath or starts with basePath followed by a "/" (path boundary check).
    /// Comparison is case-insensitive (OrdinalIgnoreCase).
    /// </summary>
    private static bool IsUnderPath(string candidatePath, string basePath)
    {
        if (string.Equals(candidatePath, basePath, StringComparison.OrdinalIgnoreCase))
            return true;

        return candidatePath.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Aggregates multiple ContentPredicate instances and evaluates them with OR logic:
/// a path is included if ANY predicate includes it.
/// </summary>
public class ContentPredicateSet
{
    private readonly List<ContentPredicate> _predicates;

    public ContentPredicateSet(SyncConfiguration configuration)
    {
        _predicates = configuration.Predicates
            .Select(p => new ContentPredicate(p))
            .ToList();
    }

    /// <summary>
    /// Returns true if any predicate in the set includes the given content path and areaId.
    /// </summary>
    public bool ShouldInclude(string contentPath, int areaId)
    {
        return _predicates.Any(p => p.ShouldInclude(contentPath, areaId));
    }
}

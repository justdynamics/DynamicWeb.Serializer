using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>Deploy-coverage state of a single content node.</summary>
public enum ContentCoverage
{
    /// <summary>Not covered by any deploy predicate, and nothing below it is.</summary>
    None,
    /// <summary>
    /// Either the node is covered but part of its subtree is carved out by an exclude,
    /// or the node itself is not covered while a deploy predicate targets a subtree below it.
    /// </summary>
    Partial,
    /// <summary>The node and (as far as path algebra can tell) its entire subtree are covered.</summary>
    Full
}

/// <summary>Coverage verdict plus a human-readable explanation for the tree tooltip.</summary>
public sealed record ContentCoverageResult(ContentCoverage Coverage, string Explanation)
{
    public static readonly ContentCoverageResult None = new(ContentCoverage.None, "");
}

/// <summary>
/// Pure path-algebra evaluation of how deploy-mode Content predicates cover a content node:
/// fully managed, partially managed (excluded descendants, or managed subtrees below an
/// unmanaged page), or not managed. The explanation names the predicate and the exclude /
/// subtree paths responsible, so a developer can see WHY a page deploys or not straight
/// from the tree tooltip.
/// </summary>
public class ContentCoverageEvaluator
{
    private readonly List<ProviderPredicateDefinition> _predicates;

    /// <param name="deployContentPredicates">
    /// Deploy-mode Content predicates (already expanded for language layers when applicable).
    /// </param>
    public ContentCoverageEvaluator(IEnumerable<ProviderPredicateDefinition> deployContentPredicates)
    {
        _predicates = deployContentPredicates.ToList();
    }

    public ContentCoverageResult Evaluate(string contentPath, int areaId)
    {
        var areaPredicates = _predicates.Where(p => p.AreaId == areaId).ToList();
        if (areaPredicates.Count == 0)
            return ContentCoverageResult.None;

        var includedBy = areaPredicates.Where(p => Includes(p, contentPath)).ToList();

        if (includedBy.Count > 0)
        {
            // Excludes that carve descendants out of this node's subtree — unless another
            // predicate covers that excluded path again.
            var carvedOut = includedBy
                .SelectMany(p => p.Excludes)
                .Where(e => IsStrictlyUnder(e, contentPath))
                .Where(e => !areaPredicates.Any(q => Includes(q, e)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var names = string.Join("', '", includedBy.Select(p => p.Name));
            if (carvedOut.Count == 0)
                return new ContentCoverageResult(ContentCoverage.Full,
                    $"Managed at deploy by '{names}'");

            return new ContentCoverageResult(ContentCoverage.Partial,
                $"Partially managed at deploy by '{names}' — excluded below this page: {string.Join(", ", carvedOut)}");
        }

        // Node itself is not covered. Is a deploy predicate targeting a subtree below it?
        var managedBelow = areaPredicates
            .Where(p => IsStrictlyUnder(p.Path, contentPath))
            .Select(p => $"{p.Path} ('{p.Name}')")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (managedBelow.Count > 0)
            return new ContentCoverageResult(ContentCoverage.Partial,
                $"Not managed itself — contains deploy-managed subtree(s): {string.Join(", ", managedBelow)}");

        return ContentCoverageResult.None;
    }

    /// <summary>Single-predicate inclusion: under the predicate path and not under any of its excludes.</summary>
    private static bool Includes(ProviderPredicateDefinition predicate, string contentPath)
    {
        if (!ContentPredicate.IsUnderPath(contentPath, predicate.Path))
            return false;
        return !predicate.Excludes.Any(e => ContentPredicate.IsUnderPath(contentPath, e));
    }

    /// <summary>candidate lies under basePath but is not basePath itself.</summary>
    private static bool IsStrictlyUnder(string candidate, string basePath)
    {
        if (string.Equals(candidate, basePath, StringComparison.OrdinalIgnoreCase))
            return false;
        return ContentPredicate.IsUnderPath(candidate, basePath);
    }
}

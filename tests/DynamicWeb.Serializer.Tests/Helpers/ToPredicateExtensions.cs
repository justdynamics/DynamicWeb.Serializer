using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Tests.Helpers;

/// <summary>
/// Phase 43 transitional shim. The original Entry → Predicate direction (ToPredicate)
/// landed in Task 8 per CONTEXT D-04 and was deleted in Task 9 — Layer A test fixtures
/// retargeted directly to entry shapes, so no call sites remained.
///
/// <para>
/// The surviving Predicate → Entry direction (<see cref="ToManifestEntry"/>) is a Rule 3
/// reverse-shim: many Layer B integration tests in <c>Providers/Content/</c>,
/// <c>Providers/SqlTable/</c>, and <c>Integration/</c> dispatch through
/// <c>provider.Deserialize(...)</c> directly (not via the orchestrator's
/// <c>[Obsolete]</c> predicate overload), so the interface contract change in Phase 43
/// Task 3 broke them. Bridging via <see cref="ToManifestEntry"/> is the smallest diff
/// that keeps Phase 43's test suite green while Phase 44's CONVERGE-03 Layer B port
/// migrates those tests to entry fixtures. CONVERGE-03 deletes this file.
/// </para>
///
/// <para>
/// Lifecycle summary:
/// <list type="bullet">
/// <item>Phase 43 Task 8: file lands with both ToPredicate + ToManifestEntry shims.</item>
/// <item>Phase 43 Task 9: ToPredicate (Entry → Predicate, original D-04 direction)
/// deleted — zero call sites because Layer A retargeted to entry fixtures.</item>
/// <item>Phase 44 CONVERGE-03: ToManifestEntry deleted along with this file as part
/// of the Layer B port to entry fixtures.</item>
/// </list>
/// </para>
/// </summary>
internal static class ToPredicateExtensions
{
    /// <summary>
    /// Phase 43 Rule 3 reverse-shim: project a predicate fixture into a manifest entry
    /// so existing predicate-fixture-flavored Layer B tests can keep dispatching through
    /// the new ManifestEntry-typed <c>provider.Deserialize</c> contract without rewriting
    /// every call site. Phase 44 CONVERGE-03 ports those tests to entry fixtures and
    /// deletes this shim.
    /// </summary>
    internal static ManifestEntry ToManifestEntry(this ProviderPredicateDefinition predicate)
    {
        if (string.Equals(predicate.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentEntry
            {
                EntryId = $"content/area-{predicate.AreaId}",
                Files = Array.Empty<string>(),
                AreaId = predicate.AreaId,
                AreaName = $"Area {predicate.AreaId}",
                Path = string.IsNullOrEmpty(predicate.Path) ? "/" : predicate.Path,
                PageId = predicate.PageId,
                AcknowledgedOrphanPageIds = predicate.AcknowledgedOrphanPageIds.ToList(),
                ExcludeAreaColumns = predicate.ExcludeAreaColumns.ToList()
            };
        }

        if (string.Equals(predicate.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlTableEntry
            {
                EntryId = $"sql/{predicate.Table}",
                Files = Array.Empty<string>(),
                Table = predicate.Table ?? string.Empty,
                NameColumn = predicate.NameColumn,
                CompareColumns = predicate.CompareColumns,
                XmlColumns = predicate.XmlColumns.ToList(),
                ResolveLinksInColumns = predicate.ResolveLinksInColumns.ToList(),
                ServiceCaches = predicate.ServiceCaches.ToList(),
                SchemaSync = predicate.SchemaSync
            };
        }

        throw new InvalidOperationException(
            $"ToManifestEntry: unsupported providerType '{predicate.ProviderType}' (test fixtures only support Content + SqlTable)");
    }
}

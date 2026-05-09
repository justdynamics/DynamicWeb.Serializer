using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Tests.Helpers;

/// <summary>
/// Phase 43 transitional shim per CONTEXT D-04. Bridges Layer-B integration tests (which
/// still use predicate fixtures) over the orchestrator pivot until Phase 44's Layer-B
/// port migrates them to entry fixtures. DELETED at the end of Phase 43 (Task 9) along
/// with ProviderPredicateDefinition references in test fixtures this PLAN touched.
///
/// <para>The shim's API is intentionally minimal — only the fields needed by the surviving
/// predicate-typed orchestrator overload (now [Obsolete]). Field-by-field projection;
/// no semantic transformation.</para>
///
/// <para>Layer A tests (orchestrator unit tests in SerializerOrchestratorTests) do NOT use
/// the shim — they construct entry fixtures directly. The shim only exists to keep the
/// existing predicate-fixture provider integration tests compiling against the [Obsolete]
/// DeserializeAll(predicates, ...) signature that Phase 44 deletes.</para>
/// </summary>
internal static class ToPredicateExtensions
{
    /// <summary>Project a <see cref="ContentEntry"/> back into a synthetic predicate for tests.</summary>
    internal static ProviderPredicateDefinition ToPredicate(this ContentEntry entry) =>
        new()
        {
            Name = entry.EntryId,
            ProviderType = "Content",
            AreaId = entry.AreaId,
            Path = entry.Path,
            PageId = entry.PageId,
            AcknowledgedOrphanPageIds = entry.AcknowledgedOrphanPageIds.ToList(),
            ExcludeAreaColumns = entry.ExcludeAreaColumns.ToList()
        };

    /// <summary>Project a <see cref="SqlTableEntry"/> back into a synthetic predicate for tests.</summary>
    internal static ProviderPredicateDefinition ToPredicate(this SqlTableEntry entry) =>
        new()
        {
            Name = entry.EntryId,
            ProviderType = "SqlTable",
            Table = entry.Table,
            NameColumn = entry.NameColumn,
            CompareColumns = entry.CompareColumns,
            XmlColumns = entry.XmlColumns.ToList(),
            ResolveLinksInColumns = entry.ResolveLinksInColumns.ToList(),
            ServiceCaches = entry.ServiceCaches.ToList(),
            SchemaSync = entry.SchemaSync
        };

    /// <summary>
    /// Phase 43 / D-04 reverse shim: project a predicate fixture into a manifest entry
    /// so existing predicate-fixture-flavored Layer B tests can keep dispatching through
    /// the new ManifestEntry-typed <c>provider.Deserialize</c> contract without rewriting
    /// every call site to construct entries from scratch. Same lifecycle as
    /// <see cref="ToPredicate(ContentEntry)"/> — deleted at end of Phase 43 (Task 9).
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

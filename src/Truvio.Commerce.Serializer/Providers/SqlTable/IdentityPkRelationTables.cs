using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Providers.SqlTable;

/// <summary>
/// Knowledge base for identity-PK relation tables (LRN-hosted-publish-10). Some DW relation
/// tables carry an identity auto-id as their PRIMARY KEY — e.g. <c>EcomVariantOptionsProductRelation</c>,
/// the variant *combination* table. Auto-ids are environment-local: a payload carrying explicit
/// auto-id values collides with the target's own rows, so the relation rows silently never land
/// (the engine reported <c>0 failed</c>) — and DW's cart then refuses every add-to-cart because
/// the variant combination does not exist. The failure is invisible outside the event log.
///
/// <para>
/// The durable fix: for these tables, rows must be matched/inserted by their NATURAL KEY (the
/// FK pair that defines the relation) and the target assigns its own auto-id. This class maps
/// each known table to its natural-key columns. The mapping only activates when
/// <see cref="GetNaturalKey"/>'s guards hold on the LIVE target schema: the table's PK must be
/// exactly its identity column(s), and every mapped natural-key column must exist — otherwise
/// the legacy PK-matching behavior is preserved (safe fallback, e.g. on DW versions where the
/// PK is the composite natural pair already).
/// </para>
///
/// <para>
/// Same pattern precedent as <see cref="Configuration.RuntimeExcludes"/>: the engine ships
/// DW-specific per-table knowledge in code.
/// </para>
/// </summary>
public static class IdentityPkRelationTables
{
    /// <summary>
    /// Table → natural-key columns (the columns that define the relation row's identity).
    /// Every entry is guarded by <see cref="GetNaturalKey"/> against the live schema, so a
    /// column-name mismatch on some platform version degrades to the legacy behavior rather
    /// than misfiring.
    /// </summary>
    private static readonly Dictionary<string, string[]> NaturalKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        // The variant combination table — the proven, customer-visible case (add-to-cart).
        ["EcomVariantOptionsProductRelation"] =
            ["VariantOptionsProductRelationProductID", "VariantOptionsProductRelationVariantID"],
        // Shop↔group relation — the same class, called out alongside it.
        ["EcomShopGroupRelation"] =
            ["ShopGroupShopID", "ShopGroupGroupID"],
        // Product↔variant-group relation — same shape as the combination table.
        ["EcomVariantGroupProductRelation"] =
            ["VariantGroupProductRelationProductID", "VariantGroupProductRelationVariantGroupID"],
        // User↔group relation — same class (auto-id PK over an FK pair).
        ["AccessUserGroupRelation"] =
            ["AccessUserGroupRelationUserId", "AccessUserGroupRelationGroupId"],
    };

    /// <summary>
    /// True when the table's PRIMARY KEY consists solely of identity (auto-increment) columns —
    /// the shape whose key values are environment-local and must not be used for cross-environment
    /// row matching.
    /// </summary>
    public static bool IsIdentityOnlyPk(TableMetadata metadata)
    {
        return metadata.KeyColumns.Count > 0
               && metadata.KeyColumns.All(k =>
                   metadata.IdentityColumns.Contains(k, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the natural-key columns for <paramref name="metadata"/>'s table when natural-key
    /// matching should be applied, else null. Guards (ALL must hold):
    /// <list type="number">
    /// <item>the table is in the knowledge base;</item>
    /// <item>the live PK is identity-only (<see cref="IsIdentityOnlyPk"/>) — on schemas where the
    /// PK is already the natural pair, the legacy path is correct and nothing changes;</item>
    /// <item>every mapped natural-key column exists on the live table (schema-variance guard).</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string>? GetNaturalKey(TableMetadata metadata)
    {
        if (!NaturalKeys.TryGetValue(metadata.TableName, out var columns))
            return null;

        if (!IsIdentityOnlyPk(metadata))
            return null;

        if (!columns.All(c => metadata.AllColumns.Contains(c, StringComparer.OrdinalIgnoreCase)))
            return null;

        return columns;
    }

    /// <summary>
    /// Heuristic for the WARN path: identity-PK tables that look like relation tables (DW names
    /// them <c>*Relation*</c>) but have no natural-key mapping. For those, an auto-id collision
    /// (payload auto-id matching an existing target row with different content) is the silent
    /// failure class of LRN-hosted-publish-10 and must be surfaced loudly.
    /// </summary>
    public static bool LooksLikeRelationTable(string tableName)
        => tableName.Contains("Relation", StringComparison.OrdinalIgnoreCase);
}

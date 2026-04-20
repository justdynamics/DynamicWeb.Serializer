namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Curated flat list of runtime-only DW columns auto-excluded from SqlTable serialization.
/// These columns hold visit counters, env-specific search-index pointers, and other data
/// that is either recomputed at runtime or meaningful only in a specific environment.
///
/// Per D-07 (Phase 37 CONTEXT.md): a SINGLE FLAT LIST. No Runtime-vs-Credential
/// classification. Credentials (payment gateway keys, carrier tokens) must be listed
/// manually by the user via <c>excludeFields</c> until v0.6.0 ships the env-config
/// workflow (DEFERRED.md, CRED-01).
///
/// A predicate can opt-back-in to any of these columns by listing the name in its
/// <c>includeFields</c>; <see cref="GetAutoExcludedColumns"/> minus <c>includeFields</c>
/// yields the effective auto-exclude set that SqlTableProvider applies.
/// </summary>
public static class RuntimeExcludes
{
    /// <summary>
    /// Table name → runtime-only columns. Rationale tracked per entry. Conservative —
    /// add an entry only when the column is demonstrably runtime-only on every DW 10.x install.
    /// </summary>
    private static readonly Dictionary<string, string[]> Map =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // F-07: visit counter on URL path table — recomputed at runtime, overwrites target on deploy.
        ["UrlPath"] = new[] { "UrlPathVisitsCount" },

        // F-06: env-specific search-index bindings; differ between Azure dev/test/QA/prod.
        ["EcomShops"] = new[]
        {
            "ShopIndexRepository",    // env-specific index repository name
            "ShopIndexName",          // env-specific index name
            "ShopIndexDocumentType",  // env-specific document type
            "ShopIndexUpdate",        // last-updated tick, env-runtime
            "ShopIndexBuilder"        // env-specific index builder type
        }
    };

    /// <summary>
    /// Returns the auto-excluded columns for a table (empty collection if none).
    /// Callers typically take this set minus <c>predicate.IncludeFields</c> to get the
    /// effective list to add on top of <c>predicate.ExcludeFields</c>.
    /// </summary>
    public static IReadOnlyCollection<string> GetAutoExcludedColumns(string tableName)
    {
        return Map.TryGetValue(tableName, out var cols)
            ? cols
            : Array.Empty<string>();
    }

    /// <summary>Full map accessor — used for README generation and admin UI display.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> All =>
        Map.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyCollection<string>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);
}

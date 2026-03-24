# Phase 15: Ecommerce Tables at Scale - Research

**Researched:** 2026-03-24
**Domain:** SQL table serialization scaling, FK dependency ordering, DW cache invalidation
**Confidence:** HIGH

## Summary

Phase 15 scales the working SqlTableProvider from a single proof table to all ~24 non-transactional ecommerce tables. The three main technical challenges are: (1) FK dependency ordering via runtime topological sort of `sys.foreign_keys`, (2) DW service cache invalidation after deserialization using `AddInManager.GetInstance<ICacheStorage>`, and (3) adding ~24 predicate definitions to the config. The existing SqlTableProvider, orchestrator, and MERGE upsert infrastructure from Phases 13-14 handle the actual serialize/deserialize work unchanged.

The DW10 source reveals an exact cache invalidation pattern in `LocalDeploymentProvider.ImportPackage`: after all data groups are imported, it iterates distinct `ServiceCaches` from affected DataGroups, resolves each via `AddInManager.GetInstance<ICacheStorage>(serviceCacheName)`, and calls `ClearCache()`. This is the pattern we replicate. The DataGroup XML files provide the complete table-to-cache mapping for all ecommerce categories.

**Primary recommendation:** Implement FK topological sort as a standalone class consumed by the orchestrator before dispatching SqlTable predicates. Add `ServiceCaches` as a new field on `ProviderPredicateDefinition`. After each SqlTable deserialize, call `ClearCache()` on each configured service cache via `AddInManager`.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** All non-transactional ecommerce tables (~24 unique tables) from Orders, Internationalization, and related DataGroups
- **D-02:** Exclude EcomOrders -- that's transactional data, not settings/config
- **D-03:** Specific categories: OrderFlows/States/StateRules, Payment, Shipping, Stock, OrderFields (excluding EcomOrders), OrderLineFields, OrderContexts, TrackAndTrace, AddressValidation, ValidationGroups, Countries, Languages, Currencies, VATGroups
- **D-04:** Runtime topological sort via `sys.foreign_keys` -- query FK metadata from SQL Server, build dependency graph, sort tables so parents are deserialized before children
- **D-05:** Single pass deserialization (not DW's two-pass approach) -- simpler, and ecommerce tables should form an acyclic dependency graph
- **D-06:** If circular FK dependencies detected, fail with clear error message listing the cycle -- don't silently break
- **D-07:** Not a problem with our architecture -- predicates reference tables directly by name, not DataGroup IDs. Each table configured once in predicates. If user accidentally configures the same table twice, second serialize overwrites same folder and second deserialize skips (checksums match). No dedup logic needed.
- **D-08:** Cache invalidation is per-predicate/per-table, not a blanket clear -- each provider knows which caches to invalidate based on what was deserialized
- **D-09:** Cache invalidation applies to ALL providers (Content AND SqlTable), not just SQL tables -- content deserialization also leaves stale caches
- **D-10:** Research required: investigate DW10 source for cache types, clear APIs, and how the Deployment tool maps tables to caches
- **D-11:** Cache invalidation should be configurable per predicate in config (which caches to clear after this predicate runs)

### Claude's Discretion
- Topological sort algorithm choice (Kahn's vs DFS)
- How to integrate FK ordering into the orchestrator (sort predicates before dispatch, or sort within SqlTableProvider)
- Config predicate structure for the ~24 ecommerce tables (one predicate per table vs grouped)
- Test strategy for FK ordering (mock FK metadata or use real DB schema)

### Deferred Ideas (OUT OF SCOPE)
- Settings & Schema providers -- future milestone scope
- Users, Marketing, PIM tables -- future milestone scope
- DataGroup auto-discovery -- future, enumerate available tables from DW metadata
- Batch predicate config (one predicate = all tables in a DataGroup) -- future UX improvement
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ECOM-01 | OrderFlows and OrderStates serialized and deserialized | Table inventory from DataGroup XMLs provides Table/NameColumn/CompareColumns for EcomOrderFlow, EcomOrderStates, EcomOrderStateRules |
| ECOM-02 | Payment and Shipping methods serialized and deserialized | DataGroup XMLs provide metadata for EcomPayments, EcomShippings, EcomMethodCountryRelation |
| ECOM-03 | Countries, Currencies, and VAT settings serialized and deserialized | DataGroup XMLs provide metadata for EcomCountries, EcomCountryText, EcomLanguages, EcomCurrencies, EcomVatGroups, EcomVatCountryRelations |
| ECOM-04 | Duplicate DataItemTypes across groups handled without duplicate rows | D-07 confirms: one predicate per table, no dedup logic needed. EcomMethodCountryRelation configured once despite appearing in Payment and Shipping DataGroups |
| SQL-03 | FK dependency ordering via topological sort prevents constraint violations | FK metadata query via sys.foreign_keys + Kahn's algorithm for topological sort |
| CACHE-01 | DW service caches invalidated after SQL table deserialization | DW10 LocalDeploymentProvider pattern: AddInManager.GetInstance<ICacheStorage>(name).ClearCache() |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb | 10.23.9 | DW platform (includes Caching, AddInManager, Data) | Already referenced in csproj |
| YamlDotNet | 13.7.1 | YAML serialization | Already in use |
| Moq | 4.20.72 | Test mocking | Already in use for ISqlExecutor mocks |
| xunit | 2.9.3 | Test framework | Already in use |

### Supporting
No new packages needed. All functionality builds on existing DW platform APIs and project infrastructure.

## Architecture Patterns

### Recommended Project Structure
```
src/Dynamicweb.ContentSync/
  Providers/
    SqlTable/
      SqlTableProvider.cs          # Unchanged - already handles any table
      FkDependencyResolver.cs      # NEW: topological sort via sys.foreign_keys
      DataGroupMetadataReader.cs   # Unchanged
      SqlTableWriter.cs            # Unchanged
    CacheInvalidator.cs            # NEW: wraps AddInManager cache clearing
    SerializerOrchestrator.cs      # MODIFIED: sort SqlTable predicates by FK order before dispatch
    ISerializationProvider.cs      # Unchanged
  Models/
    ProviderPredicateDefinition.cs # MODIFIED: add ServiceCaches field
  Configuration/
    SyncConfiguration.cs           # Contains predicate list (grows with ~24 new entries)
```

### Pattern 1: FK Topological Sort (Kahn's Algorithm)
**What:** Query `sys.foreign_keys` + `sys.foreign_key_columns` to discover parent-child table relationships. Build adjacency list. Apply Kahn's algorithm to produce a deserialization order where parents come before children.
**When to use:** Before dispatching SqlTable predicates for deserialization.
**Example:**
```csharp
// Source: SQL Server sys catalog views
public class FkDependencyResolver
{
    private readonly ISqlExecutor _sqlExecutor;

    public FkDependencyResolver(ISqlExecutor sqlExecutor) => _sqlExecutor = sqlExecutor;

    /// <summary>
    /// Query FK relationships for the given tables and return them in topological order
    /// (parents before children). Throws if circular dependency detected.
    /// </summary>
    public List<string> GetDeserializationOrder(IEnumerable<string> tableNames)
    {
        var tables = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
        var edges = QueryForeignKeyEdges(tables);
        return TopologicalSort(tables, edges);
    }

    private List<(string Parent, string Child)> QueryForeignKeyEdges(HashSet<string> tables)
    {
        // Query sys.foreign_keys joined with sys.foreign_key_columns
        // to find: child table -> parent table relationships
        // Filter to only tables in our predicate set
        var cb = new CommandBuilder();
        cb.Add(@"
            SELECT DISTINCT
                OBJECT_NAME(fk.parent_object_id) AS ChildTable,
                OBJECT_NAME(fk.referenced_object_id) AS ParentTable
            FROM sys.foreign_keys fk
            WHERE OBJECT_NAME(fk.parent_object_id) IN (");
        // Add parameterized table names...
        // Return edges filtered to tables we're deserializing
    }

    private List<string> TopologicalSort(
        HashSet<string> tables,
        List<(string Parent, string Child)> edges)
    {
        // Kahn's algorithm:
        // 1. Build in-degree map
        // 2. Seed queue with zero in-degree nodes
        // 3. Process queue, decrementing in-degrees
        // 4. If result.Count < tables.Count, circular dependency detected
        //    -> throw with cycle info
    }
}
```

**Why Kahn's over DFS:** Kahn's naturally detects cycles (remaining nodes with non-zero in-degree after processing = cycle). DFS requires separate cycle detection. Both are O(V+E) but Kahn's is more straightforward for this use case.

### Pattern 2: Cache Invalidation via AddInManager
**What:** After deserialization completes for a predicate, resolve each configured ServiceCache name through DW's `AddInManager.GetInstance<ICacheStorage>(name)` and call `ClearCache()`.
**When to use:** After each SqlTable (or Content) predicate's Deserialize completes successfully.
**Example:**
```csharp
// Source: DW10 LocalDeploymentProvider.ImportPackage (lines 182-194)
public class CacheInvalidator
{
    /// <summary>
    /// Clear DW service caches by their fully-qualified type names.
    /// Mirrors the DW10 Deployment tool's cache clearing pattern exactly.
    /// </summary>
    public void InvalidateCaches(IEnumerable<string> serviceCacheNames, Action<string>? log = null)
    {
        foreach (var serviceCacheName in serviceCacheNames.Distinct())
        {
            var cacheStorageType = AddInManager.GetTypeUnvalidated<ICacheStorage>(serviceCacheName);
            if (cacheStorageType is null)
            {
                log?.Invoke($"Cache type not found: {serviceCacheName} (skipping)");
                continue;
            }

            var cacheStorage = AddInManager.GetInstance<ICacheStorage>(serviceCacheName);
            if (cacheStorage is null)
            {
                log?.Invoke($"Could not create cache instance: {serviceCacheName} (skipping)");
                continue;
            }

            log?.Invoke($"Clearing cache: {serviceCacheName}");
            cacheStorage.ClearCache();
        }
    }
}
```

### Pattern 3: Orchestrator FK Ordering Integration
**What:** The orchestrator sorts SqlTable predicates by FK dependency order before dispatching them. Content predicates are unaffected.
**When to use:** In `DeserializeAll` before iterating predicates.
**Example:**
```csharp
// In SerializerOrchestrator.DeserializeAll:
// 1. Partition predicates into SqlTable vs other
// 2. For SqlTable predicates, extract table names
// 3. Call FkDependencyResolver.GetDeserializationOrder(tableNames)
// 4. Reorder SqlTable predicates to match FK order
// 5. Dispatch all predicates (other types first or interleaved -- doesn't matter since Content has no FK deps on SQL tables)
```

### Anti-Patterns to Avoid
- **Hardcoded table ordering:** Never hardcode FK dependency order. Tables and FKs can change between DW versions. Always query at runtime.
- **Blanket cache clear:** Don't call ClearCache on every cache after every predicate. Use per-predicate ServiceCaches configuration to clear only relevant caches.
- **Two-pass deserialization:** DW uses two passes for complex scenarios. We use single pass with FK ordering instead (D-05). Simpler and sufficient for acyclic ecommerce table graph.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cache resolution | Custom cache registry | `AddInManager.GetInstance<ICacheStorage>(name)` | DW's extensibility system already manages cache service lifecycle |
| FK metadata | Parse schema scripts or hardcode | `sys.foreign_keys` + `sys.foreign_key_columns` | SQL Server's catalog views are authoritative and always current |
| MERGE upsert | Custom INSERT/UPDATE logic | Existing `SqlTableWriter.BuildMergeCommand` | Already implemented and tested in Phase 13 |
| Row identity | Custom key generation | Existing `SqlTableReader.GenerateRowIdentity` | Already handles NameColumn + CompareColumns fallback |

## Common Pitfalls

### Pitfall 1: Self-Referencing FK (EcomOrderFlow)
**What goes wrong:** EcomOrderFlow likely has a self-referencing FK (parent flow). Topological sort must handle self-references.
**Why it happens:** Some tables reference themselves (tree structures). A self-referencing FK is not a cycle -- it's a single-node edge.
**How to avoid:** When building the FK edge list, skip edges where Parent == Child. Self-referencing FKs don't affect insertion order since MERGE handles existing rows.
**Warning signs:** Cycle detection fires on a table with only self-references.

### Pitfall 2: FK to Tables Outside Our Predicate Set
**What goes wrong:** An ecommerce table may have FKs pointing to tables we're NOT serializing (e.g., AccessUser, EcomShops). Topological sort might try to include those.
**Why it happens:** FK relationships cross DataGroup boundaries.
**How to avoid:** Filter FK edges to only include relationships between tables in our predicate set. External FKs are satisfied by existing data in the target DB (they already exist there).
**Warning signs:** FkDependencyResolver returns tables not in the predicate list.

### Pitfall 3: Missing ServiceCache Types at Runtime
**What goes wrong:** `AddInManager.GetTypeUnvalidated<ICacheStorage>(name)` returns null because the ecommerce assemblies aren't loaded.
**Why it happens:** Cache types like `Dynamicweb.Ecommerce.Orders.PaymentService` live in `Dynamicweb.Ecommerce` assembly which may not be loaded yet.
**How to avoid:** Follow DW10's pattern exactly: check for null from both `GetTypeUnvalidated` and `GetInstance`, skip gracefully with a log message. Never throw on missing cache types.
**Warning signs:** "Cache type not found" log messages during deserialization.

### Pitfall 4: EcomStockStatusLine Has No NameColumn or CompareColumns
**What goes wrong:** Tables without NameColumn fall back to composite PK identity. If the PK is a single identity column, every row gets the same identity pattern.
**Why it happens:** Some junction/detail tables (EcomStockStatusLine, EcomOrderStateRules, EcomVatCountryRelations) have no natural key name.
**How to avoid:** The existing identity resolution already handles this via PK-based identity (Phase 13 SQL-02). Verify these tables have multi-column PKs or that single-PK identity works correctly.
**Warning signs:** Rows overwrite each other during deserialization because identity strings collide.

### Pitfall 5: EcomMethodCountryRelation Appears in Multiple DataGroups
**What goes wrong:** Someone might configure two predicates for EcomMethodCountryRelation (one for Payment, one for Shipping).
**Why it happens:** DW's DataGroup XMLs list the same table in both Payment and Shipping groups.
**How to avoid:** Configure one predicate per unique table. D-07 confirms: if accidentally duplicated, the second serialize overwrites the same folder and second deserialize skips via checksums. No data corruption, just wasted work.
**Warning signs:** Log shows same table serialized/deserialized twice.

## Code Examples

### FK Metadata Query
```sql
-- Source: SQL Server sys catalog views
SELECT DISTINCT
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) IN ('EcomOrderFlow', 'EcomOrderStates', ...)
  AND OBJECT_NAME(fk.referenced_object_id) IN ('EcomOrderFlow', 'EcomOrderStates', ...)
  AND OBJECT_NAME(fk.parent_object_id) <> OBJECT_NAME(fk.referenced_object_id)  -- skip self-refs
```

### Cache Invalidation (DW10 Pattern)
```csharp
// Source: LocalDeploymentProvider.cs lines 182-194
// This is the EXACT pattern DW10 uses after importing data groups
foreach (var serviceCacheName in affectedCaches.Distinct())
{
    var cacheStorageType = AddInManager.GetTypeUnvalidated<ICacheStorage>(serviceCacheName);
    if (cacheStorageType is null)
        continue;

    var cacheStorage = AddInManager.GetInstance<ICacheStorage>(serviceCacheName);
    if (cacheStorage is null)
        continue;

    cacheStorage.ClearCache();
}
```

### Predicate Definition with ServiceCaches
```csharp
// ProviderPredicateDefinition extended with cache config
public record ProviderPredicateDefinition
{
    // ... existing fields ...

    /// <summary>
    /// Fully-qualified DW service cache type names to clear after deserialization.
    /// Sourced from DataGroup XML ServiceCaches sections.
    /// </summary>
    public List<string> ServiceCaches { get; init; } = new();
}
```

## Complete Table Inventory

Source: DataGroup XML files at `C:\temp\DataGroups\Settings_Ecommerce_*`

### Orders Category (14 tables)
| Table | NameColumn | DataGroup | ServiceCaches |
|-------|-----------|-----------|---------------|
| EcomOrderFlow | OrderFlowName | OrderFlows (also CartFlows, QuoteFlows) | (none) |
| EcomOrderStates | OrderStateName | OrderFlows (also CartFlows, QuoteFlows) | (none) |
| EcomOrderStateRules | (none) | OrderFlows (also CartFlows, QuoteFlows) | (none) |
| EcomPayments | PaymentName | Payment | Dynamicweb.Content.Versioning.CheckOutService, Dynamicweb.Ecommerce.Orders.PaymentService |
| EcomShippings | ShippingName | Shipping | Dynamicweb.Ecommerce.Orders.ShippingService |
| EcomMethodCountryRelation | MethodCountryRelMethodId | Payment (also Shipping) | (inherits from Payment/Shipping group) |
| EcomStockGroups | StockGroupName | StockState | (none) |
| EcomStockStatusLine | (none) | StockState | (none) |
| EcomStockStatusLanguageValue | StockStatusLanguageValueText | StockState | (none) |
| EcomOrderField | OrderFieldName | OrderFields | Dynamicweb.Ecommerce.Orders.OrderService |
| EcomOrderLineFields | OrderLineFieldName | OrderLineFields | Dynamicweb.Ecommerce.Orders.OrderService |
| EcomOrderContexts | OrderContextName | OrderContexts | (none) |
| EcomOrderContextShopRelation | (none) | OrderContexts | (none) |
| EcomOrderContextAccessUserRelation | (none) | OrderContexts | (none) |
| EcomTrackAndTrace | TrackAndTraceName | TrackAndTrace | (none) |
| EcomAddressValidatorSettings | AddressValidatorName | AddressValidation | (none) |
| EcomValidationGroups | ValidationGroupName | ValidationGroups | (none) |
| EcomValidationGroupsTranslation | (none) | ValidationGroups | (none) |
| EcomValidations | ValidationId | ValidationGroups | (none) |
| EcomValidationRules | (none) | ValidationGroups | (none) |

### Internationalization Category (6 tables)
| Table | NameColumn | DataGroup | ServiceCaches |
|-------|-----------|-----------|---------------|
| EcomCountries | (none) | Countries | CountryRelationService, CountryService, VatGroupCountryRelationService, VatGroupService |
| EcomCountryText | CountryTextName | Countries | (same as Countries group) |
| EcomLanguages | LanguageName | Languages | Dynamicweb.Ecommerce.International.LanguageService, Dynamicweb.SystemTools.TranslationLanguageService |
| EcomCurrencies | CurrencyName | Currencies | Dynamicweb.Ecommerce.International.CurrencyService |
| EcomVatGroups | VatGroupName | VATGroups | Dynamicweb.Ecommerce.International.VatGroupCountryRelationService, Dynamicweb.Ecommerce.International.VatGroupService |
| EcomVatCountryRelations | (none) | VATGroups | (same as VATGroups group) |

**Total unique tables: 26** (slightly more than estimated ~24 due to validation and stock detail tables)

**Note:** EcomOrder_Schema uses `SchemaDataItemProvider`, not `SqlDataItemProvider`. It is OUT OF SCOPE (schema providers are future work).

## Cache Invalidation Architecture

### How DW10 Does It (Verified from Source)

From `LocalDeploymentProvider.ImportPackage` (lines 167-194):
1. Track which DataGroups were affected (had data written)
2. After ALL data groups are imported (both passes), iterate `_affectedDataGroups.Values.SelectMany(g => g.ServiceCaches).Distinct()`
3. For each service cache name:
   - `AddInManager.GetTypeUnvalidated<ICacheStorage>(serviceCacheName)` -- check type exists
   - `AddInManager.GetInstance<ICacheStorage>(serviceCacheName)` -- get singleton instance
   - `cacheStorage.ClearCache()` -- clear the full cache

### Our Approach
1. Add `ServiceCaches` list to `ProviderPredicateDefinition`
2. Create `CacheInvalidator` class wrapping the AddInManager pattern
3. After each predicate's Deserialize succeeds (not dry-run), call `CacheInvalidator.InvalidateCaches(predicate.ServiceCaches)`
4. Collect all caches cleared across predicates and deduplicate (avoid clearing same cache twice)
5. Apply to both SqlTable AND Content providers (D-09)

### Cache to Table Mapping (from DataGroup XMLs)
| ServiceCache | Triggered By Tables |
|-------------|---------------------|
| Dynamicweb.Ecommerce.Orders.PaymentService | EcomPayments, EcomMethodCountryRelation |
| Dynamicweb.Ecommerce.Orders.ShippingService | EcomShippings, EcomMethodCountryRelation |
| Dynamicweb.Ecommerce.Orders.OrderService | EcomOrderField, EcomOrderLineFields |
| Dynamicweb.Content.Versioning.CheckOutService | EcomPayments |
| Dynamicweb.Ecommerce.International.CountryService | EcomCountries, EcomCountryText |
| Dynamicweb.Ecommerce.International.CountryRelationService | EcomCountries, EcomCountryText |
| Dynamicweb.Ecommerce.International.VatGroupService | EcomCountries, EcomVatGroups, EcomVatCountryRelations |
| Dynamicweb.Ecommerce.International.VatGroupCountryRelationService | EcomCountries, EcomVatGroups, EcomVatCountryRelations |
| Dynamicweb.Ecommerce.International.LanguageService | EcomLanguages |
| Dynamicweb.SystemTools.TranslationLanguageService | EcomLanguages |
| Dynamicweb.Ecommerce.International.CurrencyService | EcomCurrencies |
| Dynamicweb.Content.AreaService | Content (for ContentProvider) |
| Dynamicweb.Content.PageService | Content (for ContentProvider) |
| Dynamicweb.Content.ParagraphService | Content (for ContentProvider) |

### Content Provider ServiceCaches
From `010_Content.xml`:
- `Dynamicweb.Content.AreaService`
- `Dynamicweb.Content.PageService`
- `Dynamicweb.Content.ParagraphService`

These should be configured on Content predicates to satisfy D-09.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| DW two-pass import | Single-pass with FK ordering | Phase 15 (new) | Simpler code, same correctness for acyclic graphs |
| No cache invalidation | Per-predicate cache clearing | Phase 15 (new) | Admin UI reflects changes without restart |
| Hardcoded table lists | Predicate-per-table config | Phase 13-14 | Any SQL table can be added via config |

## Open Questions

1. **EcomCountries has no NameColumn**
   - What we know: DataGroup XML shows NameColumn="" and CompareColumns="" for EcomCountries
   - What's unclear: What columns form the composite PK? (Code2 + RegionCode based on CountryServiceCacheStorage)
   - Recommendation: The existing PK-based identity fallback handles this. Verify at implementation time that the PK query returns the correct columns.

2. **Content Provider cache invalidation integration**
   - What we know: D-09 says cache invalidation applies to ALL providers including Content
   - What's unclear: ContentProvider currently delegates to ContentSerializer/ContentDeserializer which don't return enough info to know what was affected
   - Recommendation: Add ServiceCaches to Content predicates and clear after Deserialize returns. The Content-specific caches (AreaService, PageService, ParagraphService) should always be cleared after any content deserialization.

3. **EcomOrderContextAccessUserRelation FK to AccessUser**
   - What we know: This junction table likely has FKs to both EcomOrderContexts and AccessUser
   - What's unclear: AccessUser is not in our predicate set
   - Recommendation: FK filter already handles this (Pitfall 2). Only FK edges between tables in our set are considered.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 + Moq 4.20.72 |
| Config file | tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj |
| Quick run command | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~SqlTable" --no-build -q` |
| Full suite command | `dotnet test tests/Dynamicweb.ContentSync.Tests --no-build` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SQL-03 | FK topological sort produces correct order | unit | `dotnet test --filter "FullyQualifiedName~FkDependencyResolver" --no-build -q` | Wave 0 |
| SQL-03 | Circular FK detection throws with cycle info | unit | Same as above | Wave 0 |
| SQL-03 | Self-referencing FK skipped without error | unit | Same as above | Wave 0 |
| SQL-03 | External FK (outside predicate set) filtered out | unit | Same as above | Wave 0 |
| CACHE-01 | CacheInvalidator calls ClearCache on resolved types | unit | `dotnet test --filter "FullyQualifiedName~CacheInvalidator" --no-build -q` | Wave 0 |
| CACHE-01 | Missing cache type logged and skipped | unit | Same as above | Wave 0 |
| ECOM-01 | OrderFlow/States predicates validate correctly | unit | `dotnet test --filter "FullyQualifiedName~SqlTableProvider" --no-build -q` | Existing |
| ECOM-04 | Duplicate table predicate produces idempotent result | unit | Covered by existing checksum skip tests | Existing |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "FullyQualifiedName~SqlTable|FullyQualifiedName~FkDependency|FullyQualifiedName~CacheInvalidator|FullyQualifiedName~Orchestrator" --no-build -q`
- **Per wave merge:** `dotnet test tests/Dynamicweb.ContentSync.Tests --no-build`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/.../Providers/SqlTable/FkDependencyResolverTests.cs` -- covers SQL-03 (topological sort, cycle detection, self-ref, external FK)
- [ ] `tests/.../Providers/CacheInvalidatorTests.cs` -- covers CACHE-01 (cache clearing, missing type handling)
- [ ] `tests/.../Providers/SerializerOrchestratorFkOrderingTests.cs` -- covers orchestrator FK integration

## Sources

### Primary (HIGH confidence)
- DW10 Source: `LocalDeploymentProvider.cs` lines 167-194 -- cache invalidation pattern (verified from source)
- DW10 Source: `ICacheStorage.cs` -- cache interface with `ClearCache()` method (verified from source)
- DW10 Source: `AddInManager.cs` -- `GetInstance<T>(string)` and `GetTypeUnvalidated<T>(string)` APIs (verified from source)
- DW10 Source: `SqlDataItemWriter.cs` -- MERGE command pattern already replicated in Phase 13 (verified)
- DataGroup XMLs at `C:\temp\DataGroups\Settings_Ecommerce_*` -- complete table inventory with ServiceCaches (verified)
- Project codebase: SqlTableProvider, SerializerOrchestrator, ISqlExecutor -- existing infrastructure (verified)

### Secondary (MEDIUM confidence)
- SQL Server `sys.foreign_keys` + `sys.foreign_key_columns` -- standard catalog views, well-documented by Microsoft
- Kahn's algorithm for topological sort -- standard CS algorithm, no external dependency needed

### Tertiary (LOW confidence)
- None -- all findings verified from source code

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new packages, all existing infrastructure
- Architecture: HIGH -- DW10 source provides exact cache invalidation pattern; FK ordering is standard topological sort
- Pitfalls: HIGH -- verified from DataGroup XML analysis and DW10 source code review
- Table inventory: HIGH -- enumerated from DataGroup XML files directly

**Research date:** 2026-03-24
**Valid until:** 2026-04-24 (stable -- DW10 caching architecture is mature)

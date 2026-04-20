---
phase: 37-production-ready-baseline
plan: 02
subsystem: infrastructure
tags: [schema-tolerance, type-coercion, refactor, deduplication]

# Dependency graph
requires:
  - phase: 37-01
    provides: DeploymentMode / ConflictStrategy + unchanged Area schema-tolerance baseline (commit f0bfbba) that this plan consolidates
provides:
  - TargetSchemaCache infrastructure class — unified per-run INFORMATION_SCHEMA cache + YAML→.NET type coercion shared across ContentDeserializer (Area writes) and SqlTableProvider (MERGE writes)
  - Single authoritative coercion path covering datetime / datetime2 / smalldatetime / date / datetimeoffset / bit / int / smallint / tinyint / bigint / decimal / numeric / money / smallmoney / float / real / uniqueidentifier / varbinary (+ int→long auto-widening for bigint)
  - LogMissingColumnOnce warning dedupe per (table, column) across rows
affects: [37-03 SqlTable exclusion curation builds on the cleaner SqlTableProvider.Deserialize path; any future raw-SQL write paths now have a drop-in helper]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Injected schema loader (Func<string, (HashSet<string>, Dictionary<string,string>)>) lets tests exercise Coerce without a live DB while production defaults to INFORMATION_SCHEMA via CommandBuilder with a parameterized TABLE_NAME (mitigates T-37-02-01)"
    - "DBNull.Value normalization at the TargetSchemaCache boundary — callers that want nullable row dictionaries re-map DBNull.Value to null after Coerce (preserves SqlTableProvider row-dictionary contract)"
    - "Provider-level shared cache — one TargetSchemaCache per ProviderRegistry.CreateDefault() call, threaded into SqlTableProvider so every SqlTable predicate in the same run coalesces to one INFORMATION_SCHEMA query per distinct table"

key-files:
  created:
    - src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs
    - tests/DynamicWeb.Serializer.Tests/Infrastructure/TargetSchemaCacheTests.cs
    - tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerAreaSchemaTests.cs
    - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderCoercionTests.cs
  modified:
    - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
    - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs
    - src/DynamicWeb.Serializer/Providers/ProviderRegistry.cs
    - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs

decisions:
  - "DataGroupMetadataReader kept as-is (did NOT route its GetColumnTypes through TargetSchemaCache). Reason: smallest diff — DGMR has separate concerns (GetNotNullColumns, TableExists, GetTableMetadata + column definitions) that TargetSchemaCache does not cover, and the one remaining GetColumnTypes call from SqlTableProvider is now a fallback for when TargetSchemaCache returns an empty type map (fixture loaders in unit tests). Perf cost of the occasional parallel query is negligible — the shared cache still cuts the common production path to one query per table."
  - "One TargetSchemaCache per ProviderRegistry, NOT threaded into ContentProvider→ContentDeserializer. Reason: ContentDeserializer only queries the Area table; SqlTableProvider queries all SqlTable predicates. There is zero overlap between the tables the two caches touch, so sharing a single instance produces no query coalescing in practice while adding plumbing through ContentProvider.BuildSerializerConfiguration. Cost/benefit favored the smaller diff."
  - "Empty-string-on-string-column returns the empty string (not DBNull.Value). Rationale: ContentDeserializer.CoerceForColumn (f0bfbba) preserved empty strings on nvarchar via its no-op default branch; SqlTableProvider.CoerceRowTypes also preserved empties on string types. Matching both keeps behavior identical."
  - "DBNull.Value normalization to null post-Coerce in SqlTableProvider. TargetSchemaCache returns DBNull.Value for null/DBNull/empty-non-string inputs (symmetric with how Dynamicweb's CommandBuilder consumes it). But SqlTableProvider's row dictionary contract uses null, not DBNull. Rather than split the cache API, the caller re-maps on the way out — one line, no behavioral diff."

metrics:
  duration: ~13 minutes
  completed: 2026-04-20
---

# Phase 37 Plan 37-02: Schema Tolerance + Coercion Consolidation Summary

Unified ContentDeserializer's Area-only schema-tolerance (commit f0bfbba) with SqlTableProvider's `CoerceRowTypes`/`IsStringType` into a single `TargetSchemaCache` infrastructure class, then rewired every raw-SQL write path through it — eliminating ~130 lines of duplicated coercion + schema-filter logic without changing observable behavior.

## What changed

### New infrastructure: `TargetSchemaCache`

`src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs` — one instance per run owns:

1. **Column discovery** — lazy `SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0}` via `CommandBuilder` parameter binding (tableName is parameterized, not interpolated — threat T-37-02-01 mitigated).
2. **Type coercion** — `Coerce(tableName, columnName, value)` covers the union of previously-duplicated types: `datetime / datetime2 / smalldatetime / date` → `DateTime` (AssumeUniversal + AdjustToUniversal); `datetimeoffset` → `DateTimeOffset` (RoundtripKind); `bit` accepts `true/false/0/1`; integer family (`int/smallint/tinyint`) → `int`; `bigint` → `long` (plus int→long auto-widening for already-typed values); `decimal/numeric/money/smallmoney` → `decimal`; `float` → `double`; `real` → `float`; `uniqueidentifier` → `Guid`; `varbinary/binary/image` → base64 round-trip.
3. **Logging dedup** — `LogMissingColumnOnce(table, column, log)` returns true exactly once per `(table, column)` pair, preventing log spam when dozens of rows share an unknown column.

Test-only constructor takes a `Func<string, (HashSet<string>, Dictionary<string, string>)>` loader so coercion can be exercised without touching a live DB.

### Call-site rewiring

**`ContentDeserializer.WriteAreaProperties` / `CreateAreaFromProperties`** (`src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs`, lines 365/409):

- Removed fields: `_targetAreaColumns`, `_targetAreaColumnTypes`, `_loggedAreaColumnMissing`
- Removed methods: `GetTargetAreaColumns`, `EnsureTargetAreaSchema`, `CoerceForColumn` (~100 LOC)
- Added: optional `TargetSchemaCache? schemaCache = null` ctor parameter (default: fresh instance)
- Inline filter + coerce + log-once all delegate to `_schemaCache`

**`SqlTableProvider.Deserialize`** (`src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs`, line 168):

- Removed: `private static CoerceRowTypes(row, columnTypes)` + `private static IsStringType(sqlType)` (~60 LOC)
- Added: optional `TargetSchemaCache? schemaCache = null` ctor parameter
- Per-row loop now: filter keys via `targetCols = _schemaCache.GetColumns(metadata.TableName)`, coerce remaining via `_schemaCache.Coerce(...)`, DBNull-normalize back to null, then `FixNotNullDefaults` (unchanged)
- `DataGroupMetadataReader.GetNotNullColumns` still called directly — that concern stays separate per the planner's "minimal diff" guidance

**`ProviderRegistry.CreateDefault`** (`src/DynamicWeb.Serializer/Providers/ProviderRegistry.cs`, line ~130):

- Constructs `var schemaCache = new TargetSchemaCache();` once and passes it to `new SqlTableProvider(..., schemaCache)`
- One INFORMATION_SCHEMA query per distinct table across a whole orchestrator run

## Call sites that now touch `_schemaCache`

| File | Line | Call |
| ---- | ---- | ---- |
| `ContentDeserializer.cs` | 365 | `_schemaCache.GetColumns("Area")` (WriteAreaProperties) |
| `ContentDeserializer.cs` | 378 | `_schemaCache.LogMissingColumnOnce("Area", kvp.Key, _log)` |
| `ContentDeserializer.cs` | 382 | `_schemaCache.Coerce("Area", kvp.Key, kvp.Value)` |
| `ContentDeserializer.cs` | 409 | `_schemaCache.GetColumns("Area")` (CreateAreaFromProperties) |
| `ContentDeserializer.cs` | 415 | `_schemaCache.LogMissingColumnOnce("Area", kvp.Key, _log)` |
| `ContentDeserializer.cs` | 419 | `_schemaCache.Coerce("Area", kvp.Key, kvp.Value)` |
| `SqlTableProvider.cs` | 168 | `_schemaCache.GetColumns(metadata.TableName)` |
| `SqlTableProvider.cs` | 169 | `_schemaCache.GetColumnTypes(metadata.TableName)` |
| `SqlTableProvider.cs` | 185 | `_schemaCache.LogMissingColumnOnce(metadata.TableName, k, log)` |
| `SqlTableProvider.cs` | 191 | `_schemaCache.Coerce(metadata.TableName, col, row[col])` |

## DataGroupMetadataReader decision

**Kept parallel, not routed through `TargetSchemaCache`.** `GetNotNullColumns`, `TableExists`, `GetTableMetadata`, `QueryColumnDefinitions` all remain on `DataGroupMetadataReader` unchanged. `GetColumnTypes` is still present (SqlTableProvider falls back to it when the shared cache returns an empty map — happens under fixture loaders in unit tests). The two classes have distinct responsibilities — `TargetSchemaCache` owns lightweight per-run column/type lookup + coercion; `DataGroupMetadataReader` owns richer metadata needed for `CreateTableFromMetadata`. Merging them would have been a strictly larger diff with no runtime benefit.

## Coverage

- **42** new unit tests (27 TargetSchemaCache + 7 ContentDeserializer area schema + 2 SqlTable coercion contract + 6 legacy-regression touch-ups on SqlTableProviderDeserializeTests that now inject a fixture cache)
- **470/470** unit tests passing (baseline 435 → +35 net after accounting for 0 regressions). All SqlTableProviderDeserializeTests pass after injection fix.
- Contract test `Deserialize_SourceHasExtraColumn_MissingOnTarget_WarnsOnce_And_StripsColumn` validates the must-have behavior from `truths[5]`: row with `{ExistingCol: <coerced>, MissingCol: <stripped>}` + single WARNING log per unknown column.
- Contract test `Deserialize_CoercionDelegated_StringDateTime_BecomesDateTimeInRow` validates end-to-end that a string datetime YAML value arrives at `SqlTableWriter.WriteRow` as a `DateTime`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocker] Legacy SqlTableProviderDeserializeTests broken by new ctor default**

- **Found during:** Task 2 GREEN run
- **Issue:** Five existing tests (`Deserialize_CreatesNewRows`, `Deserialize_DryRun_NoSqlWrites`, `Deserialize_SkipsUnchangedRows`, `Deserialize_UpdatesChangedRows`, `Deserialize_ReportsAccurateCounts`) failed with `Dynamicweb.Extensibility.Dependencies.DependencyResolverException : The Dependency Locator was not initialized properly.` because the new default ctor wired a production `TargetSchemaCache()` that tried to hit the live DB via `Database.CreateDataReader` in unit-test process space.
- **Fix:** Updated `CreateProviderWithFiles` helper to construct a fixture-backed `TargetSchemaCache` (returns the test metadata's AllColumns + empty type map). This mirrors the pattern in the new `SqlTableProviderCoercionTests`.
- **Files modified:** `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs`
- **Commit:** `7b33469` (GREEN commit includes the fix since the legacy tests and the wiring are inseparable from one commit scope)

### Threat Mitigations Applied

**T-37-02-01 (SQL injection via tableName in DefaultLoader):** `TargetSchemaCache.DefaultLoader` uses `cb.Add("SELECT ... TABLE_NAME = {0}", tableName)` — `CommandBuilder` parameterizes `{0}` rather than string-interpolating it, so malicious table names never reach the SQL parser as code.

**T-37-02-04 (Column filter bypass):** Verified by `Deserialize_SourceHasExtraColumn_MissingOnTarget_WarnsOnce_And_StripsColumn` — the row that reaches `SqlTableWriter.WriteRow` provably has the unknown column stripped.

## Bonus gaps left for future work

- **`SqlTableWriter.CreateTableFromMetadata`** (DDL path for tables missing on target) does NOT go through `TargetSchemaCache` — but it only runs when the target table doesn't exist, so schema-drift tolerance is not applicable. Left as-is.
- **`SqlTableProvider` still calls `_metadataReader.GetColumnTypes` as a fallback** when `_schemaCache.GetColumnTypes` returns empty (test fixtures that inject empty type maps). In production, the shared cache always has data because `targetCols` came from the same load. A future tidy-up could drop the fallback, but it's defensive and costs nothing.
- **ContentProvider → ContentDeserializer cache threading** — currently each `ContentDeserializer` instance constructs its own `TargetSchemaCache`. For the Area-only workload this makes zero difference (one Area query per predicate run, and each predicate is typically a different area). If a future plan moves additional tables into ContentDeserializer's direct raw-SQL path, revisit.

## TDD Gate Compliance

- RED gate (test-only): `2a1ba48 test(37-02): add failing tests for TargetSchemaCache`
- GREEN gate (impl): `9f32021 feat(37-02): add TargetSchemaCache infrastructure class`
- RED gate (test-only): `4ae5e37 test(37-02): add failing tests for TargetSchemaCache wiring`
- GREEN gate (impl): `7b33469 feat(37-02): route Area and SqlTable writes through TargetSchemaCache`

All four gates present in `git log --oneline -6`. No refactor commits were needed — the consolidation IS the refactor and it lived entirely in the GREEN phase of Task 2.

## Self-Check: PASSED

- Files exist:
  - `src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Infrastructure/TargetSchemaCacheTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerAreaSchemaTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderCoercionTests.cs` ✓
- Commits present in `git log --oneline`:
  - `2a1ba48` ✓
  - `9f32021` ✓
  - `4ae5e37` ✓
  - `7b33469` ✓
- Legacy helpers removed (grep returns no matches):
  - `GetTargetAreaColumns|EnsureTargetAreaSchema|CoerceForColumn` ✓ (only docstring reference in TargetSchemaCache.cs)
  - `_loggedAreaColumnMissing|_targetAreaColumns|_targetAreaColumnTypes` ✓
  - `private static void CoerceRowTypes|private static bool IsStringType` ✓
- Build: 0 errors
- Tests: 470 passed / 0 failed

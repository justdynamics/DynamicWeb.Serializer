# Phase 39: Seed mode field-level merge — Research

**Researched:** 2026-04-22
**Domain:** DW content deserialization semantics (ContentDeserializer + SqlTableProvider) — moving Seed mode from row/page-level skip to field-level merge
**Confidence:** HIGH (all critical claims verified against actual source at specified line ranges)

## Summary

The current Seed-mode implementation (Phase 37-01) uses a 6-line whole-entity skip in both
providers. `ContentDeserializer.DeserializePage` at lines 687–692 short-circuits the UPDATE
path with `ctx.Skipped++; return existingId` whenever the page GUID already exists on
target. `SqlTableProvider.DeserializeCoreLogic` at lines 316–322 does the equivalent via the
`existingChecksums` dict. The phase's job is to replace each skip block with a per-field
merge branch that honors `IsUnsetForMerge` (NULL-or-type-default on target = fillable).

The shape of the work is asymmetric across the two providers. On the Content side, the
merge predicate wraps an existing source-wins write path: every scalar setter on `Page`,
every call to `SaveItemFields` / `SavePropertyItemFields`, every sub-object DTO assignment.
The path is already there; the merge branch needs a live-read of the current target, a
per-field predicate, and a narrower apply step. On the SqlTable side, there is no
narrowed UPDATE — `SqlTableWriter` has exactly two write shapes: `BuildMergeCommand` (the
full MERGE) and `TruncateAndInsertAll` (no-PK fallback). A new `UpdateColumnSubset`
method is needed, sitting alongside `WriteRow`.

**Primary recommendation:** Two plans, one per provider, TDD-driven. Plan 39-01 lands the
shared `IsUnsetForMerge` helper (in `Infrastructure/` next to `TargetSchemaCache`) with
full unit coverage, then retrofits `ContentDeserializer`. Plan 39-02 extends
`SqlTableWriter` with `UpdateColumnSubset` + the new Seed branch in `SqlTableProvider`.
Optional Plan 39-03 extends `tools/e2e/full-clean-roundtrip.ps1` with a Deploy → tweak →
Seed sub-pipeline for the D-15 live gate.

## User Constraints (from CONTEXT.md)

### Locked Decisions

**Unset Detection**
- D-01 (baseline unset rule): NULL OR type default. Defaults: `""` (strings), `0`
  (int/decimal), `false` (bool), `DateTime.MinValue`, `Guid.Empty`, empty JSON for JSON cols.
- D-02 (ItemFields are strings): DW persists every ItemField as string via `ItemService`.
  Compare at string layer — fill when target is `null` or `""`. Specialization of D-01.
- D-03 (PropertyItem fields): Treat identically to ItemFields.
- D-04 (sub-object DTOs): Seo, UrlSettings, NavigationSettings, Visibility NOT atomic.
  Apply unset rule per-property inside each sub-object.

**Merge Scope on Content**
- D-05 (scalar scope): All non-identity scalars on Page merge. Always source-wins (never
  merged): `PageUniqueId`, `AreaId`, `ParentPageId`. Everything else — MenuText, UrlName,
  Active, Sort, ItemType, LayoutTemplate, LayoutApplyToSubPages, IsFolder, TreeSection,
  all ~30 Phase-23 properties — goes through unset rule.
- D-06 (permissions skipped): Seed never touches existing page permissions.
  `_permissionMapper.ApplyPermissions` bypassed on Seed UPDATE path.
- D-07 (recurse into children): When parent page exists, walk gridrows → columns →
  paragraphs and apply merge rule. Matched entities (by UniqueId) merge; unmatched are created.
- D-08 (shared helper, separate write paths): One `IsUnsetForMerge(value, typeHint)`
  helper. Each provider keeps its own write path.

**Re-run Idempotency**
- D-09 (no persisted marker): Idempotency is intrinsic. Read current target value, apply
  unset rule. No seeded_fields table.
- D-10 (type-default overwrite accepted): Customer who explicitly set Active=false or
  int=0 loses that on next Seed. Deliberate tradeoff for D-01's broader fill rule.
- D-11 (new log format): Replace `"Seed-skip: [identity] (already present)"` with
  `"Seed-merge: [identity] — N fields filled, M left (already set)"`. `Skipped` counter
  repurposed to mean "all fields already set, no writes issued".
- D-12 (schema drift inheritance): Missing target columns silently drop. Inherits
  TargetSchemaCache behavior.

**SqlTableProvider Write Mechanism**
- D-17 (read-then-narrowed-UPDATE): For each Seed row with identity match: SELECT current
  row, compute column subset where target is unset, issue targeted
  `UPDATE [table] SET col1=@p1, col2=@p2, ... WHERE [identity_predicate]`. Rows without
  identity match still flow through `_writer.WriteRow` (MERGE insert).
- D-18 (checksum-skip stays first): `existingChecksums` unchanged-skip path runs BEFORE
  field-level merge.

**Test Strategy**
- D-13 (test shape): Unit tests on shared helper covering every type in D-01. Integration
  tests per provider covering acceptance scenario.
- D-14 (TDD discipline): Two plans (Plan 39-01 Content; Plan 39-02 SqlTable). Shared helper
  lands in 39-01. Optional Plan 39-03 for live E2E gate.
- D-15 (live E2E required): Closure gate — live Swift 2.2 → CleanDB round-trip under
  strictMode: true extending `tools/e2e/full-clean-roundtrip.ps1` with Deploy → tweak →
  Seed sub-pipeline. Assert Mail1SenderEmail + branding populated; manual tweaks preserved.
- D-16 (Phase 37 D-06 treatment): Leave 37-CONTEXT.md untouched. Phase 39 supersedes
  explicitly via `supersedes:` frontmatter.

**Observability**
- D-19 (dry-run per-field diff): Dry-run prints per-field entries for every would-be
  fill: `"  would fill [col=MetaTitle]: target=NULL → seed='DW Swift 2.2'"`.

**Scope Guardrail**
- D-20 (no admin UI changes): Phase 39 is purely deserialization-behavior. No admin UI
  changes.

### Claude's Discretion

- Exact shape of `IsUnsetForMerge(value, typeHint)` signature — overloads per type, object +
  Type, small visitor. Planner picks C# idiom.
- Exact SQL for narrowed UPDATE (parameterized vs inlined column list, batching across
  rows) — planner picks what fits SqlTableWriter's existing shape.
- Whether shared helper lives in `Configuration/`, `Providers/`, or new `Merge/`
  namespace — planner chooses based on assembly layering.
- Phrasing/format of new log lines, as long as D-11 / D-19 content is present.

### Deferred Ideas (OUT OF SCOPE)

- Admin UI "Preview Seed merge" action — own phase.
- Log-viewer highlight for seed-merge lines — UI polish phase.
- Per-field seeded marker (rejected via D-09) — later milestone if customers report
  confusion.
- PROJECT.md Key Decisions table update — at milestone transitions, not in 39.

## Project Constraints (from CLAUDE.md)

No `./CLAUDE.md` file at repo root. Project-specific constraints live in
`memory/feedback_*.md` and `memory/project_*.md` (MEMORY.md index). Relevant items for
this phase:

- **No backcompat, no historical thoughtfulness** (`feedback_no_backcompat.md`): OOTB
  baseline is the goal. Safe to delete the Seed-skip code; no shim needed for the old
  `"Seed-skip:"` log line.
- **Content tables MUST use DW APIs** (`feedback_content_not_sql.md`): All page/paragraph
  writes go through `Services.Pages.SavePage` / `Services.Paragraphs.SaveParagraph` /
  `ItemService.Save`, not raw SQL. This is already how `ContentDeserializer` works —
  Phase 39 does not change it.
- **DW admin UI patterns** (`feedback_dw_patterns.md`): Not applicable — D-20 prohibits
  UI changes.
- **Swift 2.2 combined baseline is canonical** (`project_swift22_baseline_combined.md`):
  `swift2.2-combined.json` is the post-Phase-37 baseline. Phase 39 makes it correct for
  Seed re-runs.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Unset-detection predicate | Shared helper (Infrastructure) | — | Stateless pure function; D-08 mandates single helper across providers |
| Live-read of target page scalars | DW entity layer (`Services.Pages.GetPage`) | — | Already how existing UPDATE path loads `existingPage` at line 677 |
| Live-read of target item fields | DW ItemService (`ItemEntry.SerializeTo(dict)`) | — | Existing serialize path (`ContentMapper.cs:45`) proves the pattern |
| Live-read of target SQL row | `SqlTableReader.ReadAllRows` (WHERE identity) or parameterized SELECT in writer | — | Existing checksum path already reads all rows once (line 292) — may be reused or scoped |
| Narrowed UPDATE | `SqlTableWriter.UpdateColumnSubset` (new) | — | No UPDATE path exists today; MERGE is the only write |
| Seed-merge orchestration (Content) | `ContentDeserializer.DeserializePage` + recursion into GridRow/Paragraph | `PermissionMapper` (bypassed per D-06) | UPDATE path already exists — merge branch wraps it with predicate |
| Seed-merge orchestration (SqlTable) | `SqlTableProvider.DeserializeCoreLogic` | `SqlTableWriter` (new method) | Existing skip block at 313–322 becomes merge-plan construction |
| Observability (log lines + counters) | ContentDeserializer + SqlTableProvider | `ProviderDeserializeResult.Skipped` (repurposed per D-11) | No new counter field; semantic change only |

**Boundary check:** Seed-merge does NOT leak into Serialize paths. Serialization is
source-of-truth → YAML; merge is purely a deserialize-time concern. The `ConflictStrategy`
enum is already threaded correctly through both provider entry points.

## Phase Requirements

None mapped in ROADMAP (phase has `Requirements: TBD`). Treat CONTEXT.md D-01..D-20
decisions as authoritative acceptance. The plan-checker should verify every decision has a
corresponding task or explicit non-action in the plans.

| Decision ID | Research Support |
|-------------|------------------|
| D-01/D-02/D-03 | `IsUnsetForMerge` helper design — see Shared Helper Design Space |
| D-04 | Sub-object DTO breakdown — see Current Code Map |
| D-05 | Page scalar inventory — see Page Scalars to Merge |
| D-06 | PermissionMapper bypass — trivial gate in UPDATE path |
| D-07 | GridRow/Paragraph UPDATE paths already exist at lines 855–910 / 1023–1069 |
| D-08 | Helper location — see Recommended Structure |
| D-11 | Log format + counter semantics — no new type needed |
| D-17 | New `UpdateColumnSubset` method — see Narrowed-UPDATE Shape |
| D-18 | Preserve lines 304–311 checksum fast-path in SqlTableProvider |
| D-19 | Extend `LogDryRunPageUpdate` pattern at line 1428 |

## Standard Stack

### Core — No new packages

Phase 39 is a pure in-process refactor of existing code. No new NuGet dependencies.

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Dynamicweb.Content.Services.Pages` | (hosted DW version) | Page entity load/save | Already the source-wins write path [VERIFIED: lines 640, 677, 715] |
| `Dynamicweb.Content.Items.Services` | (hosted DW version) | ItemEntry CRUD + `SerializeTo(dict)` live-read | Same pattern used by serialize path in ContentMapper.cs:45 [VERIFIED] |
| `Dynamicweb.Data.CommandBuilder` | (hosted DW version) | Parameterized SQL for new UPDATE | Same builder used by existing `BuildMergeCommand` [VERIFIED: SqlTableWriter.cs:60] |
| `xUnit` | (existing test project) | Unit + integration tests | Project-wide test framework [VERIFIED: 3+ test files use `using Xunit`] |
| `Moq` | (existing test project) | ISqlExecutor mock for writer tests | Existing pattern in SqlTableProviderDeserializeTests [VERIFIED: tests/.../SqlTableProviderDeserializeTests.cs:69] |

### Supporting — Reused from existing codebase

| Component | File | Purpose | Why Standard |
|-----------|------|---------|--------------|
| `TargetSchemaCache` | `src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs` | Schema-drift tolerance for D-12 | Already reused by Area + SqlTable paths [VERIFIED] |
| `ConflictStrategy` enum | `src/DynamicWeb.Serializer/Configuration/ConflictStrategy.cs` | Semantics only change, enum stays | [VERIFIED: D-16 in CONTEXT] |
| `ItemEntry.SerializeTo(dict)` | DW API | Dumps all item fields as strings into dict | [VERIFIED: ContentMapper.cs lines 45, 167, 396] |
| `Services.Pages.GetPage(id)` | DW API | Returns Page entity with current DB state | [VERIFIED: ContentDeserializer.cs:677] |
| `ProviderDeserializeResult.Skipped` | `src/DynamicWeb.Serializer/Providers/ProviderDeserializeResult.cs` | Counter semantics change per D-11 | Existing int field, no shape change [VERIFIED] |
| `CommandBuilder` placeholder syntax `{0}` | DW Data | SQL injection–safe parameter binding | [VERIFIED: SqlTableWriter.cs:83, TargetSchemaCache.cs:39] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `IsUnsetForMerge(object?, Type)` | Overloads per-type (`IsUnset(string?)`, `IsUnset(int)`, etc.) | Overloads are clearer at call site but require caller to know concrete type at compile time; dictionary-of-object values (Fields, YAML row) force the object+Type shape. **Recommendation: object+Type overload for hot path, thin per-type overloads for clarity at Page scalar sites.** |
| New `UpdateColumnSubset` SQL | Build a MERGE with a dynamic UPDATE column list | Existing BuildMergeCommand always writes all mapped columns on `WHEN MATCHED`; hacking it to conditionally include would balloon complexity. **Recommendation: separate targeted UPDATE method — simpler, no INSERT branch needed since identity-match is pre-confirmed.** |
| SELECT per-row inside merge | Reuse the `existingChecksums`-era full-table read and project column values into a dict | Full-table read is already in memory at line 292; threading it further avoids a second round-trip. **Recommendation: reuse the same row-enumeration pass — cache `(identity → full row dict)` alongside `(identity → checksum)`.** |
| New test file per provider | Add Seed-merge cases to existing SqlTableProviderDeserializeTests / new ContentDeserializerSeedMergeTests | Existing SqlTable test file has the infrastructure (FakeSqlExecutor + fixture builders); a new file for merge keeps cases isolated. **Recommendation: new test files per plan — `IsUnsetForMergeTests.cs`, `SqlTableProviderSeedMergeTests.cs`, `ContentDeserializerSeedMergeTests.cs`.** |

**Installation:** None.

**Version verification:** Not applicable — no new packages.

## Current Code Map

### ContentDeserializer.cs (exact line ranges verified)

| Lines | Block | What It Does |
|-------|-------|-------------|
| 50–76 | Ctor | Threads `ConflictStrategy conflictStrategy = SourceWins`; stores in `_conflictStrategy` field |
| 84–99 | `WriteContext` | Carries `Created/Updated/Skipped/Failed` counters, `ExcludeFields`, `ExcludeFieldsByItemType` |
| 605–672 | `DeserializePage` INSERT path | Creates new Page, `SavePage`, `SaveItemFields`, `SavePropertyItemFields`, `ApplyPermissions`. Untouched by Phase 39. |
| **684–692** | **Seed-skip (REPLACE)** | 6 lines: if DestinationWins, log "Seed-skip:", `Skipped++`, return existingId |
| 694–698 | Dry-run UPDATE branch | Calls `LogDryRunPageUpdate(dto, existingPage, ctx)`. Dry-run merge branch needs wrapping per D-19. |
| 700–715 | Scalar assignment (source-wins) | Sets PageUniqueId, AreaId, ParentPageId, MenuText, UrlName, Active, Sort, ItemType, LayoutTemplate, LayoutApplyToSubPages, IsFolder, TreeSection, then `ApplyPageProperties(existingPage, dto)`. |
| 716 | `Services.Pages.SavePage(existingPage)` | Persists scalars |
| 717–724 | `SaveItemFields` call | Source-wins null-out of missing fields |
| 727 | `SavePropertyItemFields` call | Same pattern |
| 729–732 | UPDATE epilog | `Updated++`, log, `_permissionMapper.ApplyPermissions` (D-06 bypass target) |
| 855–910 | `DeserializeGridRow` UPDATE | GridRow scalar assignment + `ApplyGridRowVisualProperties` + `SaveItemFields`. Same merge shape. |
| 1023–1069 | `DeserializeParagraph` UPDATE | Paragraph scalar assignment + `SaveItemFields`. |
| 1075–1114 | `SavePropertyItemFields` | Loops `propItem.Names`, sets missing → null. Merge needs to skip the null-out AND the overwrite step, per-field. |
| 1138–1187 | `SaveItemFields` | Same shape as `SavePropertyItemFields` — loops `itemEntry.Names`, null-outs missing, calls `itemEntry.DeserializeFrom(contentFields)`. |
| 1193–1268 | `ApplyPageProperties` | Assigns ~30 Phase-23 properties including Seo/UrlSettings/Visibility/NavigationSettings sub-objects (D-04). |
| 1428–1496 | `LogDryRunPageUpdate` | Existing per-field diff template. D-19 extends this. |
| 1498–1533 | `LogDryRunParagraphUpdate` | Same for paragraphs. |

### SqlTableProvider.cs (exact line ranges verified)

| Lines | Block | What It Does |
|-------|-------|-------------|
| 147–158 | `Deserialize` entry | Accepts `strategy: ConflictStrategy` parameter |
| 196–244 | Schema prep | TargetSchemaCache coerce, FixNotNullDefaults, CompactXmlColumns, ApplyLinkResolution |
| 262–287 | No-PK table branch | TruncateAndInsertAll — Seed-merge DOES NOT APPLY to no-PK tables (rows have no identity to match). Skip by existing path. |
| 289–297 | `existingChecksums` lookup build | Reads all existing target rows, computes identity + checksum, stores in dict. **KEEP FOR D-17 REUSE.** |
| 299–311 | Yaml row loop + checksum fast-path | **D-18: PRESERVE.** If checksums match, skip — "unchanged" — no merge work needed. |
| **313–322** | **Seed-skip (REPLACE)** | 10 lines: if DestinationWins AND identity in existingChecksums, `skipped++`, log, continue |
| 324–340 | `_writer.WriteRow` + outcome dispatch | Source-wins fallback. Merge path still hits this for identity-unmatched rows (D-17). |

### SqlTableWriter.cs (exact line ranges verified)

| Lines | Block | What It Does |
|-------|-------|-------------|
| 35–123 | `BuildMergeCommand` | Full MERGE with WHEN MATCHED UPDATE + WHEN NOT MATCHED INSERT. Writes ALL mapped columns. |
| 129–164 | `WriteRow` | Wraps BuildMergeCommand with dry-run short-circuit |
| 175–191 | `ApplyLinkResolution` | Rewrites Default.aspx?ID= — pre-merge transform, not a write path |
| 201–236 | `CreateTableFromMetadata` | DDL only |
| 263–306 | `TruncateAndInsertAll` | No-PK fallback |
| 312–327 | `DisableForeignKeys` / `EnableForeignKeys` | FK management |
| 329–360 | `RowExistsInTarget` | SELECT 1 on keyCols — existence check only, no full row read |

**No narrowed-UPDATE path exists.** `UpdateColumnSubset` is net-new.

## Merge Branch Shape — Pseudo-code

### ContentDeserializer (replaces lines 684–692)

```csharp
// Seed mode (D-01..D-07, D-11, D-19): page exists on target — merge per-field.
if (_conflictStrategy == ConflictStrategy.DestinationWins)
{
    if (_isDryRun)
    {
        LogSeedMergeDryRun(dto, existingPage, ctx);  // D-19: per-field diff
        return existingId;
    }

    int filled = 0, left = 0;

    // 1. Scalars (D-05) — excluding PageUniqueId/AreaId/ParentPageId
    //    For each non-identity scalar property on Page:
    //      if IsUnsetForMerge(existingPage.<prop>, typeof(T)) → set, filled++ else left++
    filled += MergeScalars(existingPage, dto, ref left);

    // 2. Sub-object DTOs (D-04) — per-property application
    //    MergeSubObject(existingPage, dto.Seo, MetaTitle/Description/...)
    //    MergeSubObject(existingPage, dto.UrlSettings, ...)
    //    MergeSubObject(existingPage, dto.Visibility, ...)
    //    MergeSubObject(existingPage, dto.NavigationSettings, ...) when UseEcomGroups=true
    filled += MergeSubObjects(existingPage, dto, ref left);

    Services.Pages.SavePage(existingPage);  // writes only the scalars we flipped

    // 3. ItemFields (D-02) via per-field string compare
    //    itemEntry.SerializeTo(currentDict); then:
    //      for (k,v) in dto.Fields: if IsUnsetForMergeString(currentDict.GetValueOrDefault(k)) → write
    filled += MergeItemFields(existingPage.ItemType, existingPage.ItemId, dto.Fields, updatePageExclude, ref left);

    // 4. PropertyItem fields (D-03) — same pattern
    filled += MergePropertyItemFields(existingPage, dto.PropertyFields, updatePageExclude, ref left);

    // 5. D-06: SKIP _permissionMapper.ApplyPermissions — no-op for Seed
    // Note: no `ApplyPermissions` call here.

    // 6. Counter + log (D-11)
    if (filled == 0) ctx.Skipped++;
    else ctx.Updated++;
    Log($"Seed-merge: page {dto.PageUniqueId} (ID={existingId}) — {filled} filled, {left} left");

    // 7. D-07: recursion into gridrows/columns/paragraphs still runs below
    //    (existing code path — DeserializeGridRowSafe etc. — just inherits _conflictStrategy)
    return existingId;
}

// Source-wins fallthrough (existing lines 694–732, untouched)
```

### SqlTableProvider (replaces lines 313–322)

```csharp
// Seed mode (D-17, D-18, D-11): identity matches on target — merge per-column.
// D-18: checksum fast-path at 304–311 already ran — if we're here, checksums differ.
if (strategy == ConflictStrategy.DestinationWins
    && existingChecksums.ContainsKey(identity))
{
    // Look up current target row (from the same scan that built existingChecksums —
    // see Reuse Opportunity below for performance).
    var currentRow = existingRowsByIdentity[identity];

    // Compute merge plan: columns where target is unset per D-01
    var mergeColumns = new Dictionary<string, object?>();
    foreach (var (col, yamlValue) in yamlRow)
    {
        if (keyCols.Contains(col, OrdinalIgnoreCase)) continue;
        if (identityCols.Contains(col, OrdinalIgnoreCase)) continue;
        if (!currentRow.TryGetValue(col, out var targetValue))
            continue;  // D-12: column not on target, silently drop
        var sqlType = columnTypes.GetValueOrDefault(col);
        if (IsUnsetForMerge(targetValue, sqlType))
            mergeColumns[col] = yamlValue;
    }

    if (mergeColumns.Count == 0)
    {
        skipped++;
        Log($"  Seed-merge: [{metadata.TableName}].{identity} — 0 filled, all set", log);
    }
    else
    {
        if (isDryRun)
        {
            foreach (var (col, val) in mergeColumns)
                Log($"  would fill [{metadata.TableName}.{col}]: target=<unset> → seed='{val}'", log);
            updated++;
        }
        else
        {
            _writer.UpdateColumnSubset(metadata.TableName, keyCols, yamlRow, mergeColumns.Keys);
            updated++;
            Log($"  Seed-merge: [{metadata.TableName}].{identity} — {mergeColumns.Count} filled, "
                + $"{currentRow.Count - mergeColumns.Count - keyCols.Count} left", log);
        }
    }
    continue;
}

// Identity-unmatched: fall through to _writer.WriteRow (MERGE insert path) — existing
```

**Reuse Opportunity for SqlTable merge:** The loop at lines 292–297 already iterates
every target row to compute checksums. Store the full row dict keyed by identity in the
same pass (`existingRowsByIdentity[identity] = existingRow`). Zero extra DB round-trips
and matches D-18's intent (identity lookup is already free).

## Shared Helper Design Space — `IsUnsetForMerge` Candidates

### Signature shape

**Recommendation: single entry point + thin per-type shortcuts.**

```csharp
public static class MergePredicate
{
    // Primary — used by provider code that holds object + type hint
    public static bool IsUnsetForMerge(object? value, Type type) => ...;

    // Convenience overloads — used at Page-scalar sites where type is known statically
    public static bool IsUnsetForMerge(string? value);                 // D-02 string rule
    public static bool IsUnsetForMerge(int value);                     // 0 default
    public static bool IsUnsetForMerge(long value);
    public static bool IsUnsetForMerge(decimal value);
    public static bool IsUnsetForMerge(double value);
    public static bool IsUnsetForMerge(float value);
    public static bool IsUnsetForMerge(bool value);                    // false default
    public static bool IsUnsetForMerge(DateTime value);                // DateTime.MinValue
    public static bool IsUnsetForMerge(DateTime? value);               // null → true
    public static bool IsUnsetForMerge(Guid value);                    // Guid.Empty

    // SQL-type-aware overload for SqlTable path — dataType from INFORMATION_SCHEMA
    public static bool IsUnsetForMergeBySqlType(object? value, string? sqlDataType);
}
```

**Why this shape:**
- Object-typed overload handles the YAML dict case (`dto.Fields[key]` is `object`).
- Per-type overloads give clean call sites in `ApplyPageProperties`-style code.
- SQL-type-aware overload maps `INFORMATION_SCHEMA.DATA_TYPE` strings (`"nvarchar"`,
  `"bit"`, `"int"`, `"datetime2"`) to the right rule. Avoids the SqlTable provider having
  to reflect on CLR types.

**D-02 specialization note:** `IsUnsetForMerge(string?)` returns `true` for `null` OR
`string.IsNullOrEmpty(value)`. DW ItemService persists every field as string, so this is
the rule that matters for 90% of the merge paths.

### Location

**Recommendation: `src/DynamicWeb.Serializer/Infrastructure/MergePredicate.cs`** — same
folder as `TargetSchemaCache`. Justification:
- Phase 37-02 established Infrastructure/ as the home for deserialize-layer utilities.
- Shared across two providers; belongs in a neutral namespace.
- Pure static helper — no DI, no service.
- Avoids the `Configuration/` implications (it's not a config type) and `Merge/` new
  namespace (overkill for one file + one test file).

### Unit test surface (D-13 coverage)

Single test file: `tests/DynamicWeb.Serializer.Tests/Infrastructure/MergePredicateTests.cs`.

| Case | Expected |
|------|---------|
| `IsUnsetForMerge(null, typeof(string))` | true |
| `IsUnsetForMerge("", typeof(string))` | true |
| `IsUnsetForMerge(" ", typeof(string))` | **false** (whitespace is "set" per D-01 strict reading — planner confirm) [ASSUMED] |
| `IsUnsetForMerge("value", typeof(string))` | false |
| `IsUnsetForMerge(0, typeof(int))` | true |
| `IsUnsetForMerge(42, typeof(int))` | false |
| `IsUnsetForMerge(false, typeof(bool))` | true |
| `IsUnsetForMerge(true, typeof(bool))` | false |
| `IsUnsetForMerge(DateTime.MinValue, typeof(DateTime))` | true |
| `IsUnsetForMerge(DateTime.Now, typeof(DateTime))` | false |
| `IsUnsetForMerge((DateTime?)null, ...)` | true |
| `IsUnsetForMerge(Guid.Empty, typeof(Guid))` | true |
| `IsUnsetForMerge(Guid.NewGuid(), typeof(Guid))` | false |
| `IsUnsetForMerge(0m, typeof(decimal))` | true |
| `IsUnsetForMerge(1.5m, typeof(decimal))` | false |
| `IsUnsetForMergeBySqlType(null, "nvarchar")` | true |
| `IsUnsetForMergeBySqlType("", "nvarchar")` | true |
| `IsUnsetForMergeBySqlType("x", "nvarchar")` | false |
| `IsUnsetForMergeBySqlType(0, "int")` | true |
| `IsUnsetForMergeBySqlType(false, "bit")` | true |
| `IsUnsetForMergeBySqlType(DBNull.Value, "datetime2")` | true |
| `IsUnsetForMergeBySqlType(value, null)` | **false** (no type hint — safe default: treat as set, don't overwrite) [ASSUMED — open question] |

## Live-Read Mechanism Per Provider

### ContentDeserializer

| Surface | Read Mechanism | Code Reference |
|---------|----------------|----------------|
| Page scalars | `existingPage` already loaded at line 677 via `Services.Pages.GetPage(existingId)` | ContentDeserializer.cs:677 |
| Page Item fields | `itemEntry.SerializeTo(Dictionary<string, object?>)` — dumps current values as strings | ContentMapper.cs:45 |
| Page PropertyItem fields | `propItem.SerializeTo(Dictionary<string, object?>)` | ContentDeserializer.cs (LogDryRunPageUpdate uses pattern at 1461) |
| GridRow scalars | `existingRow2` loaded at line 877 | ContentDeserializer.cs:877 |
| GridRow Item fields | Same `SerializeTo` pattern | Established in ContentMapper |
| Paragraph scalars | `existingForUpdate` loaded at line 1036 | ContentDeserializer.cs:1036 |
| Paragraph Item fields | Same `SerializeTo` pattern | Established |

**All reads are zero-cost additions** — the entity objects are already loaded for the
UPDATE path. The only new call is `itemEntry.SerializeTo(dict)` per Item (which the
serialize path already does, so there's no surprise cost).

### SqlTableProvider

| Surface | Read Mechanism | Code Reference |
|---------|----------------|----------------|
| Current target row values | **Reuse the loop at SqlTableProvider.cs:292–297** — already reads every row for checksum building. Extend to also capture the full row dict. | SqlTableProvider.cs:292 |
| Column type hints | `_schemaCache.GetColumnTypes(tableName)` — already in scope at line 205 | SqlTableProvider.cs:205 |

**Key insight:** no second round-trip. The checksum-build scan already enumerates every
row; extending it to `existingRowsByIdentity = new Dictionary<string, Dictionary<string,
object?>>()` is O(N) memory increase for the same I/O. For large tables this could be
5–10 MB — acceptable for a deserialize pass. If memory pressure surfaces, a SELECT-on-
demand fallback is trivial to add later.

## Narrowed-UPDATE SQL Shape

### New method on SqlTableWriter

```csharp
public virtual WriteOutcome UpdateColumnSubset(
    string tableName,
    IReadOnlyList<string> keyColumns,
    Dictionary<string, object?> fullRow,       // YAML row, contains identity + all cols
    IEnumerable<string> columnsToUpdate,       // subset computed by merge predicate
    bool isDryRun,
    Action<string>? log = null)
{
    if (isDryRun)
    {
        log?.Invoke($"  [DRY-RUN] UPDATE [{tableName}] SET "
            + string.Join(",", columnsToUpdate.Select(c => $"[{c}]=@p"))
            + " WHERE " + IdentityPredicateText(keyColumns));
        return WriteOutcome.Updated;
    }

    var cb = new CommandBuilder();
    cb.Add($"UPDATE [{tableName}] SET ");
    var first = true;
    foreach (var col in columnsToUpdate)
    {
        if (!first) cb.Add(",");
        first = false;
        var val = fullRow.TryGetValue(col, out var v) ? v ?? DBNull.Value : DBNull.Value;
        cb.Add($"[{col}]=");
        cb.Add("{0}", val);
    }
    cb.Add(" WHERE ");
    for (int i = 0; i < keyColumns.Count; i++)
    {
        if (i > 0) cb.Add(" AND ");
        var keyCol = keyColumns[i];
        var keyVal = fullRow.TryGetValue(keyCol, out var kv) ? kv ?? DBNull.Value : DBNull.Value;
        cb.Add($"[{keyCol}]=");
        cb.Add("{0}", keyVal);
    }
    _sqlExecutor.ExecuteNonQuery(cb);
    return WriteOutcome.Updated;
}
```

**Key properties:**
- Parameterized via CommandBuilder `{0}` placeholders — no injection surface.
- Identity columns (pure auto-increment PKs not in `keyColumns` separately) are untouched
  because merge planning already excluded them.
- No IDENTITY_INSERT wrapping — we're updating, not inserting, so identity is irrelevant.
- `WriteOutcome.Updated` unconditionally — caller has already decided this row needs
  writing (merge plan non-empty).
- Dry-run returns Updated without executing SQL.

**Why not batch multiple rows in one UPDATE:** Each row has a different column subset
(target state varies per row). Batching would force either lowest-common-denominator
subset (wrong) or per-row statements bundled into a single command (minor perf gain, much
more complex). D-17 is silent on batching — keep it simple, one row per statement.

## Test Strategy Per D-13 / D-14 / D-15

### D-14: Plan structure

- **Plan 39-01 — Content merge + shared helper**
  - Task 1 (RED): Write `MergePredicateTests.cs` (see helper test surface above) — fails
    because helper doesn't exist.
  - Task 2 (GREEN): Implement `Infrastructure/MergePredicate.cs`. Tests pass.
  - Task 3 (RED): Write `ContentDeserializerSeedMergeTests.cs` covering acceptance
    scenario. Fails because merge branch not implemented.
  - Task 4 (GREEN): Replace lines 684–692 with merge branch. Extend `LogDryRunPageUpdate`
    to `LogSeedMergeDryRun` per D-19. Bypass `_permissionMapper.ApplyPermissions` (D-06).
    Recurse into gridrows/paragraphs (D-07) by leaving existing recursion paths intact —
    they'll inherit `_conflictStrategy` and apply their own merge branches.
  - Task 5 (REFACTOR): Extract shared `MergeScalars`/`MergeSubObjects` helpers if they
    emerge across Page/GridRow/Paragraph paths.

- **Plan 39-02 — SqlTable merge**
  - Task 1 (RED): Write `SqlTableWriterUpdateSubsetTests.cs` for new
    `UpdateColumnSubset`. Fails — method doesn't exist.
  - Task 2 (GREEN): Add `UpdateColumnSubset` to `SqlTableWriter`.
  - Task 3 (RED): Extend `SqlTableProviderDeserializeTests.cs` (or new
    `SqlTableProviderSeedMergeTests.cs`) with merge scenarios.
  - Task 4 (GREEN): Replace SqlTableProvider.cs:313–322 with merge branch. Extend row
    scan at line 292 to also capture `existingRowsByIdentity`.
  - Task 5 (REFACTOR): Thread dry-run log formatting uniformly with Content side.

- **Plan 39-03 (optional) — Live E2E gate**
  - Extend `tools/e2e/full-clean-roundtrip.ps1` with Deploy → tweak → Seed sub-pipeline
    before step 17 (EcomProducts assertion).

### D-13: Test scenarios per provider

**Content merge integration scenarios (Plan 39-01 Task 3):**

1. **Page exists, target has NULL MetaTitle, YAML has value** → Seed fills MetaTitle,
   counter filled=1
2. **Page exists, target has value MetaTitle, YAML has different value** → Seed leaves
   target alone, counter left=1
3. **Page exists, target has `""` ItemField `Mail1SenderEmail`, YAML has
   `"noreply@x.com"`** → Seed fills (D-02 string rule)
4. **Page exists, target has set ItemField, YAML has different value** → target
   preserved
5. **Page exists, target has `false` Active, YAML has `true`** → Seed fills (D-10
   explicit tradeoff — documented and tested)
6. **Page exists, target has set scalar bool=true, YAML has false** → **target
   preserved** (false is unset per D-01, so predicate matches true → write is skipped,
   AND predicate for YAML value doesn't apply — only the target-is-unset check matters)
7. **Page exists, target `Seo.MetaTitle` set but `Seo.MetaDescription` NULL** → Seed
   fills MetaDescription only, MetaTitle preserved (D-04 per-property)
8. **Page exists, dry-run mode** → per-field diff printed, no writes issued
9. **Page exists, all target fields set** → `Skipped++`, log "0 filled, N left" (D-11)
10. **Page exists, permissions on target differ from YAML** → permissions NOT touched
    (D-06)
11. **Page exists with child paragraph whose GUID doesn't match YAML paragraph** →
    paragraph is created (D-07 unmatched → create)
12. **Page exists with child paragraph whose GUID matches** → paragraph merges per-field
    (D-07 matched → merge)
13. **Re-run Seed on target that already absorbed a Seed** → all fields now set, no
    writes, idempotent (D-09)

**SqlTable merge integration scenarios (Plan 39-02 Task 3):**

1. **Row identity matches target, target column NULL, YAML has value** → UpdateColumnSubset
   fills that column
2. **Row identity matches target, all non-key columns set** → skipped, log "0 filled"
3. **Row identity matches target, partial set** → UpdateColumnSubset fires for the unset
   subset only
4. **Row identity does NOT match target** → fallthrough to `_writer.WriteRow` (MERGE
   insert)
5. **D-18 checksum fast-path**: YAML checksum == target checksum → skipped BEFORE merge
   branch engages
6. **Missing target column (D-12)** → silently excluded from merge plan, no error
7. **Dry-run merge**: per-column fill lines printed, no SQL executed
8. **No-PK table**: strategy ignored, uses existing TruncateAndInsertAll path
9. **Re-run Seed**: second run computes empty merge plan for every row → idempotent

**D-13 unit scenarios for `MergePredicate`** — already itemized above.

### D-15: Live E2E gate structure

The existing `tools/e2e/full-clean-roundtrip.ps1` runs:

```
Step 11: serialize?mode=deploy   → YAML deploy tree
Step 12: serialize?mode=seed     → YAML seed tree
Step 13: mirror SerializeRoot    → CleanDB filesystem
Step 14: deserialize?mode=deploy → writes Deploy subset, skips excluded
Step 15: deserialize?mode=seed   → NEW: should now fill excluded fields
Step 17: assert EcomProducts     → exists already
```

Phase 39's D-15 gate needs new assertions INSERTED between steps 15 and 17:

```
Step 15.1: SQL query CleanDB for:
  - Mail1SenderEmail (on EcomPayments/EcomShippings XML cols)
  - MetaTitle / branding text on known Area + Page rows
  Assert each has a non-NULL, non-empty value post-Seed
Step 15.2: Test re-run of Seed (second call to step 15) — no changes
  Assert all log lines show "Seed-merge: X fields filled=0, M left=N"

Step 15.3: Manual tweak preservation test
  a) Between steps 14 and 15, apply a small UPDATE to a page field that is
     in Seed YAML (e.g., "Test Page" MenuText = "tweaked")
  b) After step 15 completes, query that row
  c) Assert value remains "tweaked"
  (This is the strongest acceptance — proves destination-wins at field level)
```

The pipeline already has `Invoke-Sqlcmd-Scalar` for arithmetic assertions; a new
`Invoke-Sqlcmd-StringScalar` helper captures text values.

**Two-pass Seed invariance (D-09 idempotency):** Run step 15 twice in succession. Both
runs should log field-level summary; the second run should have every summary report "0
filled, N left".

## Page Scalars to Merge (D-05 Inventory)

Extracted from `DeserializePage` UPDATE path at lines 701–713 plus
`ApplyPageProperties` at lines 1193–1268.

**Always source-wins (never merged):**
- `PageUniqueId` — identity (D-05)
- `AreaId` — structure (D-05)
- `ParentPageId` — structure (D-05)

**Flat scalars in DeserializePage UPDATE path (lines 702–712):**
- `MenuText` (string)
- `UrlName` (string)
- `Active` (bool) — D-10 tradeoff applies
- `Sort` (int)
- `ItemType` (string)
- `LayoutTemplate` (string)
- `LayoutApplyToSubPages` (bool) — D-10
- `IsFolder` (bool) — D-10
- `TreeSection` (string)

**Flat scalars in `ApplyPageProperties` (lines 1196–1208):**
- `NavigationTag` (string)
- `ShortCut` (string)
- `Hidden` (bool) — D-10
- `Allowclick` (bool) — D-10, *defaults to true on DTO — see pitfall 3*
- `Allowsearch` (bool) — same
- `ShowInSitemap` (bool) — same
- `ShowInLegend` (bool) — same
- `SslMode` (int)
- `ColorSchemeId` (string)
- `ExactUrl` (string)
- `ContentType` (string)
- `TopImage` (string)
- `PermissionType` (int)
- `DisplayMode` (enum via TryParse)
- `ActiveFrom` (DateTime? — only set when HasValue)
- `ActiveTo` (DateTime? — same)

**Sub-object DTOs (D-04 per-property merge):**
- `Seo` → MetaTitle, MetaCanonical, Description, Keywords, Noindex, Nofollow, Robots404
- `UrlSettings` → UrlDataProviderTypeName, UrlDataProviderParameters (XML), UrlIgnoreForChildren, UrlUseAsWritten
- `Visibility` → HideForPhones, HideForTablets, HideForDesktops
- `NavigationSettings` → UseEcomGroups, Groups, ShopID, MaxLevels, ProductPage, NavigationProvider, IncludeProducts, ParentType (enum). **Gated by UseEcomGroups (line 1252) — merge only engages when current page already has NavigationSettings and new YAML also has UseEcomGroups=true.** This is a known-subtle area; see Pitfall 5.

Approximately 25 scalars + 4 sub-objects = ~30 properties, matching the Phase-23 count
referenced in CONTEXT.md D-05.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| SQL parameterization | String-concat UPDATE statements | `CommandBuilder` with `{0}` placeholders | Already the project-wide pattern [VERIFIED: SqlTableWriter, TargetSchemaCache]; avoids the SEED-002 / SqlIdentifierValidator concerns from Phase 37-03 |
| ItemField reading | Loop `itemEntry.Names` and `itemEntry[k]` | `itemEntry.SerializeTo(Dictionary<string,object?>)` | Existing pattern [VERIFIED: ContentMapper.cs:45]; returns everything in one pass as strings per D-02 |
| Page entity loading | New `SELECT` against Page table | `Services.Pages.GetPage(id)` | Already the UPDATE path [VERIFIED: ContentDeserializer.cs:677]; respects DW cache; returns fully-populated Page |
| Row dict building from SQL | New `IDataReader` loop | Reuse `SqlTableReader.ReadAllRows(tableName)` (already called at SqlTableProvider.cs:292) | Same reader pattern, same type coercion; no second round-trip per D-18 reuse |
| Schema-drift column filtering | Custom `INFORMATION_SCHEMA` query | `TargetSchemaCache.GetColumns(tableName)` / `LogMissingColumnOnce` | Phase 37-02 already consolidated this |
| Type coercion from YAML string | Custom `DateTime.TryParse` etc. | `TargetSchemaCache.Coerce(table, col, value)` | Already handles 14+ SQL types with correct defaults |
| Log format for counters | New struct or tuple | Repurpose `ProviderDeserializeResult.Skipped` (D-11) | Zero API shape change |
| Dry-run per-field diff | New method from scratch | Extend `LogDryRunPageUpdate` pattern at line 1428 | Existing template already does field-level diff |
| Moq setup for writer tests | New mock infrastructure | Reuse `Mock<SqlTableWriter>` pattern from SqlTableProviderDeserializeTests.cs:69 | Existing `virtual` on `WriteRow` supports mocking; add `virtual` on `UpdateColumnSubset` |

**Key insight:** Every read/write surface the merge touches already exists. The phase is
pure wiring — a predicate + a narrower call site — not new DW plumbing.

## Common Pitfalls

### Pitfall 1: DTO default-value overwrites type-default target

**What goes wrong:** `SerializedPage` has `Allowclick { get; init; } = true;` (and three
other visibility bools). If YAML omits the field, DTO carries `true`. If target has
`Allowclick = false` (the D-01 unset value), merge predicate says "fill" and flips
target to `true`. This is the D-10 tradeoff, but it's easy to miss that **YAML deserialize
already filled the DTO with `true`** rather than "field absent".

**Why it happens:** The DTO design predates the merge semantics — it was shaped for
source-wins where "absent in YAML" meaning "use DTO default" was deliberate.

**How to avoid:** Integration tests must cover the acceptance scenario **with both
Deploy-mode-produced YAML (explicit values) and hand-crafted minimal YAML (absent
fields)**. Documenting "YAML written by our serializer always contains these bools, so
the DTO default is effectively dead code for round-trip workflows" in a code comment
would help.

**Warning signs:** Round-trip test shows target bool flipped after Seed when customer
did NOT tweak it. Indicates the DTO default bled through.

### Pitfall 2: `ApplyPageProperties` assigns unconditionally today

**What goes wrong:** Current `ApplyPageProperties` (lines 1193–1268) does
`page.NavigationTag = dto.NavigationTag;` unconditionally — no check. If merge branch
calls it as-is, **every scalar on the existing page is overwritten with DTO value**,
defeating the merge.

**Why it happens:** Method is shared between INSERT (where overwrite is correct) and
UPDATE (where Seed now needs selectivity).

**How to avoid:** Either (a) introduce an `ApplyPagePropertiesWithMerge(page, dto,
counter)` variant that wraps each assignment in an `IsUnsetForMerge` gate, or (b) pass a
predicate delegate into the existing method. Option (a) is clearer at call site but
duplicates the sub-object wiring; option (b) keeps one implementation. **Recommendation:
option (a) — the merge path has to count filled/left anyway, and a delegate complicates
the counting.**

**Warning signs:** Test "target with set NavigationTag is preserved" fails — shows
predicate isn't wrapping the assignment.

### Pitfall 3: `SaveItemFields` null-outs missing fields on source-wins

**What goes wrong:** Current `SaveItemFields` (lines 1169–1181) iterates
`itemEntry.Names` and **sets missing fields to null**. If a Seed merge naively calls
`SaveItemFields` with only the filled subset, every OTHER field on target gets set to
null — catastrophic data loss.

**Why it happens:** Source-wins assumes YAML is the complete truth; absent field → clear
target. Seed is the exact opposite.

**How to avoid:** DO NOT call existing `SaveItemFields` from the Seed merge branch.
Instead, introduce `MergeItemFields(itemType, itemId, yamlFields, excludeFields,
counter)` that:
1. Loads the item
2. Reads current values via `SerializeTo(currentDict)`
3. For each (k, v) in `yamlFields`: if `IsUnsetForMerge(currentDict[k]) == true`, set
4. Calls `DeserializeFrom(filledDict)` and `Save()` **without** clearing non-filled
   fields

Same treatment for `SavePropertyItemFields` → `MergePropertyItemFields`.

**Warning signs:** Integration test shows customer's hand-tweaked field on target
becomes NULL after Seed — indicates accidentally routing through source-wins method.

### Pitfall 4: `ModuleSettings` XML merge semantics ambiguity

**What goes wrong:** `XmlFormatter.CompactWithMerge(dto.ModuleSettings,
existingForUpdate.ModuleSettings)` (line 1054) is an XML merge, not a string compare. For
Seed merge, if target has XML with some elements set and YAML has XML with other
elements, what's the correct merge? The `IsUnsetForMerge` rule doesn't apply directly —
the value is an XML document, not a scalar.

**Why it happens:** `ModuleSettings` isn't a simple string or bool — it's a structured
blob that the Paragraph deserialize path already does special-case merge on.

**How to avoid:** Document explicitly that `ModuleSettings` uses
`CompactWithMerge` regardless of `ConflictStrategy` — the XML-level merge is orthogonal
to the D-01 unset rule. Add this to the Scope Guardrail in Plan 39-01 task
documentation. An acceptance test that covers `ModuleSettings` round-trip on Seed mode
confirms the existing XML-merge behavior stays intact.

**Warning signs:** Test output shows `ModuleSettings` content unexpectedly cleared OR
duplicated after Seed.

### Pitfall 5: NavigationSettings gate and sub-object null target

**What goes wrong:** `ApplyPageProperties` creates `page.NavigationSettings = new
PageNavigationSettings { UseEcomGroups = true, ... }` at line 1254 — only when DTO has
`NavigationSettings != null && dto.NavigationSettings.UseEcomGroups`. On the merge path,
if target page currently has `NavigationSettings == null` (the D-01 unset for a complex
object), and YAML has a populated one, merge should fill — but "fill" here means
**constructing the whole sub-object at once**, not per-property merge within a
null-target.

**Why it happens:** D-04 says sub-objects are not atomic for merge, BUT if the whole
sub-object is null on target, per-property comparison is moot — every property is unset.

**How to avoid:** Special-case: if target sub-object is null and YAML sub-object is
non-null, assign the whole sub-object (counter+= number of properties filled in a whole-
object assign). If both are non-null, run per-property merge. If target has the sub-
object but YAML doesn't, no-op. Explicit in tests for each case.

**Warning signs:** Test "target NavigationSettings = null, YAML has
NavigationSettings" fails with NullReferenceException in per-property merge, OR fills
zero properties.

### Pitfall 6: DateTime and nullable DateTime asymmetry

**What goes wrong:** Per PROJECT.md STATE decisions: "ActiveFrom/ActiveTo as nullable
DateTime to distinguish unset from explicit". But the Page entity itself has
`page.ActiveFrom` as (possibly) non-nullable DateTime with a default of `DateTime.Now`.
So `IsUnsetForMerge(page.ActiveFrom)` where target was never touched returns **false**
(because `DateTime.Now != DateTime.MinValue`), so Seed doesn't fill.

**Why it happens:** DW's Page entity initializes `ActiveFrom`/`ActiveTo` to non-default
values on new pages (`DateTime.Now` / `DateHelper.MaxDate()`). These are not "unset" by
D-01's rule.

**How to avoid:** Seed merge of `ActiveFrom`/`ActiveTo` is effectively always a no-op in
practice — they're never `DateTime.MinValue` on a persisted page. Document this in the
plan: "ActiveFrom/ActiveTo flow through the merge predicate, but DW's defaults mean
they're always 'set' from Seed's perspective — Deploy is the only way to change them.
This is acceptable because customers don't typically tweak ActiveFrom/ActiveTo as
branding."

**Warning signs:** Test "target ActiveFrom is DW default, YAML has different value"
expects fill but gets preserved — indicates the `DateTime.MinValue` rule doesn't match
DW reality. Test should assert the documented behavior, not the naive D-01 expectation.

### Pitfall 7: `itemEntry.DeserializeFrom(dict)` may clear fields absent from dict

**What goes wrong:** `DeserializeFrom` behavior on a partial dict is undocumented in the
extracted code. If it clears absent fields, the Seed merge pattern of building a
filled-only dict and calling `DeserializeFrom` would clear other fields. If it merges
(leaves absent fields untouched), we're fine.

**Why it happens:** DW internal behavior — not verifiable from the serializer codebase
alone.

**How to avoid:** Integration test: load an item with 3 fields set (`A=x, B=y, C=z`),
call `DeserializeFrom(new {{ B = "new" }})`, assert `A=x, B=new, C=z`. If DW clears
absent fields, the merge strategy becomes: load current, overlay filled subset onto
current dict, then `DeserializeFrom(merged)`.

**Warning signs:** Integration test shows siblings of filled field also cleared.

### Pitfall 8: The `existingRowsByIdentity` memory cost

**What goes wrong:** Storing every target row dict keyed by identity doubles the memory
pressure of the checksum pass. For `EcomProducts` (2051 rows × ~20 cols × small values)
this is negligible; for a hypothetical customer with a 500k-row product table on a
narrow VM, it could matter.

**Why it happens:** Convenience — full row dict avoids a per-row SELECT during merge.

**How to avoid:** Set a threshold (e.g., 10k rows) above which merge switches to
per-row SELECT. OR: stream — iterate YAML rows first, build a HashSet of identities we
need to merge, then scan target rows once and capture only those in the set. This adds
complexity; defer unless the acceptance tests show memory pressure in CI. Document the
threshold choice in PLAN notes.

**Warning signs:** OOM on large tables during Seed. Not expected for Swift 2.2 baseline.

## Code Examples

### Example 1: Content merge branch skeleton

```csharp
// ContentDeserializer.DeserializePage, replacing lines 684–692
// Source: new code (this plan) — pattern borrows from LogDryRunPageUpdate at line 1428

if (_conflictStrategy == ConflictStrategy.DestinationWins)
{
    int filled = 0, left = 0;

    if (_isDryRun)
    {
        // D-19: per-field diff instead of the whole-page dry-run log
        LogSeedMergeDryRun(dto, existingPage, ctx);
        return existingId;
    }

    // D-05 + D-04: apply scalars and sub-objects with per-field merge gate
    filled += MergeScalarsAndSubObjects(existingPage, dto, ref left);
    Services.Pages.SavePage(existingPage);

    // D-02: ItemFields per-field string compare
    var pageExclude = ctx.ExcludeFieldsByItemType != null
        ? ExclusionMerger.MergeFieldExclusions(
            ctx.ExcludeFields?.ToList() ?? new List<string>(),
            ctx.ExcludeFieldsByItemType, dto.ItemType)
        : ctx.ExcludeFields;
    filled += MergeItemFields(existingPage.ItemType, existingPage.ItemId, dto.Fields, pageExclude, ref left);

    // D-03: PropertyItem per-field string compare
    filled += MergePropertyItemFields(existingPage, dto.PropertyFields, pageExclude, ref left);

    // D-06: permissions NOT applied on Seed — no _permissionMapper call

    if (filled == 0)
    {
        ctx.Skipped++;  // D-11: "all fields already set"
    }
    else
    {
        ctx.Updated++;
    }
    Log($"Seed-merge: page {dto.PageUniqueId} (ID={existingId}) — {filled} filled, {left} left");
    return existingId;  // D-07: children recursion continues below as before
}
```

### Example 2: MergeItemFields helper

```csharp
// D-02: DW persists every ItemField as string. Compare at string layer.
private int MergeItemFields(
    string? itemType, string itemId,
    Dictionary<string, object> yamlFields,
    IReadOnlySet<string>? excludeFields,
    ref int left)
{
    if (string.IsNullOrEmpty(itemType)) return 0;

    var itemEntry = Services.Items.GetItem(itemType, itemId);
    if (itemEntry == null)
    {
        Log($"WARNING: Could not load ItemEntry for type={itemType}, id={itemId}");
        return 0;
    }

    // Read current target state as strings (D-02)
    var currentDict = new Dictionary<string, object?>();
    itemEntry.SerializeTo(currentDict);

    var filledFields = new Dictionary<string, object?>();
    int filled = 0;
    foreach (var kvp in yamlFields)
    {
        if (ItemSystemFields.Contains(kvp.Key)) continue;
        if (excludeFields?.Contains(kvp.Key) == true) continue;

        currentDict.TryGetValue(kvp.Key, out var currentVal);
        if (MergePredicate.IsUnsetForMerge(currentVal?.ToString()))  // D-02 string overload
        {
            filledFields[kvp.Key] = kvp.Value;
            filled++;
        }
        else
        {
            left++;
        }
    }

    if (filledFields.Count == 0) return 0;

    // Overlay filled subset onto current state so DeserializeFrom doesn't clear siblings
    // (Pitfall 7 mitigation — defensive even if DW's DeserializeFrom preserves absent keys)
    foreach (var (k, v) in filledFields)
        currentDict[k] = v;

    itemEntry.DeserializeFrom(currentDict);
    itemEntry.Save();
    return filled;
}
```

### Example 3: SqlTableWriter narrowed UPDATE

Already shown in *Narrowed-UPDATE SQL Shape* above.

### Example 4: Test shape — Seed merge preserves customer tweak

```csharp
[Fact]
public void SeedDeserialize_PreservesCustomerTweakedField()
{
    // Arrange: page exists on target with a customer-tweaked MetaTitle
    var existingPage = BuildPageWithMetaTitle(guid: pageGuid, metaTitle: "Customer Tweaked Title");
    Services.Pages.SavePage(existingPage);

    // YAML has a different MetaTitle (would overwrite on source-wins)
    var yamlDto = new SerializedPage
    {
        PageUniqueId = pageGuid,
        Seo = new() { MetaTitle = "YAML Default Title" },
        // ... minimal scaffolding
    };
    var ctx = new WriteContext { PageGuidCache = new() { [pageGuid] = existingPage.ID } };

    var sut = new ContentDeserializer(
        configuration: BuildConfig(),
        conflictStrategy: ConflictStrategy.DestinationWins);

    // Act
    sut.DeserializePageViaTestHook(yamlDto, ctx);

    // Assert: customer's title is preserved, counter reflects "left"
    var after = Services.Pages.GetPage(existingPage.ID);
    Assert.Equal("Customer Tweaked Title", after.MetaTitle);
    Assert.Equal(0, ctx.Updated);
    Assert.Equal(1, ctx.Skipped);  // D-11: all-set case
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Seed = whole-entity skip (Phase 37-01 D-06) | Seed = per-field merge (Phase 39 D-01..D-18) | This phase | Deploy + Seed combine cleanly; re-runs are idempotent without markers |
| Two-config split (Phase 37-01 D-01) | Still two-config split | — | Unchanged; Phase 39 only changes Seed semantics |
| Source-wins in ContentDeserializer | Still source-wins (default) | — | Unchanged; only DestinationWins path changes |
| `"Seed-skip: [identity] (already present)"` log line | `"Seed-merge: [identity] — N filled, M left"` (D-11) | This phase | Log shape change — consumers parsing this string need update (none known) |

**Deprecated/outdated in this phase:**

- `ContentDeserializer.cs:684–692` Seed-skip block → replaced with merge branch
- `SqlTableProvider.cs:313–322` Seed-skip block → replaced with merge branch + checksum fast-path preserved
- Behavior: "pages whose PageUniqueId is already on target stay untouched" (37-VERIFICATION §1, 37-01-SUMMARY line 17) — SUPERSEDED by D-01..D-18

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.x (existing test project) [VERIFIED: tests/DynamicWeb.Serializer.Tests/*.csproj uses xUnit, multiple test files import `using Xunit`] |
| Config file | tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj (implicit xunit.runner.visualstudio) |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --filter "FullyQualifiedName~MergePredicate" -c Debug` |
| Full suite command | `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj -c Debug` |

### Phase Requirements → Test Map

| Decision ID | Behavior | Test Type | Automated Command | File Exists? |
|-------------|----------|-----------|-------------------|-------------|
| D-01 | `IsUnsetForMerge` returns true for NULL and type default per type | unit | `dotnet test --filter "FullyQualifiedName~MergePredicateTests.IsUnset_"` | Wave 0 |
| D-02 | `IsUnsetForMerge(string?)` returns true for null or empty | unit | `dotnet test --filter "FullyQualifiedName~MergePredicateTests.String"` | Wave 0 |
| D-03 | PropertyItem fields merge identically to ItemFields | integration | `dotnet test --filter "FullyQualifiedName~ContentDeserializerSeedMergeTests.PropertyItem"` | Wave 0 |
| D-04 | Seo, UrlSettings, Visibility, NavigationSettings merge per-property not per-sub-object | integration | `dotnet test --filter "FullyQualifiedName~ContentDeserializerSeedMergeTests.SubObject"` | Wave 0 |
| D-05 | All non-identity Page scalars participate | integration | `dotnet test --filter "FullyQualifiedName~ContentDeserializerSeedMergeTests.Scalar"` | Wave 0 |
| D-06 | `ApplyPermissions` bypassed on Seed UPDATE | integration | `dotnet test --filter "FullyQualifiedName~ContentDeserializerSeedMergeTests.Permissions"` | Wave 0 |
| D-07 | Matched gridrows/paragraphs merge; unmatched created | integration | `dotnet test --filter "FullyQualifiedName~ContentDeserializerSeedMergeTests.Recursion"` | Wave 0 |
| D-08 | Shared helper works in both Content + SqlTable merge paths | unit (helper) + integration (provider-level) | covered by D-01 + D-13/D-14 tests | Wave 0 |
| D-09 | Re-running Seed is a no-op (idempotent) | integration | `dotnet test --filter "FullyQualifiedName~ContentDeserializerSeedMergeTests.Idempotent_RerunNoOp"` | Wave 0 |
| D-10 | Customer-set bool/int defaults are overwritten on next Seed | integration | `dotnet test --filter "FullyQualifiedName~ContentDeserializerSeedMergeTests.TypeDefault_Overwritten"` | Wave 0 |
| D-11 | New log format + Skipped=="all set" semantics | integration | same test class — asserts log output contains "Seed-merge:" and counter values | Wave 0 |
| D-12 | Missing target columns silently drop from merge plan | integration (SqlTable) | `dotnet test --filter "FullyQualifiedName~SqlTableProviderSeedMergeTests.SchemaDrift"` | Wave 0 |
| D-17 | SqlTableWriter `UpdateColumnSubset` emits narrowed UPDATE with identity WHERE | unit | `dotnet test --filter "FullyQualifiedName~SqlTableWriterUpdateSubsetTests"` | Wave 0 |
| D-18 | Checksum fast-path runs BEFORE merge branch | integration (SqlTable) | `dotnet test --filter "FullyQualifiedName~SqlTableProviderSeedMergeTests.ChecksumFastPath_FiresFirst"` | Wave 0 |
| D-19 | Dry-run prints per-field "would fill" lines | integration | both provider test classes assert log capture contains expected pattern | Wave 0 |
| D-15 | Live E2E gate: Deploy → tweak → Seed preserves tweak | E2E (manual pipeline) | `pwsh tools/e2e/full-clean-roundtrip.ps1` (extended with sub-pipeline) | **Wave 0 pipeline extension** |

### Sampling Rate

- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --filter "FullyQualifiedName~MergePredicate|FullyQualifiedName~SeedMerge" -c Debug` (< 30s)
- **Per wave merge:** `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj -c Debug` (full suite, ~1–2 min given ~620 tests at baseline)
- **Phase gate:** Full suite green + `tools/e2e/full-clean-roundtrip.ps1` exits 0 with Seed-merge assertions passing (D-15)

### Wave 0 Gaps

New test files to be created:
- [ ] `tests/DynamicWeb.Serializer.Tests/Infrastructure/MergePredicateTests.cs` — covers D-01/D-02 helper (all ~22 cases itemized above)
- [ ] `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerSeedMergeTests.cs` — covers D-03..D-11, D-19 on Content path (13 integration scenarios)
- [ ] `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterUpdateSubsetTests.cs` — covers D-17 (parameterized SQL shape, column subset, no IDENTITY_INSERT)
- [ ] `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` — covers D-12/D-17/D-18/D-19 on SqlTable path (9 integration scenarios)

Pipeline extension:
- [ ] `tools/e2e/full-clean-roundtrip.ps1` — insert Deploy → tweak → Seed sub-pipeline steps (15.1 / 15.2 / 15.3) before step 17

Framework install: None — xUnit + Moq already present.

### Observability gates

New log lines to grep for in test output:
- `Seed-merge: [identity] — N filled, M left` (D-11) — asserted in 2 integration test classes
- `would fill [col=X]: target=<unset> → seed='<value>'` (D-19) — asserted in dry-run tests
- Absence of `Seed-skip:` after the phase ships (regression guard)

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Whitespace-only strings (`" "`, `"\t"`) are "set", not "unset" — per strict D-01 reading | Shared Helper Design Space / Unit test surface | If user intent is "treat whitespace as unset", 1 test case flips. Minor; covered by documenting the helper's rule explicitly. |
| A2 | `IsUnsetForMergeBySqlType(value, null)` returns false (conservative — unknown type means "don't overwrite") | Shared Helper Design Space | If D-12 intent is "missing type hint means fill", tests flip. Recommend flagging to user before writing helper. |
| A3 | `itemEntry.DeserializeFrom(partialDict)` leaves absent fields untouched (Pitfall 7) | Code Examples / Pitfall 7 | If DW clears absent fields, merge must overlay onto current dict first — mitigation already baked into Example 2 defensively. No risk in practice because defense is in place. |
| A4 | `page.ActiveFrom` / `ActiveTo` default to `DateTime.Now` / `DateHelper.MaxDate()` on new pages — never `DateTime.MinValue` (Pitfall 6) | Pitfalls | If DW actually initializes to MinValue, merge behavior differs from documented no-op. Mitigated by writing a test that asserts the actual behavior. |
| A5 | Test assemblies are net8.0 (from obj paths) while host DW targets net10.0 (from full-clean-roundtrip.ps1) — the serializer DLL must build for both | Cross-cutting | If the serializer .csproj targets only net8.0, hot-deploy to net10.0 host works only due to binary compat of referenced DW packages. Already verified by Phase 38.1's live pipeline — low risk. |
| A6 | `PagePropertyMapper.cs` file does not exist — the mapping logic lives inline in `ContentDeserializer.ApplyPageProperties` (lines 1193–1268) | CONTEXT.md canonical refs vs. reality | CONTEXT.md canonical refs say `PagePropertyMapper.cs`. Grep and Glob both return no file. The ~30 properties are in `ApplyPageProperties` inside `ContentDeserializer`. Planner must reference the actual location. **Low risk — clerical mismatch in CONTEXT, real code is unambiguous.** |
| A7 | Existing `SqlTableProviderDeserializeTests.cs` has no DestinationWins assertions to rewrite — only SourceWins cases exist | Test Strategy | Phase 39 does not break any existing test; it only adds new ones. No "adjust tests that assert row-level skip" work because no such test exists. Scope-shrinks Plan 39-02 slightly. [VERIFIED: grep for `DestinationWins.*Deserialize` in tests/ returns only StrictModeIntegrationTests using SourceWins] |

## Open Questions

1. **Whitespace-only target string: unset or set?**
   - What we know: D-01 says NULL or empty string `""` is unset.
   - What's unclear: `" "`, `"  "`, `"\t\n"` — neither NULL nor empty but arguably customer "cleared" the field to whitespace accidentally.
   - Recommendation: Treat whitespace as "set" (strict D-01). If the acceptance scenario surfaces a case where whitespace-clearing matters, escalate to D-01.1 amendment.

2. **`IsUnsetForMergeBySqlType(value, null)` behavior?**
   - What we know: D-12 says missing target columns silently drop.
   - What's unclear: what if the column exists on target but its `INFORMATION_SCHEMA` DATA_TYPE isn't in our coerce table?
   - Recommendation: Return false (conservative) and log a single WARNING via `_schemaCache.LogMissingColumnOnce`-style dedup. Planner confirms with user.

3. **`DeserializeFrom(partialDict)` DW behavior — preserves or clears?**
   - What we know: Existing source-wins path explicitly null-outs missing fields BEFORE calling `DeserializeFrom` (line 1178), implying DeserializeFrom does NOT auto-clear.
   - What's unclear: Whether a smaller dict passed directly (without pre-null-out) leaves siblings untouched.
   - Recommendation: Plan 39-01 Task 3 includes a targeted integration test for this exact behavior; mitigation via overlay-onto-current-dict is in Example 2 defensively regardless.

4. **NavigationSettings sub-object null-to-filled transition (Pitfall 5)?**
   - What we know: D-04 says sub-objects merge per-property.
   - What's unclear: Whether "target sub-object is null, YAML has sub-object" should construct the whole sub-object or skip.
   - Recommendation: Fill the whole sub-object when target is null (consistent with "unset" being the trigger). Test case added.

5. **EcomPayments / EcomShippings XML columns (`PaymentGatewayParameters`, `ShippingServiceParameters`) — how does merge interact with XML element exclusions?**
   - What we know: The Deploy config excludes specific XML elements (e.g. `Mail1SenderEmail`) from those XML columns; the Seed YAML keeps them.
   - What's unclear: After Deploy strips elements from XML on target, Seed YAML brings them back — but the merge predicate is operating on the column value (a whole XML string), not per-element. `IsUnsetForMerge` on a non-empty XML column returns false, so Seed would NOT fill even when the excluded elements are missing from target.
   - Recommendation: Document this as a known limitation in the phase exit notes. The Deploy-config mail fields live in XML sub-structures that Seed won't reach through column-level merge. To fix, we'd need XML-element-level merge — explicit scope for a follow-up phase. **This is a potential D-15 E2E gate gotcha — the live test should verify whether Mail1SenderEmail actually gets filled by Seed. If not, the acceptance scenario from CONTEXT.md is only partially delivered.** Flag to user before Plan 39-02 executes.

6. **Should Page scalar merge happen BEFORE or AFTER ItemFields merge?**
   - What we know: Existing source-wins UPDATE path does scalars then ItemFields (lines 701–724).
   - What's unclear: Whether merge ordering matters for DW cascade behavior (e.g. does changing `ItemType` invalidate cached ItemFields lookups?).
   - Recommendation: Keep ordering identical to source-wins (scalars first, then ItemFields, then PropertyItem). Minimizes surprise.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Plans 39-01/02 build | ✓ (assumed — all prior phases use it) | — | — |
| xUnit test runner | D-13 unit/integration tests | ✓ | 2.x | — |
| Moq | SqlTableWriter.UpdateColumnSubset mocking | ✓ | (existing) | — |
| DW hosts (Swift 2.2 + CleanDB) | D-15 live E2E gate | ✓ at configured ports (from `full-clean-roundtrip.ps1` defaults) | net10.0 | Plan 39-03 is optional per D-14 — defer if hosts unavailable |
| sqlcmd / sqlpackage | D-15 pipeline bacpac restore + assertions | ✓ (pipeline auto-installs sqlpackage if absent) | — | — |
| PowerShell (pwsh) | D-15 pipeline | ✓ (pipeline shipped Phase 38.1) | — | — |

**Missing dependencies with no fallback:** None identified.

**Missing dependencies with fallback:** Plan 39-03 is optional — if DW hosts are
unavailable, Plans 39-01 + 39-02 ship with integration-test-only coverage and the user
runs the E2E gate manually later.

## Runtime State Inventory

Not applicable — this phase is a pure behavior refactor, not a rename/migration. No
stored strings change. No service configs change. No secrets rename. No build artifact
renames.

## Security Domain

The `security_enforcement` config key is not explicitly set in `.planning/config.json`;
absent = enabled. Applying ASVS categories for this phase:

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Phase is pure deserialization logic; auth is the hosting app's concern |
| V3 Session Management | no | same |
| V4 Access Control | partial | D-06 explicitly bypasses PermissionMapper on Seed merge — documented decision, tested |
| V5 Input Validation | yes | YAML values flow into SQL; parameterized via CommandBuilder `{0}` placeholders (existing pattern) |
| V6 Cryptography | no | No secrets, tokens, hashes in this path |

### Known Threat Patterns for DW Serializer Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection via YAML column names | Tampering | Phase 37-03 `SqlIdentifierValidator` already validates table/column names at config load; merge path inherits this — no new surface |
| SQL injection via YAML column values | Tampering | CommandBuilder parameterized binding (`cb.Add("{0}", value)`); new `UpdateColumnSubset` uses identical pattern |
| Unintended permission change via Seed | Elevation of Privilege | D-06 bypass is explicit: `_permissionMapper.ApplyPermissions` NOT called on Seed UPDATE. Test asserts permissions are untouched. |
| Denial-of-service via large `existingRowsByIdentity` dict | DoS | Pitfall 8 — document threshold, defer mitigation unless observed in production. Swift 2.2 baseline is well under any reasonable threshold. |
| Information disclosure via new log lines | Info Disclosure | D-11's `"N filled, M left"` is count-only, no field values — no sensitive data leaked. D-19's dry-run log CAN include values (`"would fill [col=X]: ... → seed='<value>'"`) — dry-run logs are intentionally verbose; document as "do not enable dry-run with verbose logging in production trust-boundary contexts". |

## Sources

### Primary (HIGH confidence)

- [VERIFIED: `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs`] — direct code read lines 1–200, 600–1070, 1420–1535, including `DeserializePage` UPDATE path 684–733, `ApplyPageProperties` 1193–1268, `SaveItemFields` 1153–1187, `SavePropertyItemFields` 1075–1114, `LogDryRunPageUpdate` 1428–1496
- [VERIFIED: `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs`] — full file read, exact line 313–322 Seed-skip + 304–311 checksum fast-path + 292–297 existingChecksums build
- [VERIFIED: `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs`] — full file read, confirmed no UPDATE path exists (only MERGE via BuildMergeCommand + TruncateAndInsertAll + RowExistsInTarget existence check)
- [VERIFIED: `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableReader.cs`] — full file read, confirmed `ReadAllRows(tableName, whereClause?)` + `GenerateRowIdentity` + `CalculateChecksum`
- [VERIFIED: `src/DynamicWeb.Serializer/Infrastructure/TargetSchemaCache.cs`] — full file read, schema-drift + type coercion surface confirmed
- [VERIFIED: `src/DynamicWeb.Serializer/Configuration/ConflictStrategy.cs`] — full file read, 2-value enum with JsonConverter, confirmed no plumbing changes needed
- [VERIFIED: `src/DynamicWeb.Serializer/Models/SerializedPage.cs`] — full DTO shape with DTO-default tradeoffs
- [VERIFIED: `src/DynamicWeb.Serializer/Providers/ProviderDeserializeResult.cs`] — full file, confirmed `Skipped` is plain int field no API change needed
- [VERIFIED: `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs` lines 30–65] — `ItemEntry.SerializeTo(dict)` read pattern
- [VERIFIED: `tools/e2e/full-clean-roundtrip.ps1`] — full file read, confirmed pipeline structure + insertion points for D-15 extension
- [VERIFIED: `.planning/phases/39-seed-mode-field-level-merge-deploy-seed-split-intent-is-fiel/39-CONTEXT.md`] — all 20 decisions + canonical refs + reusable assets
- [VERIFIED: `.planning/phases/37-production-ready-baseline/37-CONTEXT.md` §D-06] — superseded row-level skip decision text
- [VERIFIED: grep across `tests/DynamicWeb.Serializer.Tests/`] — confirmed no existing test asserts DestinationWins row-level skip semantics (only SourceWins tests in integration layer)

### Secondary (MEDIUM confidence)

- [CITED: `memory/feedback_no_backcompat.md`] — no backcompat directive (cross-session feedback)
- [CITED: `memory/feedback_content_not_sql.md`] — Content tables must use DW APIs
- [CITED: `.planning/STATE.md`] — Phase 37-01 SUMMARY line 17 + 192 + 235/236 documenting current row-level skip decision

### Tertiary (LOW confidence / [ASSUMED])

- [ASSUMED] Whitespace string = "set" (conservative D-01 reading)
- [ASSUMED] Unknown SQL data type hint = "don't fill" (conservative)
- [ASSUMED] `itemEntry.DeserializeFrom(partialDict)` preserves absent fields — defensive overlay in Example 2 mitigates uncertainty
- [ASSUMED] `page.ActiveFrom` defaults to non-MinValue on new pages per PROJECT.md decision note

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies, all cited packages verified in-repo
- Architecture: HIGH — every line range verified by direct file read; merge branch shape grounded in existing source-wins code
- Pitfalls: HIGH — 7 of 8 pitfalls derive from direct observation of existing code shape or CONTEXT.md tradeoffs; Pitfall 7 (`DeserializeFrom` behavior) is the one LOW-confidence item, mitigated by a test + defensive overlay in Example 2
- Test strategy: HIGH — test surface fully itemized against each decision ID

**Research date:** 2026-04-22
**Valid until:** 2026-05-22 (stable — no external fast-moving dependencies)

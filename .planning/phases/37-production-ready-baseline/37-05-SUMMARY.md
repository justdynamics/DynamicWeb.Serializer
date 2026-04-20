---
phase: 37-production-ready-baseline
plan: 05
subsystem: infrastructure / serializer correctness
tags: [template-manifest, link-sweep, cross-env-resolution, sqltable, content-provider, admin-ui]

# Dependency graph
requires:
  - phase: 37-01
    provides: ModeConfig Deploy/Seed split — ContentSerializer/ContentDeserializer still
      Deploy-scoped; no per-mode threading needed here
  - phase: 37-02
    provides: Unchanged SqlTableProvider / TargetSchemaCache structure — LINK-02 pass 2
      hooks between TargetSchemaCache coercion and BuildMergeCommand
  - phase: 37-03
    provides: SqlIdentifierValidator reused for ResolveLinksInColumns validation in
      ConfigLoader and SavePredicateCommand; same aggregated-error pattern
  - phase: 37-04
    provides: StrictModeEscalator + orchestrator log-wrapper — template manifest +
      SqlTable link warnings flow through the same WARNING-prefix interception path

provides:
  - TemplateAssetManifest — manifest-only template tracking (TEMPLATE-01 / D-19, D-20);
    Write/Read/Validate of `templates.manifest.yml` with path-traversal guard (T-37-05-01),
    DoS cap of 100,000 refs (T-37-05-05), and truncated `referencedBy` listing (>5
    sources summarised as `first5, ..., (N total)`)
  - TemplateReferenceScanner — walks SerializedPage trees and extracts page-layout /
    item-type / grid-row references with source-page attribution. Item-type refs collected
    from page.ItemType + gridRow.ItemType + paragraph.ItemType (union) so one scan covers
    the three DW item-type surfaces.
  - BaselineLinkSweeper — post-serialize sweep (LINK-02 pass 1 / D-22). Returns
    SweepResult { Unresolved, ResolvedCount }; ContentSerializer throws InvalidOperationException
    with a multi-line breakdown when any Default.aspx?ID=N / "SelectedValue": "N" reference
    has no matching SerializedPage.SourcePageId in the same tree.
  - InternalLinkResolver.ResolveInStringColumn — thin alias for ResolveLinks; pins the
    SqlTable-column use case in types at call sites without duplicating regex/map logic.
  - ProviderPredicateDefinition.ResolveLinksInColumns — per-SqlTable-predicate column
    opt-in for at-deserialize Default.aspx?ID=N rewriting.
  - ProviderDeserializeResult.SourceToTargetPageMap — ContentProvider-populated map of
    source-env page IDs → target-env page IDs; aggregated by the orchestrator and threaded
    into SqlTable predicates that opt in via ResolveLinksInColumns.
  - ISerializationProvider.Deserialize gains an optional `InternalLinkResolver?` parameter
    (6th position, default null). Backwards-compat for Content provider (ignored) and
    contract-required for SqlTableProvider (threaded into SqlTableWriter.ApplyLinkResolution).
  - SerializerOrchestrator.DeserializeAll conditionally reorders Content predicates to run
    BEFORE SqlTable predicates when any SqlTable predicate has non-empty
    ResolveLinksInColumns (approach A — see Decisions).
  - Admin UI: PredicateEditModel.ResolveLinksInColumns + screen editor + save command
    persistence + query round-trip (with opportunistic fix for pre-existing Phase 37-03
    gap where WhereClause / IncludeFields weren't re-read from disk into the edit form).
  - README.md "Cross-environment link resolution (LINK-02)" section — documents the
    pre-commit sweep, content pages pass, SqlTable opt-in pass, and strict-mode interaction.

affects:
  - Swift 2.2 baseline: F-17 identified ~10 orphan Default.aspx?ID= references
    (e.g. `Default.aspx?ID=9579` pointing to pages outside the baseline path); once this
    plan ships, those serializer runs will fail loud with the per-ref breakdown so the
    baseline config can be fixed before commit. This is the intended CACHE-01-style loud
    failure pattern from Phase 37-04.
  - F-07 UrlPath.UrlPathRedirect round-trip: Swift 2.2's known UrlPath row with
    `UrlPathRedirect = Default.aspx?ID=5862` now round-trips cleanly to CleanDB when the
    predicate carries `resolveLinksInColumns: ["UrlPathRedirect"]`.
  - Inline template validation in ContentDeserializer (~80 LOC across three methods +
    three call sites) is removed — the manifest pre-flight covers all three reference
    kinds up-front so operators see missing-template problems BEFORE any page writes begin.
  - Phase 37-03 follow-up admin UI gap (IncludeFields / WhereClause round-trip) closed
    inline while touching PredicateByIndexQuery.

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Manifest-only template tracking per D-19 / D-20 — record WHICH cshtml / grid-row /
       item-type files the baseline needs, do NOT serialize their contents. Templates
       deploy via the code repo; the manifest catches deploys where the code is missing
       or out of sync with the baseline."
    - "Pre-flight vs per-page validation — inline ValidatePageLayout / ValidateItemType /
       ValidateGridRowDefinition ran per-page during write, which meant operators saw
       dozens of WARNING lines interleaved with page creates. The manifest pre-flight
       runs ONCE at the start of Deserialize — operator sees 'Template validation: 145
       found, 3 missing — see warnings above' immediately, then the writes proceed
       against a known-validated template set."
    - "Orchestrator ordering switch (approach A) — when any SqlTable predicate opts into
       link resolution, Content predicates run FIRST. Chosen over approach B (second
       deserialize pass per link-dependent SqlTable predicate) because A is a pure list
       reorder with no second sweep through SqlTable data. The existing FK ordering
       still runs before the LINK-02 reorder, so parent→child ordering within SqlTable
       predicates is preserved."
    - "Cross-environment map built from ContentProvider — after ContentDeserializer
       completes, ContentProvider re-reads every area's YAML tree and pairs SourcePageId
       with target PageID via PageUniqueId (GUID) lookup. The orchestrator aggregates
       maps across multiple Content predicate runs into a single dictionary fed to
       SqlTable predicates that opt in."
    - "Interface-first but compatible — ISerializationProvider.Deserialize gains a 6th
       optional parameter (InternalLinkResolver?) rather than a sibling overload. Moq
       test fixtures required updating because expression trees don't support optional
       arguments; all call sites were updated in the same commit (Rule 3 blocker)."

key-files:
  created:
    - src/DynamicWeb.Serializer/Infrastructure/TemplateAssetManifest.cs
    - src/DynamicWeb.Serializer/Infrastructure/TemplateReferenceScanner.cs
    - src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs
    - tests/DynamicWeb.Serializer.Tests/Infrastructure/TemplateAssetManifestTests.cs
    - tests/DynamicWeb.Serializer.Tests/Infrastructure/TemplateReferenceScannerTests.cs
    - tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs
    - tests/DynamicWeb.Serializer.Tests/Serialization/InternalLinkResolverSqlTableTests.cs
    - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableLinkResolutionIntegrationTests.cs
  modified:
    - src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs
    - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
    - src/DynamicWeb.Serializer/Serialization/InternalLinkResolver.cs
    - src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs
    - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
    - src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs
    - src/DynamicWeb.Serializer/Providers/SerializationProviderBase.cs
    - src/DynamicWeb.Serializer/Providers/ProviderDeserializeResult.cs
    - src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs
    - src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs
    - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs
    - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs
    - src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
    - src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs
    - src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs
    - tests/DynamicWeb.Serializer.Tests/Integration/StrictModeIntegrationTests.cs
    - tests/DynamicWeb.Serializer.Tests/Providers/SerializerOrchestratorTests.cs
    - README.md

decisions:
  - "Orchestrator ordering approach A (Content-first reorder) chosen over approach B
     (dedicated second deserialize pass after SqlTable writes). Reason: smallest diff.
     Approach A is a list reorder inside DeserializeAll triggered by a single
     `predicates.Any(p => p.ResolveLinksInColumns.Count > 0)` check; approach B would
     require a second sweep over the SqlTable predicates with a separate code path for
     'rewrite existing rows'. The existing FK-ordering logic runs BEFORE the LINK-02
     reorder so parent→child ordering within SqlTable is preserved — the LINK-02 reorder
     only flips Content-vs-SqlTable, not intra-SqlTable order."
  - "InternalLinkResolver.ResolveInStringColumn is a thin alias for ResolveLinks rather
     than a rewrite of the regex/map logic. Keeps the SqlTable call site's intent
     documented in types while guaranteeing zero behavior drift from the content-layer
     path. Planner explicitly allowed this as the smallest diff option."
  - "ISerializationProvider.Deserialize signature gained an optional 6th parameter
     instead of a separate Deserialize2 overload. Same-named overloads with an added
     optional arg are backwards-compat for call sites that use optional-argument defaults
     but NOT for Moq expression-tree Setup calls — every .Setup / .Verify / .Returns
     lambda in SerializerOrchestratorTests + StrictModeIntegrationTests had to include
     the new parameter explicitly. Tracked as Rule 3 blocker (auto-fixed inline)."
  - "Raw-numeric link sweep NOT implemented in BaselineLinkSweeper. The deserialize-time
     InternalLinkResolver's 'entire string is a pure integer AND is in the map' check
     still covers the LinkEditor default of plain '121' strings. Sweeping raw numerics
     at serialize time would generate false positives on ordinary numeric fields (sort
     orders, widths, ColumnId values, etc.) — documented in-code."
  - "Template manifest located at the serializer OUTPUT ROOT (alongside area folders),
     NOT inside a per-mode subfolder. Reason: ContentSerializer doesn't know per-mode
     at the point where the manifest is written (the orchestrator's per-mode manifest
     work from Phase 37-01 operates on the output root directly; templates.manifest.yml
     sits next to {mode}-manifest.json). Pre-flight validation in ContentDeserializer
     reads from `_configuration.OutputDirectory` — the same root."
  - "Template manifest validation is LENIENT at the escalator level — the template
     manifest's own StrictModeEscalator instance is constructed with strict: false so
     missing templates always log via the orchestrator's WARNING sink. Strict-mode
     treatment is inherited from Phase 37-04's log-wrapper at the orchestrator boundary,
     which intercepts every WARNING: line through RecordOnly. This avoids a second
     strict-mode code path inside ContentDeserializer."
  - "Opportunistic Phase 37-03 gap fix: PredicateByIndexQuery now populates Model.WhereClause
     and Model.IncludeFields from the saved predicate. Pre-Phase-37-05 these fields were
     settable via SavePredicateCommand but silently empty when re-editing the predicate.
     This is a two-line no-regression fix (one per field) and was free to include since
     the query file was already modified to add ResolveLinksInColumns round-trip. Marks
     the admin-UI edit form for SqlTable predicates as now fully round-tripping."
  - "PredicateByIndexQuery ordering gap — Model.Mode was previously loaded correctly
     but the new ResolveLinksInColumns was added at the end of the model spread. No
     ordering significance (PredicateEditModel fields are independent); placement
     follows the pattern of IncludeFields (grouped with the other SqlTable opt-in)."

metrics:
  duration: ~48 minutes
  completed: 2026-04-20
---

# Phase 37 Plan 37-05: TEMPLATE-01 + LINK-02 Summary

Closed the last two baseline-correctness gaps surfaced by the Swift 2.2 → CleanDB round-trip
test and promoted `TemplateAssetManifest` + `BaselineLinkSweeper` + per-predicate
`resolveLinksInColumns` into the deserialize pipeline. Together the three pieces make a
serialize run fail loudly at source when the baseline is internally inconsistent (orphan
Default.aspx?ID= refs), and make a deserialize run fail loudly on the target when the
code deploy is missing templates the baseline expected. The combination gets v0.5.0 to
"production ready" against the CI/CD target that Phase 37 set as the phase goal.

## What changed

### TEMPLATE-01 — TemplateAssetManifest + TemplateReferenceScanner

`src/DynamicWeb.Serializer/Infrastructure/TemplateAssetManifest.cs` — record type
`TemplateReference { Path, Kind, ReferencedBy }` + manifest class with Write/Read/Validate.
Manifest YAML (sorted by `(Kind, Path)` for deterministic diffs) lives at the serializer
output root as `templates.manifest.yml`.

**Validate** checks each reference against `{filesRoot}/Templates/Designs/**` (page-layout
/ grid-row) or `{filesRoot}/System/Items/ItemType_*.xml` (item-type). Missing entries
`escalator.Escalate(...)` once per reference with the `referencedBy` list truncated to the
first 5 sources + `(N total)` so the error message scales.

**Path-traversal guard (T-37-05-01):** every reference path must match
`^[a-zA-Z0-9_./\- ]+$` AND reject `..`, backslash, and drive-rooted paths. Paths failing
the check are flagged and escalated without touching the filesystem.

**DoS guard (T-37-05-05):** manifests claiming more than 100,000 references are rejected
up-front with a single Escalate call. Swift 2.2's ~1500 pages emit ~10 refs in practice;
the cap is four orders of magnitude above the expected size.

`src/DynamicWeb.Serializer/Infrastructure/TemplateReferenceScanner.cs` — walks a
`List<SerializedPage>` tree and emits `TemplateReference` records for every `page.Layout`
(kind `page-layout`), `page.ItemType` / `gridRow.ItemType` / `paragraph.ItemType` (kind
`item-type`, deduped by path), and `gridRow.DefinitionId` (kind `grid-row`). The
`ReferencedBy` list accumulates across all source pages that share the same (kind, path)
for actionable multi-source attribution.

**ContentSerializer.Serialize** appends after all predicates emit:
```csharp
var scanner = new TemplateReferenceScanner();
var refs = scanner.Scan(allSerializedPages);
new TemplateAssetManifest().Write(_configuration.OutputDirectory, refs);
Log($"Wrote {TemplateAssetManifest.ManifestFileName} with {refs.Count} template reference(s)");
```

**ContentDeserializer.Deserialize** runs pre-flight before any page writes:
```csharp
var refs = _templateManifest.Read(_configuration.OutputDirectory);
if (refs != null && refs.Count > 0)
{
    var missing = _templateManifest.Validate(_filesRoot, refs, _templateEscalator);
    Log($"Template validation: {refs.Count - missing} found, {missing} missing");
}
```

**Inline validation removed:** ~80 LOC across `ValidatePageLayout`, `ValidateItemType`,
`ValidateGridRowDefinition` + the 4 call sites in `DeserializePage` / `DeserializeGridRow` /
`DeserializeParagraph`. The manifest pre-flight covers every ref up-front so operators see
missing-template summaries BEFORE any create/update SQL runs.

### LINK-02 pass 1 — BaselineLinkSweeper

`src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` — post-serialize tree
sweep. Regex patterns reused verbatim from `InternalLinkResolver` (`Default\.aspx\?ID=(\d+)`
and `"SelectedValue": "(\d+)"`) so the sweep matches exactly what the deserialize path will
try to rewrite. Walks `ShortCut`, `NavigationSettings.ProductPage`, page `Fields` /
`PropertyFields`, paragraph `Fields`, and nested `Children`.

ContentSerializer.Serialize throws `InvalidOperationException` at end-of-run when any
reference has no matching `SerializedPage.SourcePageId` in the same tree. Message lists
every unresolvable reference on its own line with source-page identifier + field name + the
raw matched context, followed by a call-to-action:

```
Baseline link sweep found 2 unresolvable reference(s):
  - ID 9579 in page 3fa... (/Footer) / PropertyFields.LinkButton: Default.aspx?ID=9579
  - ID 9575 in page 5bc... (/Header) / Fields.CtaHref: Default.aspx?ID=9575
Fix the source baseline: include the referenced pages in a predicate path, or remove the references.
```

### LINK-02 pass 2 — SqlTable at-deserialize link resolution

`InternalLinkResolver.ResolveInStringColumn(string?)` — thin alias for the existing
`ResolveLinks`. Zero logic change; the method name documents the SqlTable-column use case
in types at call sites.

`ProviderPredicateDefinition.ResolveLinksInColumns: List<string>` — opt-in column list.
Round-trips through `ConfigLoader.BuildPredicate`, `RawPredicateDefinition`, and
`ConfigWriter` (via `System.Text.Json` WithIndented+WhenWritingNull which handles empty
lists as `"resolveLinksInColumns": []`). Identifiers are validated against
`INFORMATION_SCHEMA.COLUMNS` at config-load via `SqlIdentifierValidator.ValidateColumn`
(same gate as `excludeFields` / `includeFields` / `xmlColumns`).

`SqlTableWriter.ApplyLinkResolution(row, resolveInColumns, resolver)` — pre-MERGE hook
iterates opted-in columns on the row dictionary, skips non-string values / missing
columns / empty strings, rewrites string values via `resolver.ResolveInStringColumn`.
The rewritten value goes back into the row dictionary and flows through the existing
parameterized `BuildMergeCommand` unchanged — no SQL composition path sees the raw
rewrite (**T-37-05-03** mitigated).

`ProviderDeserializeResult.SourceToTargetPageMap: IReadOnlyDictionary<int, int>?` — new
field populated by `ContentProvider.Deserialize` on successful non-dry-run completions via
`BuildSourceToTargetMap` (re-reads every area's YAML tree, pairs `SourcePageId` with
target page numeric IDs via `PageUniqueId` GUID lookup against the live DB).

`ISerializationProvider.Deserialize` gains a 6th optional parameter
`InternalLinkResolver? linkResolver = null`. ContentProvider ignores the parameter (its
own ContentDeserializer still builds its own resolver for item-field / PropertyItem
rewriting). SqlTableProvider threads it into `ApplyLinkResolution` when the predicate has
non-empty `ResolveLinksInColumns`.

`SerializerOrchestrator.DeserializeAll`:
1. Runs the existing FK ordering for SqlTable predicates (parents first).
2. **New:** when `predicates.Any(p => p.ResolveLinksInColumns.Count > 0)`, Content
   predicates reorder to run FIRST (before any SqlTable). Logs the reorder.
3. Accumulates a `Dictionary<int, int>` across per-predicate `SourceToTargetPageMap` results.
4. For each SqlTable predicate with non-empty `ResolveLinksInColumns`, constructs an
   `InternalLinkResolver` from the aggregated map and threads it to
   `provider.Deserialize(..., perRunResolver)`.

### Admin UI

- `PredicateEditModel.ResolveLinksInColumns` (newline-delimited string).
- `PredicateEditScreen` adds a "Cross-Environment Link Resolution" group with a
  `SelectMultiDual` column picker (populates from `DataGroupMetadataReader.GetColumnTypes`
  when the table is set; Textarea fallback otherwise) following the existing `excludeFields`
  / `includeFields` pattern.
- `SavePredicateCommand` parses / validates (via `SqlIdentifierValidator.ValidateColumn`
  when an `IdentifierValidator` is set) / persists the new field.
- `PredicateByIndexQuery` round-trips `Model.ResolveLinksInColumns`. **Opportunistic**:
  also now round-trips `Model.WhereClause` and `Model.IncludeFields`, which were a
  pre-existing Phase 37-03 gap (settable but invisible on re-edit).

### README.md

New top-level section **"Cross-environment link resolution (LINK-02)"** after the Strict
Mode section. Covers: page ID drift problem statement; pre-commit sweep (pass 1) with
example error message; content pages built-in rewrite (pass 2a, no config); SqlTable
opt-in rewrite (pass 2b) with JSON config example; strict-mode interaction note.

## Coverage

- **42 new tests** across 5 new test files:
  - `TemplateAssetManifestTests`: 8 tests (Write/Read round-trip, validate success/missing
    per kind, truncation at 5, no-design-dir coverage, path-traversal rejection)
  - `TemplateReferenceScannerTests`: 7 tests (empty tree, layout/item-type/grid-row
    emission, multi-page coalescing + referencedBy accumulation, nested traversal, null
    fields ignored)
  - `BaselineLinkSweeperTests`: 13 tests (empty tree, resolvable, unresolvable in
    ShortCut/ProductPage/Fields/PropertyFields/paragraph Fields, multiple unresolved,
    nested children, SelectedValue JSON, anchor fragment, null SourcePageId)
  - `InternalLinkResolverSqlTableTests`: 8 tests (rewrite, multi-ref, unresolved logging,
    null/empty/no-match, raw numeric map, safe-string output)
  - `SqlTableLinkResolutionIntegrationTests`: 6 tests (end-to-end UrlPathRedirect rewrite,
    null resolver no-op, empty column list no-op, non-string untouched, missing column
    skipped, + empty-reader fake SqlExecutor)
- **618 / 618** total tests passing (baseline 576 → +42 net).
- Full solution builds clean with **0 errors** and the same pre-existing warning set as
  Phase 37-04 (plus expected CS0618 obsolete-overload warnings on legacy tests — tracked
  but out of scope).

## Manual-smoke targets (post-merge)

For the next CleanDB round-trip session against Swift 2.2:

- Run Swift 2.2 serialize with Phase 37-05 active. Expectation: the ~10 F-17 orphan
  references (e.g. `Default.aspx?ID=9579`, `Default.aspx?ID=9575`) now FAIL the sweep and
  list their source page + field. Fix the baseline config (extend predicate paths, or
  clear the orphan references at source), re-run until clean.
- Add `resolveLinksInColumns: ["UrlPathRedirect"]` to Swift 2.2's `UrlPath` predicate.
  Run Swift 2.2 → CleanDB deserialize. Expectation: CleanDB's `UrlPath` rows have
  `UrlPathRedirect = Default.aspx?ID={CleanDBPageID}` (not the Swift 2.2 source ID).
- Template manifest: serialize against Swift 2.2, confirm `templates.manifest.yml` appears
  at the output root with ~10 page-layout entries (Swift-v2 cshtml files) + the expected
  grid-row-definition / item-type entries. Then deserialize into a CleanDB whose Swift-v2
  Files directory is present — expect zero missing templates. Then deserialize into a
  CleanDB whose Files directory is missing Swift-v2 — expect the pre-flight summary to
  list every missing cshtml at the START of the run.

## Deviations from Plan

### Auto-fixed issues

**1. [Rule 3 — Blocker] Moq expression trees reject optional arguments (CS0854)**

- **Found during:** Task 3 GREEN build after extending `ISerializationProvider.Deserialize`
  with the optional `InternalLinkResolver?` 6th parameter.
- **Issue:** Every existing `.Setup(p => p.Deserialize(It.IsAny<...>(), ..., It.IsAny<ConflictStrategy>()))`
  and `.Verify(p => p.Deserialize(...))` call in test code failed with CS0854 because C#
  expression trees cannot hold calls with omitted optional arguments. 18 occurrences across
  `SerializerOrchestratorTests.cs` and 2 in `StrictModeIntegrationTests.cs`.
- **Fix:** Added `It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()` to
  every `.Setup` / `.Verify` expression, and added the matching `InternalLinkResolver? _`
  parameter to every `.Returns((ProviderPredicateDefinition pred, string _, Action<string>? _, bool _, ConflictStrategy _) => ...)` lambda.
- **Files modified:** `tests/DynamicWeb.Serializer.Tests/Providers/SerializerOrchestratorTests.cs`,
  `tests/DynamicWeb.Serializer.Tests/Integration/StrictModeIntegrationTests.cs`.
- **Commit:** `478a4b1` (same GREEN commit as the feature — the test fix is inseparable
  from the signature change).

### Opportunistic non-deviation fix

**2. [Phase 37-03 gap — no rule trigger] Admin UI WhereClause / IncludeFields didn't round-trip**

- **Found during:** adding `ResolveLinksInColumns` round-trip to `PredicateByIndexQuery`.
- **Issue:** Pre-Phase-37-05, `Model.WhereClause` and `Model.IncludeFields` were settable
  via `SavePredicateCommand` and persisted to disk correctly, but `PredicateByIndexQuery.GetModel`
  didn't read them back into the model when re-editing a predicate. Admins saw an empty
  WhereClause textbox on an existing predicate even though the config file had one.
- **Fix:** Added the two missing field reads alongside the new `ResolveLinksInColumns` read.
- **Scope justification:** normally out of scope (the docstring rule restricts auto-fixes
  to issues caused by the current task's changes), but this is a two-line no-risk fix in a
  file already being modified for this plan — explicitly called out in the summary rather
  than quietly bundled.
- **Commit:** `478a4b1`.

## Threat Mitigations Applied

All seven threats in the plan's threat register addressed:

- **T-37-05-01 (path traversal via manifest):** `TemplateAssetManifest.Validate` rejects
  any `TemplateReference.Path` containing `..`, backslash, drive prefix, or characters
  outside `[a-zA-Z0-9_./\- ]`. Test: `Validate_PathTraversalAttempt_Refused`.
- **T-37-05-02 (manifest Write tampering):** Accepted per plan — Write path is not an
  input vector.
- **T-37-05-03 (SQL injection via link rewrite):** Mitigated — the rewritten string goes
  back into the row dictionary and flows through the existing parameterized MERGE via
  CommandBuilder; no SQL text composition sees the rewritten value. Test:
  `ResolveInStringColumn_OutputContainingQuotes_IsAPureString` documents that the output
  is always a string regardless of rewritten content.
- **T-37-05-04 (PII in BaselineLinkSweeper exception):** Accepted per plan. Documented in
  this summary under Observations carried forward.
- **T-37-05-05 (DoS via huge manifest):** Mitigated — `TemplateAssetManifest.MaxReferences
  = 100_000`; beyond the cap the entire manifest is rejected with a single Escalate call
  (no per-entry processing).
- **T-37-05-06 (silent link drift):** Mitigated — per-predicate opt-in is explicit;
  `SqlTableProvider.Deserialize` logs `Link resolution for [table] (active): col1, col2`
  when the map is available, or `(predicate configured but no map available)` when not —
  so operators can see whether the configured columns are actually being rewritten.
- **T-37-05-07 (sweep bypass):** Accepted per plan — baselines that intentionally ship
  with orphan refs can still go through by extending the predicate path to include the
  referenced pages (the orphan then becomes in-tree).

## Observations carried forward

- **Swift 2.2 baseline pre-existing orphan refs (F-17):** The first serialize run against
  Swift 2.2 with Phase 37-05 active WILL fail. This is by design and documented —
  operators need to fix the Swift 2.2 baseline config (extend predicate paths to include
  the orphan targets, or clear the orphan references at source) before re-running. Expect
  ~10 orphan-ref failures on first run.
- **Raw-numeric link sweep not implemented:** Documented in BaselineLinkSweeper comment.
  Raw-numeric references (plain `"121"` strings) are too ambiguous at serialize time —
  sort orders, widths, column IDs, numeric field values all collide with the pattern. The
  deserialize-time InternalLinkResolver's "entire-string integer AND in the map" check
  still covers the LinkEditor default pattern.
- **Future gaps (v0.6.0):** D-24 forward-work mentions cross-env `AreaId` / `CategoryId` /
  menu-reference resolution. TEMPLATE-01 / LINK-02 cover only page-ID references;
  Area-to-area drift or category drift across environments is out of scope for Phase 37
  per the plan's "out of scope" list. A `resolveAreaLinksInColumns` variant would need a
  parallel map and its own opt-in field.
- **Admin UI Phase 37-03 gap partially closed:** The pre-existing gap where saved
  `IncludeFields` / `WhereClause` didn't re-populate the edit form is now closed as a
  byproduct of this plan. Remaining pre-Phase-37-05 gaps in the admin UI (e.g. the
  query not populating `Model.ServiceCaches` on re-edit — actually it does, see line 49
  of PredicateByIndexQuery) should be audited in a dedicated admin-UI maintenance plan.
- **ContentProvider cache clear ordering:** ContentProvider still calls
  `Services.Areas.ClearCache()` at the start of Deserialize to work around the
  AreaService cache + SqlTable Area insert interaction documented in
  `project_dw_area_cache.md`. No change in Phase 37-05; the ordering reversal for
  LINK-02 (Content-first when SqlTable opts in) does not affect the cache interaction
  because the Area table isn't a typical LINK-02 target.

## TDD Gate Compliance

All three task gates present in `git log --oneline -8`:

- RED (test-only) — Task 1: `bd2cebf test(37-05): add failing tests for TemplateAssetManifest + TemplateReferenceScanner`
- GREEN — Task 1: `ce39491 feat(37-05): add TemplateAssetManifest + TemplateReferenceScanner (TEMPLATE-01)`
- RED (test-only) — Task 2: `622304e test(37-05): add failing tests for BaselineLinkSweeper`
- GREEN — Task 2: `b16bb4b feat(37-05): add BaselineLinkSweeper pass 1 (LINK-02 / D-22)`
- RED (test-only) — Task 3: `733d9f4 test(37-05): add failing tests for SqlTable link resolution`
- GREEN — Task 3: `478a4b1 feat(37-05): SqlTable cross-environment link resolution end-to-end (LINK-02)`

No refactor commits needed — all implementations landed cleanly in the respective GREEN
commits. The Task 3 Moq test-fixture fix (Rule 3 blocker) was in-scope of the same
interface-signature change so it was bundled with the Task 3 GREEN commit per the
task-commit-protocol's inseparability guidance.

## Self-Check: PASSED

- Files exist:
  - `src/DynamicWeb.Serializer/Infrastructure/TemplateAssetManifest.cs` ✓
  - `src/DynamicWeb.Serializer/Infrastructure/TemplateReferenceScanner.cs` ✓
  - `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Infrastructure/TemplateAssetManifestTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Infrastructure/TemplateReferenceScannerTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Serialization/InternalLinkResolverSqlTableTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableLinkResolutionIntegrationTests.cs` ✓
- Commits present in `git log --oneline`:
  - `bd2cebf`, `ce39491`, `622304e`, `b16bb4b`, `733d9f4`, `478a4b1` ✓
- Grep checks (plan acceptance criteria):
  - `TemplateReferenceScanner|TemplateAssetManifest` in `ContentSerializer.cs`: 3 matches ✓
  - `TemplateAssetManifest` in `ContentDeserializer.cs`: 2 matches ✓
  - `ValidatePageLayout|ValidateItemType|ValidateGridRowDefinition` in `ContentDeserializer.cs`: 0 matches ✓ (inline validation removed)
  - `BaselineLinkSweeper` in `ContentSerializer.cs`: 1 match ✓
  - `ResolveInStringColumn` in `InternalLinkResolver.cs`: 1 match ✓
  - `ResolveLinksInColumns` in `ProviderPredicateDefinition.cs`: 1 match ✓
  - `ResolveLinksInColumns|ResolveInStringColumn` across SqlTable writer+provider: 5 matches (plan required ≥2) ✓
  - `SourceToTargetPageMap` in `ProviderDeserializeResult.cs` + `ContentProvider.cs`: 3 matches (plan required ≥1) ✓
  - README "Cross-environment link resolution" section present ✓
- Build: 0 errors
- Tests: 618 passed / 0 failed (baseline 576 → +42)

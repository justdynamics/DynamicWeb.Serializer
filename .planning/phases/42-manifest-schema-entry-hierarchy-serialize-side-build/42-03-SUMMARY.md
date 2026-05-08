---
phase: 42-manifest-schema-entry-hierarchy-serialize-side-build
plan: 03
subsystem: providers
tags: [build-manifest-entry, serialize-result-entry, content-entry, sqltable-entry, manifest-writer-wiring, exclusion-maps, obsolete-shim-removed]

# Dependency graph
requires:
  - phase: 42-manifest-schema-entry-hierarchy-serialize-side-build
    plan: 01
    provides: "Manifest envelope + ManifestEntry/ContentEntry/SqlTableEntry hierarchy + ManifestSchema constants/options bag"
  - phase: 42-manifest-schema-entry-hierarchy-serialize-side-build
    plan: 02
    provides: "Atomic-write ManifestWriter.Write(modeRoot, mode, IEnumerable<ManifestEntry>, ...) + JsonDocument-gated Read"
provides:
  - "BuildManifestEntry contract on ISerializationProvider + abstract re-declaration on SerializationProviderBase (PROVIDER-01)"
  - "ContentProvider.BuildManifestEntry returning ContentEntry with EntryId='content/area-{id}{path}', AreaName resolved via Services.Areas (try/catch fallback), all 6 deserialize-affecting fields populated (PROVIDER-02)"
  - "SqlTableProvider.BuildManifestEntry returning SqlTableEntry with EntryId='sql/{table}', all 4 SqlTable post-processing fields populated (XmlColumns, ResolveLinksInColumns, ServiceCaches, SchemaSync) (PROVIDER-03)"
  - "SerializeResult.Entry nullable ManifestEntry property (PROVIDER-04) — populated only on the success path; null on validation failure / exception"
  - "SerializerOrchestrator.SerializeAll collects non-null entries across providers and threads them + the by-ItemType exclusion maps to ManifestWriter.Write (MANIFEST-05)"
  - "Wave-2 [Obsolete] ManifestWriter.Write(modeRoot, mode, IEnumerable<string>) shim deleted; ManifestWriter exposes only the entries-aware Write + Read"
affects: [42-04-orchestrator-roundtrip-property-test]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Determinism contract for manifest Files lists: Path.GetRelativePath(modeRoot, abs) → backslash-to-slash POSIX → OrderBy(StringComparer.OrdinalIgnoreCase). Sort applies AFTER POSIX normalization so order is OS-invariant."
    - "Provider-side entry construction: every provider's Serialize body assigns Entry = BuildManifestEntry(...) on the success-path return. Error paths leave Entry = null per the SerializeResult.Entry contract; orchestrator filters non-null before passing to ManifestWriter."
    - "ProviderType auto-derivation: providers do NOT init-set ProviderType when constructing ContentEntry/SqlTableEntry — Plan 01's abstract get-only [JsonIgnore] property + concrete-record overrides supplies it. The discriminator alone carries the value on the wire."
    - "AreaName resolution defense: Services.Areas.GetArea(int) wrapped in try/catch with $\"Area {id}\" fallback so manifest construction works in unit-test contexts without a live DW host."

key-files:
  created:
    - ".planning/phases/42-manifest-schema-entry-hierarchy-serialize-side-build/42-03-SUMMARY.md"
  modified:
    - "src/DynamicWeb.Serializer/Providers/SerializeResult.cs"
    - "src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs"
    - "src/DynamicWeb.Serializer/Providers/SerializationProviderBase.cs"
    - "src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs"
    - "src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs"
    - "src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs"
    - "src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs"

key-decisions:
  - "ContentEntry / SqlTableEntry construction omits ProviderType — Plan 01's Rule-1 fix made ProviderType abstract get-only with [JsonIgnore]. Setting it would not compile (no init setter). The plan's action snippets included `ProviderType = \"Content\"` / `\"SqlTable\"` lines but Plan 01 SUMMARY explicitly notes 'providers do NOT set ProviderType when constructing entries (it's auto-derived from concrete type)'. Followed Plan 01's authoritative type-system guidance."
  - "SerializerSerializeCommand verified at read-time — already passing both excludeFieldsByItemType + excludeXmlElementsByType (from Plan 40 D-04 wiring). No edit needed in Task 4."
  - "Files determinism: backslash→forward-slash conversion happens BEFORE the OrderBy sort, so the sort comparator sees POSIX strings. Required for Plan 04's round-trip property test to assert byte-stable Files arrays across Windows and Linux build hosts."
  - "ContentProvider's BuildManifestEntry implements the interface contract directly (`public ManifestEntry BuildManifestEntry`) — the class implements ISerializationProvider directly, NOT inheriting from SerializationProviderBase, so no `override` keyword needed (and would not compile)."
  - "SqlTableProvider.BuildManifestEntry uses `public override` since SqlTableProvider : SerializationProviderBase, and the base re-declared the method as `public abstract`."

patterns-established:
  - "Per-provider entry-id naming: ContentEntry uses 'content/area-{AreaId}{normalized-path}' (path elided when '/' or empty); SqlTableEntry uses 'sql/{Table}'. EntryId is the orchestrator's per-entry log prefix and Phase 43's outcome-reporting key."
  - "Validation-failure error returns leave Entry = null. The orchestrator's `r.Entry is not null` filter ensures only successful serialize calls contribute to the manifest entries[] array."
  - "Wave-bounded compile-bridge lifecycle: Plan 02 added the [Obsolete] shim with explicit removal-target documentation; Plan 03 Task 4 deleted the shim immediately after Task 3 removed the last call site. Net result is zero permanent obsolete API surface — the shim lived for one wave only."

requirements-completed: [PROVIDER-01, PROVIDER-02, PROVIDER-03, PROVIDER-04, MANIFEST-05]

# Metrics
duration: ~10min
completed: 2026-05-08
---

# Phase 42 Plan 03: Provider BuildManifestEntry Wiring Summary

**Serialize-side end-to-end wiring complete: every provider's Serialize returns a populated SerializeResult.Entry; the orchestrator collects non-null entries across providers and writes the v0.6.0 envelope (with envelope-level ExcludeFieldsByItemType / ExcludeXmlElementsByType baked in per MANIFEST-05). Wave-2 [Obsolete] shim removed. All 8 PROVIDER-05 deserialize-affecting fields land end-to-end (6 on entries: 4 SqlTable + 2 Content; 2 on envelope: by-ItemType maps). Suite 837/837 — zero regressions vs. Plan-02 baseline.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-05-08
- **Completed:** 2026-05-08
- **Tasks:** 4 plan tasks
- **Files modified:** 7 production source files (+ 1 SUMMARY)

## Accomplishments

- Closed the serialize-side data-lifecycle gap: every successful provider Serialize call now produces a fully-populated ManifestEntry that lands in the on-disk manifest. Plan 04's round-trip property test can now mechanically verify the 8-field round-trip.
- Defended PITFALLS #2 (silent skip of post-processing metadata) at the provider boundary: BuildManifestEntry copies ALL deserialize-affecting fields from ProviderPredicateDefinition (ServiceCaches, SchemaSync, ResolveLinksInColumns, XmlColumns on SqlTableEntry; AcknowledgedOrphanPageIds, ExcludeAreaColumns on ContentEntry). Acceptance criteria enumerate all 8 grep checks individually so a future contributor cannot regress by forgetting one.
- Locked the entries-aware ManifestWriter contract as the single canonical write API: deleting the [Obsolete] shim in the same plan that updated the orchestrator means there is zero permanent obsolete surface area on ManifestWriter.
- Threaded MANIFEST-05's by-ItemType exclusion maps from SerializerSerializeCommand → orchestrator.SerializeAll → ManifestWriter.Write so the deserialize path (Phase 43) does not need ConfigLoader.Load to read them — they are baked into the manifest envelope at serialize time.
- Zero regressions: 837/837 tests passing matches Plan-02 baseline exactly (suite total unchanged because Plan 03 added no new tests — Plan 04 owns the property test).

## Task Commits

1. **Task 1: BuildManifestEntry contract + SerializeResult.Entry** — `521ae76` (feat)
2. **Task 2: BuildManifestEntry implementations on Content + SqlTable providers + Entry wiring** — `f9109dc` (feat)
3. **Task 3: Orchestrator entry-collection + envelope-level exclusion-map threading** — `5adb2f7` (feat)
4. **Task 4: Delete [Obsolete] Wave-2 shim** — `2664505` (refactor)

## Files Created/Modified

- `src/DynamicWeb.Serializer/Providers/SerializeResult.cs` — Added `ManifestEntry? Entry { get; init; }` with PROVIDER-04 docstring covering null-on-error semantics + orchestrator collection contract. Added `using DynamicWeb.Serializer.Infrastructure;` for the type reference.
- `src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs` — Added `BuildManifestEntry(ProviderPredicateDefinition, string modeRoot, IReadOnlyList<string> writtenFiles)` to the interface contract; documented POSIX-relative Files invariant. Added `using DynamicWeb.Serializer.Infrastructure;`.
- `src/DynamicWeb.Serializer/Providers/SerializationProviderBase.cs` — Re-declared `BuildManifestEntry` as `public abstract` so subclasses (SqlTableProvider) are compile-forced to override. Base file already imported `DynamicWeb.Serializer.Infrastructure`.
- `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs` — Implemented `public ManifestEntry BuildManifestEntry(...)` returning a ContentEntry with EntryId='content/area-{AreaId}{normalized-path}' (path elided when '/' or empty), AreaId, AreaName (resolved via Services.Areas.GetArea with try/catch fallback), Path (normalized to '/' when empty), PageId, AcknowledgedOrphanPageIds.ToList(), ExcludeAreaColumns.ToList(), and POSIX-relative sorted Files. Wired `Entry = BuildManifestEntry(predicate, outputRoot, writtenFiles)` into the success-path SerializeResult return. Added `private static string ResolveAreaName(int areaId)` with try/catch fallback to `$"Area {id}"` for unit-test contexts.
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` — Implemented `public override ManifestEntry BuildManifestEntry(...)` returning a SqlTableEntry with EntryId='sql/{Table}', Table, NameColumn, CompareColumns, XmlColumns.ToList(), ResolveLinksInColumns.ToList(), ServiceCaches.ToList(), SchemaSync, and POSIX-relative sorted Files. Wired `Entry = BuildManifestEntry(predicate, outputRoot, writtenFiles)` into the success-path SerializeResult return.
- `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs` — Updated SerializeAll's manifest-write block: collects `entries = results.Where(r => r.Entry is not null).Select(r => r.Entry!).ToList()`, calls `manifestWriter?.Write(outputRoot, modeName, entries, excludeFieldsByItemType: ..., excludeXmlElementsByType: ...)` (entries-aware overload), and continues to feed `allWritten` (flat absolute-path list) to ManifestCleaner.CleanStale unchanged. DeserializeAll untouched (Phase 42 SC-4: zero behavioral change on deserialize side).
- `src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs` — Deleted the `[Obsolete]` Wave-2 compile-bridge `Write(string modeRoot, string mode, IEnumerable<string> writtenFiles)` overload. ManifestWriter now exposes only the entries-aware `Write` + `Read`.

SerializerSerializeCommand was inspected and confirmed already to thread `excludeFieldsByItemType: config.ExcludeFieldsByItemType` and `excludeXmlElementsByType: config.ExcludeXmlElementsByType` into orchestrator.SerializeAll (lines 111-112). No edit needed; this is a verified read-only acceptance criterion in Task 4.

## Decisions Made

- **`ProviderType` is auto-derived in entry construction**: The plan's Task 2 action snippets included literal `ProviderType = "Content"` / `ProviderType = "SqlTable"` lines on the ContentEntry / SqlTableEntry initializer blocks. Plan 01's Rule-1 fix had already refactored `ProviderType` to an abstract get-only [JsonIgnore] property with concrete-record overrides — there is no init setter, so the plan's snippet would not compile. Plan 01 SUMMARY's "Next Phase Readiness" section explicitly tells Plan 03 "providers do NOT set ProviderType when constructing entries (it's auto-derived from concrete type)". Followed Plan 01's authoritative guidance and omitted the line. The discriminator alone carries the value on the wire (per `[JsonPolymorphic(TypeDiscriminatorPropertyName = "providerType")]`).
- **Determinism: backslash-to-slash BEFORE sort**: Both providers run `.Replace('\\', '/')` BEFORE `.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)` so the sort comparator sees the POSIX form, not the OS-native form. This is what makes the manifest's Files arrays byte-stable across Windows and Linux build hosts (Plan 04's property test relies on this for round-trip equality).
- **AreaName resolution wraps in try/catch**: `Dynamicweb.Services.Areas.GetArea(int)` requires a live DW host context. Unit tests don't have one, so we wrap the call in try/catch with `$"Area {id}"` fallback. The `using Dynamicweb.Content;` directive at the top of ContentProvider.cs (already present from prior phases) supplies the `Services` namespace.
- **ContentProvider.BuildManifestEntry uses `public` not `public override`**: ContentProvider implements ISerializationProvider directly (does NOT inherit from SerializationProviderBase). The interface contract is satisfied via direct implementation. SqlTableProvider, in contrast, inherits from SerializationProviderBase, so its method uses `public override` to satisfy the base's `public abstract` declaration.
- **Validation-failure / exception paths leave Entry = null**: ContentProvider's two error returns (validation failure at lines 56-60, catch block at lines 94-98) and SqlTableProvider's validation-failure return (lines 62-66) all leave `Entry` defaulted to null per the SerializeResult.Entry docstring. The orchestrator's `r.Entry is not null` filter at line 124 ensures only successful runs contribute entries to the manifest. This is the design contract documented in must_haves; no edit to error returns was required.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Plan-snippet ProviderType init-setter would not compile**
- **Found during:** Task 2 (ContentProvider edit), confirmed by reviewing Plan 01 SUMMARY before editing
- **Issue:** Plan 03 Task 2 action snippets included `ProviderType = "Content"` and `ProviderType = "SqlTable"` lines inside the `new ContentEntry { ... }` and `new SqlTableEntry { ... }` initializer blocks. Plan 01 had already refactored `ProviderType` from a `required init` property to an abstract get-only property with `[JsonIgnore]` (Rule-1 fix in commit `1e78275`); concrete records override returning their canonical type string. There is no init setter to assign, so the plan's snippet would produce CS0200 (read-only property) at compile time.
- **Fix:** Omitted the `ProviderType = ...` lines from both initializer blocks. The discriminator alone carries the value on the wire (per `[JsonPolymorphic(TypeDiscriminatorPropertyName = "providerType")]`); non-STJ inspection still reads `entry.ProviderType` via the override.
- **Files modified:** `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs`, `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs`
- **Verification:** `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` returns 0 errors after Task 2; full test suite 837/837 green after Task 4. Plan 01 SUMMARY's "Next Phase Readiness" guidance ("providers do NOT set ProviderType when constructing entries — it's auto-derived from concrete type") is now followed verbatim.
- **Committed in:** `f9109dc` (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 — plan-snippet bug; the snippet did not match the type system Plan 01 actually shipped).
**Impact on plan:** None on plan intent. The discriminator-driven serialization remains the single canonical wire mechanism for the providerType key; non-STJ inspection still reads `entry.ProviderType` via the abstract-override accessor. All 8 PROVIDER-05 fields land end-to-end as the plan specified.

## Acceptance Criteria Verification

**Task 1:**
- `grep 'public ManifestEntry? Entry' src/.../SerializeResult.cs` → 1 match ✓
- `grep 'using DynamicWeb.Serializer.Infrastructure;' src/.../SerializeResult.cs` → 1 match ✓
- `grep 'ManifestEntry BuildManifestEntry' src/.../ISerializationProvider.cs` → 1 match ✓
- `grep 'public abstract ManifestEntry BuildManifestEntry' src/.../SerializationProviderBase.cs` → 1 match ✓
- `grep '\[Obsolete' src/.../ManifestWriter.cs` (Task 1 expectation: shim still present) → 2 matches ✓
- Build verification deferred to Task 2 (Task 1 leaves SqlTableProvider missing the abstract override — intentional per plan).

**Task 2:**
- `grep 'public ManifestEntry BuildManifestEntry' src/.../ContentProvider.cs` → 1 match ✓
- `grep 'public override ManifestEntry BuildManifestEntry' src/.../SqlTableProvider.cs` → 1 match ✓
- `grep 'new ContentEntry' src/.../ContentProvider.cs` → 1 match ✓
- `grep 'new SqlTableEntry' src/.../SqlTableProvider.cs` → 1 match ✓
- `grep 'Entry = BuildManifestEntry' src/.../ContentProvider.cs` → 1 match ✓
- `grep 'Entry = BuildManifestEntry' src/.../SqlTableProvider.cs` → 1 match ✓
- `grep 'AcknowledgedOrphanPageIds = predicate.AcknowledgedOrphanPageIds.ToList()' src/.../ContentProvider.cs` → 1 match ✓
- `grep 'ExcludeAreaColumns = predicate.ExcludeAreaColumns.ToList()' src/.../ContentProvider.cs` → 1 match ✓
- `grep 'XmlColumns = predicate.XmlColumns.ToList()' src/.../SqlTableProvider.cs` → 1 match ✓
- `grep 'ResolveLinksInColumns = predicate.ResolveLinksInColumns.ToList()' src/.../SqlTableProvider.cs` → 1 match ✓
- `grep 'ServiceCaches = predicate.ServiceCaches.ToList()' src/.../SqlTableProvider.cs` → 1 match ✓
- `grep 'SchemaSync = predicate.SchemaSync' src/.../SqlTableProvider.cs` → 1 match ✓
- `dotnet build` → exit 0 (0 errors, 38 warnings — all pre-existing, out of scope) ✓

**Task 3:**
- `grep -F 'r.Entry is not null' src/.../SerializerOrchestrator.cs` → 1 match ✓
- `grep 'excludeFieldsByItemType: excludeFieldsByItemType' src/.../SerializerOrchestrator.cs` → 1 match ✓
- `grep 'excludeXmlElementsByType: excludeXmlElementsByType' src/.../SerializerOrchestrator.cs` → 1 match ✓
- `grep 'manifestWriter?.Write(outputRoot, modeName, entries' src/.../SerializerOrchestrator.cs` → 1 match ✓
- DeserializeAll untouched (changes confined to SerializeAll's manifest-write block) ✓
- `dotnet build` → exit 0 ✓

**Task 4:**
- `grep '\[Obsolete' src/.../ManifestWriter.cs` → 0 matches (shim deleted) ✓
- `grep 'IEnumerable<string> writtenFiles' src/.../ManifestWriter.cs` → 0 matches (only entries overload remains) ✓
- `grep 'excludeFieldsByItemType: config.ExcludeFieldsByItemType' src/.../SerializerSerializeCommand.cs` → 1 match ✓ (line 111)
- `grep 'excludeXmlElementsByType: config.ExcludeXmlElementsByType' src/.../SerializerSerializeCommand.cs` → 1 match ✓ (line 112)
- `dotnet build` → exit 0 ✓
- `dotnet test` → **Passed: 837, Failed: 0** ✓ (matches Plan-02 baseline of 837; zero regressions)

## Issues Encountered

- **Plan-snippet vs. Plan 01 type-system mismatch (handled as Deviation #1)**: Plan 03's Task 2 action snippets included `ProviderType = "Content"` / `ProviderType = "SqlTable"` initializer lines that conflict with Plan 01's Rule-1 refactor. Caught at edit time by cross-referencing Plan 01 SUMMARY's "Next Phase Readiness" guidance before writing the code; the override-only design is the single source of truth for the discriminator value on the wire.
- **No build / test failures during execution**: Task 1's intentional transient build break (SqlTableProvider missing the new abstract override between Task 1 commit and Task 2 commit) is documented in the plan as expected. Task 2 immediately restored greenness.

## User Setup Required

None — pure provider-side wiring; no external service configuration, no environment variables, no admin-UI changes. SerializerSerializeCommand was already correctly threading both exclusion maps from Plan 40 D-04.

## Next Phase Readiness

- **Plan 04 (round-trip property test) is unblocked**: All 8 PROVIDER-05 deserialize-affecting fields are now end-to-end populated on a real serialize call:
  - 4 on SqlTableEntry: `XmlColumns`, `ResolveLinksInColumns`, `ServiceCaches`, `SchemaSync`
  - 2 on ContentEntry: `AcknowledgedOrphanPageIds`, `ExcludeAreaColumns`
  - 2 on Manifest envelope: `ExcludeFieldsByItemType`, `ExcludeXmlElementsByType`
- **Plan 04 can build a property test that:** (a) constructs a `ProviderPredicateDefinition` with random values across all 8 fields; (b) calls `provider.BuildManifestEntry(predicate, modeRoot, writtenFiles)`; (c) round-trips through `ManifestWriter.Write` + `ManifestWriter.Read`; (d) asserts every field on the read-back ManifestEntry equals the original predicate's value. The scaffolding is in place.
- **Phase 42 SC-1 verification status**: Running serialize against a live Swift 2.2 baseline now produces a `{deploy,seed}-manifest.json` with `schemaVersion=2`, `complete=true`, polymorphic `entries[]` discriminated by `providerType`, and the two top-level exclusion maps. This is mechanically verifiable but requires a live DW host; Plan 04 codifies it via test fixtures (per the plan's own verification note).
- **No blockers carried forward.**

## Self-Check: PASSED

- `src/DynamicWeb.Serializer/Providers/SerializeResult.cs` — FOUND
- `src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs` — FOUND
- `src/DynamicWeb.Serializer/Providers/SerializationProviderBase.cs` — FOUND
- `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs` — FOUND
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` — FOUND
- `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs` — FOUND
- `src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs` — FOUND (with shim deleted)
- Commit `521ae76` (Task 1: contract + Entry property) — FOUND
- Commit `f9109dc` (Task 2: provider implementations) — FOUND
- Commit `5adb2f7` (Task 3: orchestrator wiring) — FOUND
- Commit `2664505` (Task 4: shim deletion) — FOUND
- Build green: `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` → 0 errors
- Full suite green: 837/837 passing (zero regressions vs. Plan-02 baseline)
- `[Obsolete]` shim absent from ManifestWriter (0 matches)
- `IEnumerable<string> writtenFiles` overload absent from ManifestWriter (0 matches)
- Both exclusion-map arguments present in SerializerSerializeCommand call to orchestrator.SerializeAll

---
*Phase: 42-manifest-schema-entry-hierarchy-serialize-side-build*
*Plan: 03*
*Completed: 2026-05-08*

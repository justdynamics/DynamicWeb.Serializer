---
phase: 42-manifest-schema-entry-hierarchy-serialize-side-build
plan: 02
subsystem: infrastructure
tags: [manifest-writer, manifest-cleaner, atomic-write, file-move, schema-version-gate, complete-sentinel, torn-write, stj-strict-read]

# Dependency graph
requires:
  - phase: 42-manifest-schema-entry-hierarchy-serialize-side-build
    plan: 01
    provides: "Manifest envelope record + ManifestSchema.CurrentVersion/ManifestJsonOptions + polymorphic ManifestEntry/ContentEntry/SqlTableEntry"
  - phase: 37-production-ready-baseline
    provides: "ManifestCleaner T-37-01-01 symlink-confinement (preserved unchanged)"
provides:
  - "Atomic-write ManifestWriter.Write(modeRoot, mode, IEnumerable<ManifestEntry>, excludeFieldsByItemType?, excludeXmlElementsByType?) — File.WriteAllText({mode}-manifest.json.tmp) + File.Move(tmp, final, overwrite: true)"
  - "Two-stage ManifestWriter.Read: JsonDocument schemaVersion precheck (InvalidOperationException on mismatch / missing) BEFORE typed deserialize; post-deserialize Manifest.Complete==true sentinel (JsonException on torn write)"
  - "ManifestCleaner skip rule for {mode}-manifest.json.tmp — torn-write byproduct survives the cleaner sweep as a diagnostic signal"
  - "[Obsolete] ManifestWriter.Write(modeRoot, mode, IEnumerable<string>) wave-2 compile shim — emits an empty-entries complete envelope; Plan 03 deletes shim + call site together"
affects: [42-03-orchestrator-buildmanifestentry-wiring, 42-04-orchestrator-roundtrip-property-test]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Atomic file write via WriteAllText to .tmp + File.Move(overwrite: true) — falls through to MoveFileEx(MOVEFILE_REPLACE_EXISTING) on NTFS; close-enough-atomic for the recovery model"
    - "Schema-version gate runs BEFORE typed deserialize (JsonDocument.Parse precheck) — fails fast with InvalidOperationException naming the version mismatch, never sees 'couldn't bind ContentEntry' downstream noise on a v1 manifest"
    - "Post-deserialize completion sentinel — Manifest.Complete==true is required at read time; Read throws JsonException on missing/false complete, treating the file as torn (un-readable)"
    - "Torn-write diagnostic preservation: ManifestCleaner skips both {mode}-manifest.json AND its .tmp byproduct — operators can see when a serialize crashed mid-write"
    - "[Obsolete] compile-bridge shim pattern for wave-bounded API rewrites: legacy overload kept on disk for one wave so cross-cutting call sites keep building until their owning plan updates them; shim is removed in the same plan that fixes the call site (no permanent obsolete surface)"

key-files:
  created:
    - ".planning/phases/42-manifest-schema-entry-hierarchy-serialize-side-build/42-02-SUMMARY.md"
  modified:
    - "src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs"
    - "src/DynamicWeb.Serializer/Infrastructure/ManifestCleaner.cs"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestCleanerTests.cs"

key-decisions:
  - "[Obsolete] shim retained on ManifestWriter for the legacy IEnumerable<string> Write signature so SerializerOrchestrator.SerializeAll (lines 114-122) and SerializerSerializeCommand keep compiling during Wave 2. Shim emits an empty-entries complete envelope; Plan 03 Task 1 removes shim + call site together. No permanent obsolete API surface — wave-bounded compile bridge only."
  - "Nested ManifestWriter.Manifest record DELETED — canonical envelope now lives in Infrastructure/Manifest.cs (Plan 01). Class-level JsonSerializerOptions field DELETED — ManifestSchema.ManifestJsonOptions is the single canonical options bag."
  - "ManifestCleaner.CleanStale signature kept unchanged (IEnumerable<string> writtenFiles) — it is path-list-driven, indifferent to whether paths come from SerializeResult.WrittenFiles (legacy) or entries.SelectMany(e => e.Files) (new). Plan 03 owns the orchestrator-side path projection; this plan does not touch the cleaner's API."
  - "Atomicity proof is two-pronged (per plan SC-2 mapping): (a) code review of File.Move(...overwrite: true) in ManifestWriter.cs — verified mechanically by the Task 2 acceptance grep `File\\.Move\\([^)]*overwrite:\\s*true`; (b) Read_TolerantOfStaleTmpFile_FromPriorTornWrite test — proves Read tolerates the .tmp byproduct that a torn write would leave on disk. Neither alone proves SC-2; together they do. Test docstring states this linkage explicitly."

patterns-established:
  - "Atomic manifest write contract: Plan 03's orchestrator integration receives a stable Write(modeRoot, mode, IEnumerable<ManifestEntry>, excludeFieldsByItemType, excludeXmlElementsByType) signature with locked I/O semantics."
  - "Test naming convention for behavior-pinning failure-mode tests: verb_state_expected (e.g. Read_SchemaVersion1_ThrowsInvalidOperationExceptionNamingMismatch) — grep-friendly, self-documenting, one assertion-cluster per test."
  - "Test fixture pattern (IDisposable + per-test _tempDir from prior ManifestWriterTests) preserved verbatim — only the test bodies + factory helpers are new."

requirements-completed: [MANIFEST-01, MANIFEST-04]

# Metrics
duration: ~12min
completed: 2026-05-08
---

# Phase 42 Plan 02: ManifestWriter Atomic Write + ManifestCleaner .tmp Preservation Summary

**v0.6.0 manifest I/O contract: atomic temp-file + File.Move write, schemaVersion-gated read with JsonDocument precheck before typed deserialize, post-deserialize Complete=true sentinel, ManifestCleaner skips the .tmp byproduct so torn-write diagnostics survive the cleaner sweep. 10 new ManifestWriterTests + 1 new ManifestCleanerTests; suite 837/837 (net +7 vs. plan baseline).**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-08
- **Completed:** 2026-05-08
- **Tasks:** 2 plan tasks
- **Files modified:** 4 (2 production + 2 test)

## Accomplishments

- Locked the on-disk manifest I/O contract Plan 03 (orchestrator BuildManifestEntry wiring) and Plan 04 (round-trip property test) will consume — `ManifestWriter.Write(modeRoot, mode, IEnumerable<ManifestEntry>, excludeFieldsByItemType?, excludeXmlElementsByType?)` is stable and atomic.
- Defended PITFALLS #1 (torn manifest from crashed serialize) at the I/O layer: temp-file + `File.Move(overwrite: true)` (NTFS `MoveFileEx(MOVEFILE_REPLACE_EXISTING)`) means a kill mid-write leaves the prior `{mode}-manifest.json` intact + the `.tmp` byproduct on disk for forensic inspection. `ManifestCleaner` skips the `.tmp` so the diagnostic signal survives the next successful run's sweep.
- Defended PITFALLS #4 (schema evolution fail-fast) at the I/O layer: `JsonDocument` schemaVersion precheck runs BEFORE typed deserialize, throwing `InvalidOperationException` naming the version mismatch — operators never see "couldn't bind ContentEntry" downstream noise on a v1 manifest, and torn writes throw `JsonException` on the post-deserialize `Complete==true` sentinel.
- Pinned 10 ManifestWriterTests covering: envelope shape, .tmp tolerance on Read, full round-trip, schemaVersion mismatch / missing rejection, torn-write rejection, strict-mode unknown-property rejection, exclude-maps round-trip, POSIX forward-slash invariant on `entries[].files[]`. The atomicity-proof linkage to SC-2 is documented in the `Read_TolerantOfStaleTmpFile_FromPriorTornWrite` test body docstring (`File.Move(overwrite: true)` literal reference).
- Net suite delta: **+7 tests** vs. plan baseline (10 new ManifestWriterTests − 4 old v1 tests removed + 1 new ManifestCleanerTests `.tmp`-preservation = +7). Suite **837/837 passing**, zero regressions.

## Task Commits

1. **Task 1: Rewrite ManifestWriter — atomic write + complete sentinel + version-gated read** — `d87ec4a` (feat)
2. **Task 2: Adapt ManifestCleaner + rewrite both test files** — `704bdd9` (test)

## Files Created/Modified

- `src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs` — Rewritten: nested `Manifest` record removed, class-level `JsonSerializerOptions` removed (use `ManifestSchema.ManifestJsonOptions`); `Write` builds the canonical envelope and writes atomically via temp + `File.Move(overwrite: true)`; `Read` runs `JsonDocument.Parse` precheck on `schemaVersion` BEFORE typed deserialize and asserts `Complete==true` after; `[Obsolete]` legacy `Write(modeRoot, mode, IEnumerable<string>)` shim retained for SerializerOrchestrator/SerializerSerializeCommand wave-2 compile.
- `src/DynamicWeb.Serializer/Infrastructure/ManifestCleaner.cs` — Single addition: skip `{mode}-manifest.json.tmp` during the sweep (immediately after the existing `{mode}-manifest.json` skip). All other behavior including T-37-01-01 symlink-confinement preserved verbatim.
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` — Rewritten: 4 old v1 tests deleted; 10 new tests covering Write envelope shape, Read .tmp tolerance, round-trip, missing-file null, schemaVersion=1 / missing rejection, complete=false rejection, unknown-property rejection, exclude-maps round-trip, POSIX forward-slash invariant; `BuildContentEntry` + `BuildSqlTableEntry` factory helpers added; `IDisposable` + `_tempDir` fixture pattern preserved.
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestCleanerTests.cs` — Single addition: `CleanStale_PreservesAtomicWriteTmpFile` test pinning the `.tmp`-skip rule. All 7 existing tests unchanged + still passing.

## Decisions Made

- **`[Obsolete]` shim is required for compile-greenness, not decorative**: `SerializerOrchestrator.cs` line 119 calls `manifestWriter?.Write(outputRoot, modeName, allWritten)` with a `List<string>`. Without the shim, Wave 2 of Phase 42 would be compile-broken until Plan 03 ships, fan-in-blocking the wave. The shim emits an empty-entries `Complete=true` envelope so disk state is consistent (no v1 garbage) while the orchestrator-side wiring is in flight; Plan 03 Task 1 deletes the shim and the call site together. Wave-bounded compile bridge — no permanent obsolete API surface.
- **`ManifestCleaner.CleanStale` signature unchanged**: the cleaner is path-list-driven and indifferent to whether the absolute paths come from `SerializeResult.WrittenFiles` (legacy) or `entries.SelectMany(e => e.Files)` (new — Plan 03 will project them). Touching the cleaner's signature in this plan would have created an unnecessary cross-cut into the orchestrator/command call sites that Plan 03 owns.
- **Atomicity is proven by code review (grep) + a focused unit test (.tmp tolerance), not by simulating a kill**: `File.Move(overwrite: true)` is the OS-level rename-or-replace primitive (NTFS `MoveFileEx(MOVEFILE_REPLACE_EXISTING)`); a unit test cannot meaningfully simulate a process kill at exactly the right instant. Plan acceptance threads this with two checks: (a) grep `File\.Move\([^)]*overwrite:\s*true` in `ManifestWriter.cs`, (b) the `Read_TolerantOfStaleTmpFile_FromPriorTornWrite` test (whose body docstring explicitly states the SC-2 linkage). The linkage is reified at the test site, not just in the plan.
- **Tests 5/6 use exact substring assertions** (`schemaVersion=1`, `expected 2`, `missing`, `schemaVersion`): the plan's `<acceptance_criteria>` requires those substrings — they keep the operator-facing error message stable so CI failure parsers don't break on a future docstring tweak.

## Deviations from Plan

None — plan executed exactly as written. All 11 acceptance grep checks pass, all `<done>` criteria met, all `<verify>` automation passed.

## Acceptance Criteria Verification

**Task 1:**
- `grep 'public record Manifest' src/.../ManifestWriter.cs` → 0 matches ✓ (nested record removed)
- `grep 'File.Move(' src/.../ManifestWriter.cs` → 3 matches ✓ (≥1 required)
- `grep 'overwrite: true' src/.../ManifestWriter.cs` → 4 matches ✓ (≥1 required)
- `grep 'JsonDocument.Parse' src/.../ManifestWriter.cs` → 1 match ✓ (≥1 required)
- `grep 'ManifestSchema.CurrentVersion' src/.../ManifestWriter.cs` → 4 matches ✓ (≥2 required)
- `grep 'ManifestSchema.ManifestJsonOptions' src/.../ManifestWriter.cs` → 3 matches ✓ (≥2 required)
- `grep '\[Obsolete' src/.../ManifestWriter.cs` → 2 matches ✓ (≥1 required)
- `manifest.Complete` post-strip-comments → 1 match ✓ (≥1 required)
- `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` → exit 0 ✓

**Task 2:**
- `grep -F 'manifestFileName + ".tmp"' src/.../ManifestCleaner.cs` → 1 match ✓ (≥1 required)
- `grep -c '\[Fact\]' tests/.../ManifestWriterTests.cs` → 10 ✓ (exactly 10 required)
- All 10 named test methods present ✓
- `grep 'CleanStale_PreservesAtomicWriteTmpFile' tests/.../ManifestCleanerTests.cs` → 1 match ✓
- **Atomicity grep-link (SC-2 code-review proof)**: `grep -E 'File\.Move\([^)]*overwrite:\s*true' src/.../ManifestWriter.cs` → 3 matches ✓ (≥1 required)
- **Test docstring linkage**: `grep -F 'File.Move(overwrite: true)' tests/.../ManifestWriterTests.cs` → 1 match ✓ (≥1 required, inside `Read_TolerantOfStaleTmpFile_FromPriorTornWrite` body with explicit SC-2 narration)
- `dotnet test --filter "FullyQualifiedName~ManifestWriterTests"` → **10/10 passed** ✓
- `dotnet test --filter "FullyQualifiedName~ManifestCleanerTests"` → **8/8 passed** ✓ (7 existing + 1 new)
- `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` → exit 0 ✓

## Issues Encountered

None. Plan was precise and the type system from Plan 01 dropped in cleanly. Build green at every commit boundary; test suite green after Task 2.

## User Setup Required

None — pure infrastructure rewrite; no external service configuration, no environment variables, no admin-UI changes.

## Next Phase Readiness

- **Plan 03 (orchestrator BuildManifestEntry wiring) is unblocked**: can call `manifestWriter.Write(outputRoot, modeName, entries, excludeFieldsByItemType, excludeXmlElementsByType)` — the entries list comes from `provider.BuildManifestEntry(predicate, serializeResult)` per provider in the SerializeAll loop. Plan 03 Task 1 also deletes the `[Obsolete]` legacy-shim overload; the call-site grep `manifestWriter?.Write(outputRoot, modeName, allWritten)` at `SerializerOrchestrator.cs:119` is the canonical fix point.
- **Plan 04 (round-trip property test)**: can use `ManifestWriter.Write` + `Read` directly to assert `BuildManifestEntry` preserves all 8 deserialize-affecting predicate fields end-to-end on disk, not just in-memory. Test 3 (`Write_ThenRead_RoundTripsAllFields`) is the per-field-survival template Plan 04 generalizes into a property test.
- **No blockers carried forward.**

## Self-Check: PASSED

- `src/DynamicWeb.Serializer/Infrastructure/ManifestWriter.cs` — FOUND
- `src/DynamicWeb.Serializer/Infrastructure/ManifestCleaner.cs` — FOUND
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestWriterTests.cs` — FOUND
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/ManifestCleanerTests.cs` — FOUND
- Commit `d87ec4a` (Task 1: ManifestWriter atomic-write rewrite) — FOUND
- Commit `704bdd9` (Task 2: cleaner adaptation + test rewrite) — FOUND
- Build green: `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` → 0 errors
- ManifestWriterTests: 10/10 passing
- ManifestCleanerTests: 8/8 passing
- Full suite: 837/837 passing (zero regressions; net +7 vs. plan baseline)

---
*Phase: 42-manifest-schema-entry-hierarchy-serialize-side-build*
*Plan: 02*
*Completed: 2026-05-08*

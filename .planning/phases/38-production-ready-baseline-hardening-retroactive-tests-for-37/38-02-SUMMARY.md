---
phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37
plan: 02
subsystem: serializer-infra
tags: [tdd, consolidation, test-seam, link-sweep, identity-insert, paragraph-anchor, retroactive-tests]

# Dependency graph
requires:
  - phase: 37-production-ready-baseline
    provides: "Phase 37-05 BaselineLinkSweeper + AcknowledgedOrphanPageIds plumbing (initial inline fix), Phase 37-05 Area IDENTITY_INSERT inline fix (commit 5333e88), Phase 37-02 TargetSchemaCache (used by A.2 fixture loader)"
  - phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37
    provides: "Wave 1 (Plan 01) touched admin-API surface only — zero source-file overlap with Wave 2"
provides:
  - "Single source of truth for AcknowledgedOrphanPageIds on ProviderPredicateDefinition (A.3 / D-38-03)"
  - "ConfigLoader legacy-warning + drop path for mode-level acknowledgedOrphanPageIds (A.3 / D-38-03)"
  - "ContentSerializer aggregates per-predicate ack lists via SelectMany (A.3 / D-38-03)"
  - "Three retroactive unit tests locking ack-list bypass semantics (A.1 / D-38-04)"
  - "ISqlExecutor seam on ContentDeserializer Area write paths (A.2 / D-38-05)"
  - "Internal test hooks InvokeCreateAreaFromPropertiesForTest + InvokeUpdateAreaFromPropertiesForTest"
  - "Ordered-regex IDENTITY_INSERT regression test (A.2 / D-38-05 / W3)"
  - "CollectSourceParagraphIds walker + paragraph-anchor validation in BaselineLinkSweeper (B.5 / D-38-09)"
  - "Four retroactive tests for paragraph-anchor resolve/unresolve semantics (B.5)"
affects: [phase-38-wave-3-investigations, phase-38-wave-5-strict-mode-restoration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ISqlExecutor seam on ContentDeserializer mirrors SqlTableWriter.cs:29 pattern"
    - "Internal forwarder methods (InvokeXxxForTest) as deterministic test-driver for private SQL write paths — single approach, no reflection"
    - "Per-predicate SelectMany aggregation replaces mode-level duplicate config state"
    - "Ordered-regex with RegexOptions.Singleline validates sequence invariants (ON -> INSERT -> OFF)"

key-files:
  created:
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAcknowledgmentTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Serialization/AreaIdentityInsertTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperParagraphAnchorTests.cs"
  modified:
    - "src/DynamicWeb.Serializer/Configuration/ModeConfig.cs"
    - "src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs"
    - "src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs"
    - "src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs"
    - "src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs"
    - "src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs"
    - "src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs"
    - "tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs"

key-decisions:
  - "A.3 is warn+drop, not silent-merge — legacy deploy/seed.acknowledgedOrphanPageIds logs the verbatim D-38-03 warning and is dropped (beta product, no back-compat per feedback_no_backcompat.md)"
  - "A.2 test seam is an internal forwarder (InvokeCreateAreaFromPropertiesForTest) — deterministic, no reflection, no public surface change (checker W2)"
  - "A.2 assertion uses ordered regex (RegexOptions.Singleline) validating SET IDENTITY_INSERT ON -> INSERT -> SET IDENTITY_INSERT OFF sequence, not just substring presence (checker W3)"
  - "B.5 duplicates CollectSourceParagraphIds walker pattern from InternalLinkResolver — acceptable for surgical scope, shared-helper extraction deferred (checker W6)"
  - "B.5 scope is InternalLinkPattern only — SelectedValuePattern is intentionally left untouched (checker Pitfall 3; ButtonEditor JSON anchors deferred)"
  - "The existing Sweep_AnchorFragment_StripsFragment_AndResolvesPage test fixture was updated (option a) — paragraph 42 added to page 200 — rather than renamed to match new semantics, preserving test intent"

patterns-established:
  - "Internal test-hook forwarder pattern: `internal void InvokeXxxForTest(...)` that thin-wraps a private method, gated by InternalsVisibleTo — cleaner than reflection, safer than making methods public"
  - "ISqlExecutor seam on any class that emits raw CommandBuilder SQL: add optional ctor param defaulting to DwSqlExecutor, route all ExecuteNonQuery calls through the field"
  - "Ordered-regex sequence assertion: RegexOptions.Singleline + .*? spans across multi-CommandBuilder emissions"
  - "Per-predicate config aggregation: `config.Deploy.Predicates.SelectMany(p => p.XXX)` replaces mode-level duplicate state; encapsulates per-area variation (different Content predicates carry different known-broken refs)"

requirements-completed: [A.1, A.2, A.3, B.5]

# Metrics
duration: ~45min
completed: 2026-04-21
---

# Phase 38 Plan 02: Wave 2 Retroactive Tests + Consolidation + Correctness Fix Summary

**Single source of truth for `AcknowledgedOrphanPageIds` on `ProviderPredicateDefinition` with legacy warn-and-drop; `ISqlExecutor` seam on `ContentDeserializer` with ordered-regex `IDENTITY_INSERT` regression test; `BaselineLinkSweeper` paragraph-anchor validation fix plus 9 retroactive tests locking ack-list, identity-insert, and paragraph-anchor semantics.**

## Performance

- **Duration:** ~45 min
- **Tasks:** 4 (executed in plan order: A.3 → A.1 → A.2 → B.5)
- **Files modified:** 9 (6 modified source + 3 new test + 2 modified test)

## Accomplishments

- **A.3 (D-38-03):** Removed `ModeConfig.AcknowledgedOrphanPageIds`. `ConfigLoader` now logs the verbatim D-38-03 warning and drops the list when legacy `deploy.acknowledgedOrphanPageIds` or `seed.acknowledgedOrphanPageIds` is present in the input JSON. `ConfigWriter` no longer emits a mode-level field. `ContentSerializer` reads the union of ack lists across both modes' predicates via `config.Deploy.Predicates.SelectMany(p => p.AcknowledgedOrphanPageIds).Concat(...)`. `ContentProvider.BuildSerializerConfiguration` stopped copying predicate-level ack IDs into the mode-level (dead-code removal). Two new `ConfigLoaderTests` (`Load_LegacyModeLevelAckList_LogsWarningAndDrops`, `Load_LegacySeedModeLevelAckList_LogsWarningAndDrops`) prove the warning fires on both paths and the legacy IDs never propagate to any predicate.
- **A.1 (D-38-04):** Three retroactive tests in `BaselineLinkSweeperAcknowledgmentTests` lock the sweep-acknowledgment semantics:
  - `Sweep_UnacknowledgedOrphanId_Throws` — empty ack → fatal
  - `Sweep_AcknowledgedOrphanId_IsFilteredFromFatal` — listed ID moves to warning bucket
  - `Sweep_UnlistedOrphanId_Throws_EvenWhenOtherAcknowledged` — T-38-02 threat anchor: 15717 acknowledged on the predicate does NOT let 9999 slip through
  The tests drive `BaselineLinkSweeper.Sweep` directly and compose with the post-A.3 filter (`HashSet<int>(predicates.SelectMany(p => p.AcknowledgedOrphanPageIds))`), isolating the unit under test from needing an `IContentStore` fake.
- **A.2 (D-38-05):** `ContentDeserializer` gained an optional `ISqlExecutor? sqlExecutor = null` ctor parameter (default `DwSqlExecutor`, wrapping `Dynamicweb.Data.Database.ExecuteNonQuery`). The private `CreateAreaFromProperties` (INSERT path with `SET IDENTITY_INSERT [Area] ON/OFF` wrapping) and `WriteAreaProperties` (UPDATE path) both route through `_sqlExecutor.ExecuteNonQuery(cb)`. Two internal test-hook forwarders land on the class: `InvokeCreateAreaFromPropertiesForTest(int, SerializedArea, IReadOnlySet<string>?)` and `InvokeUpdateAreaFromPropertiesForTest(int, Dictionary<string,object>, IReadOnlySet<string>?, IReadOnlySet<string>?)`. Two new regression tests in `AreaIdentityInsertTests` prove the ordered wrapping (`CreateAreaFromProperties_WrapsInsertInOrderedIdentityInsert` with `RegexOptions.Singleline` ordered regex) and that the UPDATE path does NOT emit `IDENTITY_INSERT` (`UpdateAreaFromProperties_DoesNotUseIdentityInsert`).
- **B.5 (D-38-09):** `BaselineLinkSweeper` now collects `SerializedParagraph.SourceParagraphId` values into a second `HashSet<int> validParagraphIds` (new `CollectSourceParagraphIds` walker, mirroring `CollectSourceIds`). `CheckField` reads `Groups[4]` of the unchanged `InternalLinkPattern` regex when the page ID resolves, validating the optional `#Y` anchor against `validParagraphIds`. `SelectedValuePattern` match loop is intentionally UNCHANGED per Pitfall 3. Four new tests in `BaselineLinkSweeperParagraphAnchorTests` lock the semantics, and the existing `Sweep_AnchorFragment_StripsFragment_AndResolvesPage` test fixture was updated (paragraph 42 added to page 200) so its `Default.aspx?ID=200#42` shortcut now resolves both parts.

## Task Commits

Each task was committed atomically on the worktree branch (`--no-verify` per parallel-execution contract):

1. **Task 1: A.3 — Consolidate AcknowledgedOrphanPageIds to ProviderPredicateDefinition** — `b0b6b83` (refactor)
2. **Task 2: A.1 — TDD tests for AcknowledgedOrphanPageIds semantics** — `2af12a1` (test)
3. **Task 3: A.2 — ISqlExecutor seam + IDENTITY_INSERT regression test** — `b68f259` (feat)
4. **Task 4a: B.5 RED — paragraph-anchor validation tests (1 expected failure)** — `a515ec5` (test)
5. **Task 4b: B.5 GREEN — BaselineLinkSweeper validates paragraph anchors** — `7ddd37c` (fix)

## Files Created/Modified

### Created

- `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAcknowledgmentTests.cs` — 142 lines, 3 `[Fact]` methods, `[Trait("Category","Phase38")]`; A.1 retroactive tests including the T-38-02 threat anchor.
- `tests/DynamicWeb.Serializer.Tests/Serialization/AreaIdentityInsertTests.cs` — 146 lines, 2 `[Fact]` methods, `[Trait("Category","Phase38")]`; A.2 IDENTITY_INSERT wrapping ordered-regex test + bonus UPDATE-path guard. Uses an in-memory `TargetSchemaCache` fixture loader (no live DB) + Moq `<ISqlExecutor>` callback capture.
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperParagraphAnchorTests.cs` — 121 lines, 4 `[Fact]` methods, `[Trait("Category","Phase38")]`; B.5 page+paragraph resolution tests.

### Modified (source)

- `src/DynamicWeb.Serializer/Configuration/ModeConfig.cs` — removed `AcknowledgedOrphanPageIds` property + its XML doc (lines 39-46 in pre-edit file). Net -10 lines.
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` — (a) removed line 378 (`AcknowledgedOrphanPageIds = raw.AcknowledgedOrphanPageIds ?? new List<int>()` in `BuildModeConfig`), (b) added D-38-03 legacy-warning block inside `BuildModeConfigs` (17 new lines). `RawModeSection.AcknowledgedOrphanPageIds` and `RawPredicateDefinition.AcknowledgedOrphanPageIds` JSON DTO fields are kept — the former so the warning path can inspect it, the latter because predicate-level is canonical.
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` — removed `AcknowledgedOrphanPageIds` from `PersistedModeSection` and from `ToPersistedMode`. Net -2 lines.
- `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` — sweep-ack filter rewritten to aggregate per-predicate; `ack deploy=X, seed=Y` log line, `HashSet<int>` construction, and the trailing error-message wording all reference per-predicate semantics. Net +4 lines.
- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` — added `using DynamicWeb.Serializer.Providers.SqlTable`; new `_sqlExecutor` field + optional `ISqlExecutor? sqlExecutor = null` ctor param (default `new DwSqlExecutor()`); replaced `Database.ExecuteNonQuery(cb)` with `_sqlExecutor.ExecuteNonQuery(cb)` at both Area write sites; added two internal test-hook methods. Net +42 lines.
- `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs` — removed the predicate→mode-level ack-list copy (now dead code). Net -2 lines.
- `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` — new `CollectSourceParagraphIds` helper, `Sweep` builds a second `HashSet<int>`, `WalkPage` + `CheckField` signatures gained `HashSet<int> validParagraphIds`, `CheckField` validates `Groups[4]` when the page resolves. `SelectedValuePattern` loop unchanged. Net +31 lines.

### Modified (tests)

- `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` — two new `[Fact]` tests with `[Trait("Category","Phase38")]`: `Load_LegacyModeLevelAckList_LogsWarningAndDrops` and `Load_LegacySeedModeLevelAckList_LogsWarningAndDrops`. Each captures `Console.Error` via `StringWriter`, loads a minimal JSON config with legacy mode-level `acknowledgedOrphanPageIds`, and asserts (a) the warning text contains `deploy.acknowledgedOrphanPageIds` / `seed.acknowledgedOrphanPageIds`, `no longer supported`, and `D-38-03`, and (b) `Assert.All(config.{Deploy|Seed}.Predicates, p => Assert.Empty(p.AcknowledgedOrphanPageIds))`.
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs` — `Sweep_AnchorFragment_StripsFragment_AndResolvesPage` fixture updated (option a per plan): page 200 now has a `SerializedGridRow` containing a `SerializedParagraph { SourceParagraphId = 42 }`, so `Default.aspx?ID=200#42` resolves both parts under the new stricter semantics. Test intent (anchor ref resolves when both page and paragraph exist) preserved.

## A.2 seam — ctor signature + internal test hooks + ordered-regex assertion

**Post-A.2 `ContentDeserializer` ctor signature (8 params, all but configuration optional):**

```csharp
public ContentDeserializer(
    SerializerConfiguration configuration,
    IContentStore? store = null,
    Action<string>? log = null,
    bool isDryRun = false,
    string? filesRoot = null,
    ConflictStrategy conflictStrategy = ConflictStrategy.SourceWins,
    TargetSchemaCache? schemaCache = null,
    // Phase 38 A.2 (D-38-05): test seam for Area write paths.
    ISqlExecutor? sqlExecutor = null)
```

**Internal test-hook forwarders (`InternalsVisibleTo("DynamicWeb.Serializer.Tests")` already on csproj line 34):**

```csharp
internal void InvokeCreateAreaFromPropertiesForTest(int areaId, SerializedArea area, IReadOnlySet<string>? excludeFields)
    => CreateAreaFromProperties(areaId, area, excludeFields);

internal void InvokeUpdateAreaFromPropertiesForTest(int areaId, Dictionary<string, object> properties, IReadOnlySet<string>? excludeFields, IReadOnlySet<string>? excludeAreaColumns = null)
    => WriteAreaProperties(areaId, properties, excludeFields, excludeAreaColumns);
```

*Note: the plan stub named the UPDATE path `UpdateAreaFromProperties`; the actual private method in the codebase is `WriteAreaProperties`. The internal forwarder name retains the plan's `InvokeUpdateAreaFromPropertiesForTest` moniker for test-surface consistency but delegates to `WriteAreaProperties`.*

**Ordered-regex assertion (W3-hardened, verbatim from `AreaIdentityInsertTests.cs`):**

```csharp
var orderedPattern = new Regex(
    @"SET\s+IDENTITY_INSERT\s+\[Area\]\s+ON.*?INSERT\s+INTO\s+\[?Area\]?.*?SET\s+IDENTITY_INSERT\s+\[Area\]\s+OFF",
    RegexOptions.Singleline | RegexOptions.IgnoreCase);

Assert.True(orderedPattern.IsMatch(combined),
    "Area-create must emit SET IDENTITY_INSERT ON -> INSERT -> SET IDENTITY_INSERT OFF in order. " +
    "Captured text:\n" + combined);
```

The `string.Join("\n", capturedCommands)` combined text preserves emission order, so the regex matches whether the wrapping lives in a single multi-statement `CommandBuilder` or is split across separate `CommandBuilders`.

## A.1 test count + names

3 `[Fact]` methods in `BaselineLinkSweeperAcknowledgmentTests`:

1. `Sweep_UnacknowledgedOrphanId_Throws` — empty ack list → unresolved pageId 9999 is fatal
2. `Sweep_AcknowledgedOrphanId_IsFilteredFromFatal` — ack list `[15717]` → 15717 moves to acknowledged bucket, no fatal unresolved
3. `Sweep_UnlistedOrphanId_Throws_EvenWhenOtherAcknowledged` — **T-38-02 threat anchor**: ack `[15717]`, tree refs both 15717 and 9999 → fatal contains exactly `[9999]`

## B.5 test count + existing-test fixture update

**New tests:** 4 `[Fact]` methods in `BaselineLinkSweeperParagraphAnchorTests`:

1. `Sweep_PageAndParagraph_BothResolve_CountsAsResolved` — `Default.aspx?ID=4897#15717` resolves when page 4897 contains paragraph 15717
2. `Sweep_PageResolves_ParagraphDoesNot_Unresolved` — `Default.aspx?ID=4897#99999` with no paragraph 99999 → 1 unresolved (99999) with `Context` containing `"4897#99999"`
3. `Sweep_PageDoesNotResolve_AnchorNotChecked` — `Default.aspx?ID=9999#15717` with no page 9999 → 1 unresolved (9999); anchor not checked when page already fails
4. `Sweep_NoAnchor_Unchanged` — no `#Y` suffix behaves exactly like before

**Existing test updated:** `BaselineLinkSweeperTests.Sweep_AnchorFragment_StripsFragment_AndResolvesPage` — option (a) fixture update (paragraph 42 added to page 200).

## Full test suite pass count

- **Before Plan 38-02:** 629/629 passing (from Plan 38-01 SUMMARY: 620 baseline + 9 Wave 1 tests).
- **After Plan 38-02:** 640/640 passing.
- **Net new Phase 38 tests in this plan:** 11 (+2 A.3 ConfigLoader warning, +3 A.1 ack semantics, +2 A.2 identity insert, +4 B.5 paragraph anchor).

```
Passed!  - Failed:     0, Passed:   640, Skipped:     0, Total:   640, Duration: ~650 ms
```

`dotnet test tests/DynamicWeb.Serializer.Tests --filter "Category!=Integration" --nologo` exits 0.

## Plan-level grep verification

All acceptance-criteria greps green after the final commit:

- `! grep -rn "ModeConfig.*AcknowledgedOrphanPageIds\|mode\.AcknowledgedOrphanPageIds" src/` → no matches (A.3 consolidation)
- `grep -c "AcknowledgedOrphanPageIds" src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` → 1 (canonical home retained)
- `grep -q "WARNING: deploy.acknowledgedOrphanPageIds" src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` → 0
- `grep -q "WARNING: seed.acknowledgedOrphanPageIds" src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` → 0
- `grep -q "SelectMany(p => p.AcknowledgedOrphanPageIds)" src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` → 0
- `grep -q "ISqlExecutor? sqlExecutor" src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` → 0
- `grep -c "_sqlExecutor.ExecuteNonQuery" src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` → 2
- `grep -q "SET IDENTITY_INSERT \[Area\] ON" src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` → 0 (literal preserved)
- `grep -q "SET IDENTITY_INSERT \[Area\] OFF" src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` → 0 (literal preserved)
- `grep -q "InvokeCreateAreaFromPropertiesForTest" src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` → 0
- `grep -q "RegexOptions.Singleline" tests/DynamicWeb.Serializer.Tests/Serialization/AreaIdentityInsertTests.cs` → 0
- `grep -q "CollectSourceParagraphIds" src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` → 0
- `grep -q "validParagraphIds" src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` → 0
- `grep -q "m.Groups\[4\].Success" src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` → 0

## Decisions Made

- **A.2 internal-hook method name for UPDATE path** — kept `InvokeUpdateAreaFromPropertiesForTest` from the plan stub even though the actual private method is `WriteAreaProperties`. Test-surface stability > source-name symmetry. The forwarder delegates to `WriteAreaProperties`; the test-facing identifier reflects the domain operation (update an area from its properties), not the private method's implementation name.
- **B.5 existing-test update strategy** — option (a) per plan: extend the fixture so the anchor ID is a real paragraph source ID. Preserves the test's name and intent (`Sweep_AnchorFragment_StripsFragment_AndResolvesPage`) while matching the new semantics. Option (b) (rename + rewrite) would have lost the original regression coverage.
- **A.3 warning wording verbatim** — kept the three distinctive phrases from CONTEXT §Specifics (`deploy.acknowledgedOrphanPageIds` / `seed.acknowledgedOrphanPageIds`, `no longer supported`, `D-38-03`) so tests can grep-assert without coupling to non-essential formatting.
- **Extra A.3 test added** — the plan called for one new `Load_LegacyModeLevelAckList_LogsWarningAndDrops` test. Added a second (`Load_LegacySeedModeLevelAckList_LogsWarningAndDrops`) to exercise the seed path symmetrically. Both mode paths are written independently in the production code, so independent test coverage is warranted.

## Deviations from Plan

None — plan executed as written. Method-name mismatch in Task 3 (`UpdateAreaFromProperties` vs. `WriteAreaProperties`) was handled by keeping the test-facing forwarder name and delegating to the actual private method; this was within the plan's "adjust the internal forwarder MUST match the actual private method signatures — read those methods first and adjust" guidance.

## Issues Encountered

- **Worktree base reset required at agent startup.** The worktree was initially at `e12f9617` (newer than the expected base `95fe17f4`). The `<worktree_branch_check>` step hard-reset cleanly — no user changes to preserve. Resolved before any plan work began.
- **Flaky `ConfigPathResolverTests.FindOrCreateConfigFile_ReturnsExisting_WhenFileExists`.** This test occasionally fails on the first run of the full suite (temp-directory race, unrelated to this plan). Re-running immediately produces a clean 640/640 pass. Not caused by this plan's changes; pre-existing flakiness. Not worth blocking on — re-run policy applied.

## Threat Flags

None — this plan's changes sit entirely within the Phase 38 Wave 2 threat model (T-38-01 consolidation warning, T-38-02 ack-list malicious-ID guard, T-38-03 FK-integrity via IDENTITY_INSERT wrapping, T-38-B5-01/T-38-B5-02 paragraph-anchor regex scope), all already dispositioned in the plan's `<threat_model>`. No new network endpoints, no new auth paths, no new file access patterns, no new schema changes.

## Next Phase Readiness

- **Wave 3 (Plan 03)** — B.1/B.2/B.3/B.4/C.1 investigations + C.1 `FlatFileStore` dedup fix. Zero source-file overlap with Wave 2.
- **Wave 5 (Plan 05)** — D-38-16 config flip. B.5 (this plan) is now a correctness fix rather than a toggle; the `"acknowledgedOrphanPageIds": [15717]` entry in `swift2.2-combined.json` can be removed by Plan 05 once Wave 3 investigations close. This plan did NOT modify the combined config (plan's explicit scope boundary — Wave 5 owns that flip).
- **No live E2E re-run** — not in scope for this plan; deferred to Wave 5 final verification.

## Self-Check

- [x] `src/DynamicWeb.Serializer/Configuration/ModeConfig.cs` modified (A.3 field removed)
- [x] `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` modified (legacy warning + drop)
- [x] `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` modified (persist shape no longer emits mode-level ack list)
- [x] `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` modified (per-predicate SelectMany aggregation)
- [x] `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` modified (ISqlExecutor seam + internal test hooks)
- [x] `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs` modified (dead-code removal)
- [x] `src/DynamicWeb.Serializer/Infrastructure/BaselineLinkSweeper.cs` modified (CollectSourceParagraphIds + paragraph-anchor validation)
- [x] `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` modified (2 new Phase 38 tests)
- [x] `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperTests.cs` modified (Sweep_AnchorFragment fixture updated)
- [x] `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAcknowledgmentTests.cs` created (3 Facts)
- [x] `tests/DynamicWeb.Serializer.Tests/Serialization/AreaIdentityInsertTests.cs` created (2 Facts)
- [x] `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperParagraphAnchorTests.cs` created (4 Facts)
- [x] Commit `b0b6b83` present in `git log`
- [x] Commit `2af12a1` present in `git log`
- [x] Commit `b68f259` present in `git log`
- [x] Commit `a515ec5` present in `git log`
- [x] Commit `7ddd37c` present in `git log`
- [x] Full suite green (640/640)
- [x] `! grep -rn "ModeConfig.*AcknowledgedOrphanPageIds" src/` returns nothing
- [x] STATE.md not modified (orchestrator owns)
- [x] ROADMAP.md not modified (orchestrator owns)

## Self-Check: PASSED

---
*Phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37*
*Plan: 02*
*Completed: 2026-04-21*

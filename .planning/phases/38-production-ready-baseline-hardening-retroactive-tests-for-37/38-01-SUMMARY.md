---
phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37
plan: 01
subsystem: admin-api
tags: [admin-api, docs, query-binding, http-status, baseline, env-config]

# Dependency graph
requires:
  - phase: 37-production-ready-baseline
    provides: "SerializerSerializeCommand/SerializerDeserializeCommand Mode property (37-01), OrchestratorResult HasErrors semantics (37-04), BaselineLinkSweeper acknowledged-orphan plumbing (37-05)"
provides:
  - "Unconditional query-param fallback for ?mode=seed on both API commands (D-38-11)"
  - "Pure MapStatusFromResult status-mapping helper + InvokeMapStatusForTest test seam on both commands (D-38-12)"
  - "SynthOrchestratorResult test helper for driving the status-mapping branch with zero errors"
  - "Swift2.2-baseline.md: Pre-existing source-data bugs section (D-38-14)"
  - "env-bucket.md: per-env configuration reference cross-linked to three-bucket split (D-38-15)"
affects: [phase-38-wave-2-retroactive-tests, phase-38-wave-3-investigations, phase-38-wave-5-strict-mode-restoration]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pure status-mapping helper + internal test seam (InvokeMapStatusForTest) for testing API status invariants without running the full pipeline"
    - "Synthetic OrchestratorResult test helper (SynthOrchestratorResult.WithEmptyErrors) for driving zero-error branches without DB/FS/HTTP"

key-files:
  created:
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/SynthOrchestratorResult.cs"
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSerializeCommandTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerDeserializeCommandTests.cs"
    - "docs/baselines/env-bucket.md"
  modified:
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs"
    - "docs/baselines/Swift2.2-baseline.md"

key-decisions:
  - "D.1 query-param fallback is UNCONDITIONAL per D-38-11 + checker B4 — no curl-probe escape hatch, always lands"
  - "D.2 zero-error status invariant is proven via SynthOrchestratorResult + InvokeMapStatusForTest seam — unconditional Assert.Equal, no environment-dependent branching"
  - "Deserialize command gets an additional ?strictMode=true|false query fallback (only applies when StrictMode is null, preserving JSON body precedence)"
  - "env-bucket.md explicitly cross-references the DEPLOYMENT/SEED/ENVIRONMENT three-bucket split per checker warning W5"

patterns-established:
  - "Internal InvokeMapStatusForTest seam: pure static mapper + internal wrapper for test access, leveraging existing InternalsVisibleTo on the serializer csproj"
  - "Synth test helpers constructed with explicit init-only collections (Errors = new(), SerializeResults = new(), DeserializeResults = new()) to guarantee HasErrors == false"

requirements-completed: [D.1, D.2, E.1, E.2]

# Metrics
duration: 22min
completed: 2026-04-21
---

# Phase 38 Plan 01: Quick-Win Baseline Hardening Summary

**?mode=seed query-param fallback + unconditional zero-error == Ok status invariant on both Serialize/Deserialize admin API commands, plus the Swift2.2-baseline.md pre-existing-bugs section and the new env-bucket.md per-env reference cross-linked to the three-bucket split.**

## Performance

- **Duration:** ~22 min
- **Started:** 2026-04-21T~14:30Z
- **Completed:** 2026-04-21T~14:52Z
- **Tasks:** 3
- **Files modified:** 7 (2 modified source + 3 new test + 1 modified doc + 1 new doc)

## Accomplishments

- **D.1 (D-38-11):** Unconditional `Dynamicweb.Context.Current?.Request?["mode"]` fallback landed on both `SerializerSerializeCommand` and `SerializerDeserializeCommand`. The fallback fires when `Mode` stayed at the `"deploy"` default; any value read from the query string is re-validated through `Enum.TryParse<DeploymentMode>` before use (same threat gate as the JSON body path, per T-38-D1-01). Deserialize also gained an optional `?strictMode=true|false` fallback that only applies when `StrictMode` is null.
- **D.2 (D-38-12):** Extracted the status-mapping branch of `Handle()` into a pure `MapStatusFromResult(OrchestratorResult, string) → CommandResult` helper on both commands. The internal `InvokeMapStatusForTest(OrchestratorResult)` seam exposes the mapping to test code without running the full pipeline. The invariant is proven **unconditionally** in two test methods (`Handle_ZeroErrors_SynthOrchestratorResult_ReturnsOk` on each command) that drive `SynthOrchestratorResult.WithEmptyErrors()` through the seam and `Assert.Equal(CommandResult.ResultType.Ok, mapped.Status)` with no `if` wrapper.
- **E.1 (D-38-14):** Swift2.2-baseline.md lines 178–235 now carry the new `## Pre-existing source-data bugs caught by Phase 37 validators` section with all four required sub-sections: 3 column-name mistakes (SqlIdentifierValidator catch), 5 orphan page IDs (BaselineLinkSweeper catch), 267 + 238 cleanup counts (direct DB analysis via the 03- and 04- scripts), and the `acknowledgedOrphanPageIds: [15717]` temporary-note with removal gate pointing at B.5 / D-38-16.
- **E.2 (D-38-15):** New `docs/baselines/env-bucket.md` (166 lines) with 8 `##` sections: Relationship to the DEPLOYMENT/SEED/ENVIRONMENT split (W5 cross-reference), Purpose, What is NOT in the baseline and why, GlobalSettings.config, Azure Key Vault secrets, Per-env Area fields, Swift templates filesystem, Azure App Service config pattern. Explicitly cross-references Swift2.2-baseline.md throughout.

## Task Commits

Each task was committed atomically on the worktree branch (`--no-verify` per parallel-execution contract):

1. **Task 1: D.1 + D.2 on SerializerSerializeCommand** — `048e78c` (feat)
2. **Task 2: Mirror D.1 + D.2 on SerializerDeserializeCommand** — `29a644e` (feat)
3. **Task 3: E.1 extend Swift2.2-baseline.md + E.2 new env-bucket.md** — `e5681c7` (docs)

## Files Created/Modified

### Created

- `tests/DynamicWeb.Serializer.Tests/AdminUI/SynthOrchestratorResult.cs` — static factory `WithEmptyErrors()` → `OrchestratorResult { Errors = [], SerializeResults = [], DeserializeResults = [] }`. Guarantees `HasErrors == false`.
- `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSerializeCommandTests.cs` — 5 `[Fact]` methods covering JSON body mode parse, query-param marker (`QueryParamMode`), Invalid rejection, unconditional zero-error == Ok via synth helper, anti-regression Message content independence.
- `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerDeserializeCommandTests.cs` — 4 `[Fact]` methods mirroring the Serialize shape on the Deserialize command, same unconditional invariant.
- `docs/baselines/env-bucket.md` — 166 lines, 8 H2 sections, cross-refs Swift2.2-baseline.md and the three-bucket split taxonomy.

### Modified

- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` — added D-38-11 fallback block after the initial `Enum.TryParse` gate; extracted `MapStatusFromResult` + `InvokeMapStatusForTest` (internal static); replaced inline `return new CommandResult { Status = result.HasErrors ? ... }` with `return MapStatusFromResult(result, message)`.
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` — same D-38-11 fallback plus the optional `?strictMode` fallback (gated on `StrictMode is null` so JSON body always wins); same `MapStatusFromResult` + `InvokeMapStatusForTest` refactor.
- `docs/baselines/Swift2.2-baseline.md` — new `## Pre-existing source-data bugs caught by Phase 37 validators` section (lines 178–235) inserted between the existing "What's NOT in the baseline" table and "Azure deployment assumptions" section.

## D.2 status-guard test — proof of unconditional invariant

Test method: `Handle_ZeroErrors_SynthOrchestratorResult_ReturnsOk` (present on both `SerializerSerializeCommandTests` and `SerializerDeserializeCommandTests`).

Body (Serialize variant; Deserialize is identical modulo the command class name):

```csharp
[Fact]
public void Handle_ZeroErrors_SynthOrchestratorResult_ReturnsOk()
{
    // D-38-12 (hardened per checker B3): UNCONDITIONAL assertion.
    // Construct a synthetic zero-error OrchestratorResult and drive the
    // status-mapping branch directly. No environment dependency, no escape.
    // If this assertion fails, the D.2 regression has re-appeared: the HTTP
    // status-mapping branch has flipped a zero-error orchestrator result to
    // Error/Invalid, which would produce HTTP 400 on a successful serialize.
    var synth = SynthOrchestratorResult.WithEmptyErrors();

    var mapped = SerializerSerializeCommand.InvokeMapStatusForTest(synth);

    Assert.Equal(CommandResult.ResultType.Ok, mapped.Status);
}
```

No `if` wrapper around the `Assert.Equal` — the assertion is always executed and always checked. No environment branching, no curl probe, no config-file dependency.

## SynthOrchestratorResult helper — public API

Location: `tests/DynamicWeb.Serializer.Tests/AdminUI/SynthOrchestratorResult.cs`

```csharp
internal static class SynthOrchestratorResult
{
    public static OrchestratorResult WithEmptyErrors();
}
```

Returns an `OrchestratorResult` with `Errors`, `SerializeResults`, and `DeserializeResults` all explicitly empty → `HasErrors` evaluates `false` by definition. Placed in `DynamicWeb.Serializer.Tests.AdminUI` so both the Serialize and Deserialize command tests share the helper without cross-file setup.

## E.1 extension location

- File: `docs/baselines/Swift2.2-baseline.md`
- Section heading: `## Pre-existing source-data bugs caught by Phase 37 validators`
- Lines: 178–235 (58 lines of new content)
- Inserted between: the existing "What's NOT in the baseline" table (ending line 176) and the existing "Azure deployment assumptions" section (now at line 236, previously line 178)
- Sub-sections: H3 "Three column-name mistakes (caught by SqlIdentifierValidator)", H3 "Five orphan page references (caught by BaselineLinkSweeper)", H3 "Orphan areas + soft-deleted pages (found via direct DB analysis)", H3 "Temporary: `acknowledgedOrphanPageIds: [15717]` on the Content predicate"

## E.2 env-bucket.md shape

- File: `docs/baselines/env-bucket.md` (166 lines total)
- `##` heading count: 8 (target: at least 7, which includes Relationship + Purpose + 5 main sections; achieved 8 by splitting "What is NOT in the baseline and why" from the deep-dive sections)
- Three-bucket cross-reference heading: `## Relationship to the DEPLOYMENT/SEED/ENVIRONMENT split` (present, with a bucket-ownership table that links `Swift2.2-baseline.md#the-three-bucket-split`)
- Additional cross-references: direct links to `Swift2.2-baseline.md#known-gaps-input-to-d2` (credential exclusion gap) and `Swift2.2-baseline.md#azure-deployment-assumptions` (config pattern)
- Swift git-clone pointer present: `git clone https://github.com/dynamicweb/Swift` in the "Swift templates filesystem" section with `git pull` update instructions and a note about Files-volume persistence on Azure App Service

## Full test suite pass count

- `dotnet test tests/DynamicWeb.Serializer.Tests --nologo` → **629 passed, 0 failed, 0 skipped** (620 baseline + 9 new Phase 38 tests: 5 Serialize + 4 Deserialize).
- `dotnet build --nologo` → 0 errors, 58 warnings (all warnings pre-existing; none introduced by this plan).
- Per-task filter verification:
  - `--filter FullyQualifiedName~SerializerSerializeCommandTests` → 5 passed
  - `--filter FullyQualifiedName~SerializerDeserializeCommandTests` → 4 passed

## Plan-level grep verification

All acceptance-criteria checks green:

- `grep -q "D-38-12" src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` → 0
- `grep -q "D-38-12" src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` → 0
- `grep -q "D-38-11" src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` → 0
- `grep -q "Dynamicweb.Context.Current?.Request?\[\"mode\"\]"` on both command files → 0
- `grep -q "## Pre-existing source-data bugs caught by Phase 37 validators" docs/baselines/Swift2.2-baseline.md` → 0
- `grep -q "acknowledgedOrphanPageIds: \[15717\]" docs/baselines/Swift2.2-baseline.md` → 0
- `grep -q "VatGroupName" docs/baselines/Swift2.2-baseline.md` → 0
- `grep -q "267 orphan-area pages" docs/baselines/Swift2.2-baseline.md` → 0
- `grep -q "238 soft-deleted pages" docs/baselines/Swift2.2-baseline.md` → 0
- `test -f docs/baselines/env-bucket.md` → 0
- `grep -q "^## GlobalSettings\.config$" docs/baselines/env-bucket.md` → 0
- `grep -c "^## " docs/baselines/env-bucket.md` → 8 (≥ 7 required)
- `grep -q "Swift2.2-baseline.md" docs/baselines/env-bucket.md` → 0
- `grep -q "git clone.*github.com/dynamicweb/Swift" docs/baselines/env-bucket.md` → 0
- `grep -qE "three-bucket split|DEPLOYMENT.*SEED.*ENVIRONMENT" docs/baselines/env-bucket.md` → 0
- `grep -q "## Relationship to the DEPLOYMENT/SEED/ENVIRONMENT split" docs/baselines/env-bucket.md` → 0

## Decisions Made

- **Strict mode query-param fallback on Deserialize is gated on `StrictMode is null`.** This preserves JSON body precedence (explicit body override still wins over query). The plan's PATTERNS.md §SerializerDeserializeCommand.cs left this optional; including it closes the "additional consideration" bullet without adding a new entry point.
- **MapStatusFromResult takes `(result, message)` rather than reading `result.Summary` inside.** Production `Handle()` composes a richer message (including `fileCount`, `modeRoot`, per-predicate summary) before mapping; making message an explicit parameter avoids duplicating that composition inside the helper. `InvokeMapStatusForTest` uses `result.Summary ?? string.Empty` so tests don't need to construct a message.
- **SynthOrchestratorResult also sets `DeserializeResults = []`.** The plan stub only showed `Errors` + `SerializeResults`, but `OrchestratorResult.HasErrors` also checks `DeserializeResults.Any(r => r.HasErrors)`. Setting all three empty is the only way to guarantee `HasErrors == false` regardless of which command drives the helper.

## Deviations from Plan

None - plan executed exactly as written. All acceptance criteria satisfied on the first iteration; the TDD RED phase for D.2 (write the test first, then the refactor) proceeded as planned and the test passed immediately after the `MapStatusFromResult` extraction landed (which matches the plan's Step 3 guidance: "if it passes immediately, the existing `result.HasErrors ? Error : Ok` line is already correct for the synthetic case; add the D-38-12 inline comment").

## Issues Encountered

- **Worktree base reset required.** The agent-a8548878 worktree was initially at `e12f9617` (newer than the expected base `de3759d5`). The `<worktree_branch_check>` step hard-reset to the expected base without data loss (fresh worktree; no user changes to preserve). Resolved before any plan work began.
- **Initial `dotnet test` ran from the wrong directory.** First test run was accidentally rooted at `C:/VibeCode/DynamicWeb.Serializer` (main repo) instead of the worktree; no tests matched the filter. Switched to the worktree root (`.claude/worktrees/agent-a8548878`) and tests discovered and passed immediately.
- **9 failing tests in `DynamicWeb.Serializer.IntegrationTests.dll`** — ALL pre-existing, ALL require live DW host initialization (per the header comment in `CustomerCenterSerializationTests.cs`: "IMPORTANT: These integration tests require the DW runtime to be initialized. They CANNOT be run from a developer workstation directly via 'dotnet test'."). Every failure is `DependencyResolverException : The Dependency Locator was not initialized properly.`. None are caused by this plan's changes. Scope-boundary rule applied: out-of-scope for Wave 1; logged here for traceability.

## Threat Flags

None — this plan's changes sit entirely within the Phase 37 admin-API threat model (T-38-D1-01 / T-38-D1-02 / T-38-D2-01 / T-38-E2-01, all already dispositioned in the plan's `<threat_model>`). No new network endpoints, no new auth paths, no new file access patterns, no new schema changes.

## Next Phase Readiness

- Wave 1 closes D.1, D.2, E.1, E.2. The remaining Phase 38 waves build on untouched source files:
  - Wave 2 (Plan 02): A.1 / A.2 / A.3 / B.5 retroactive tests — touches `ProviderPredicateDefinition`, `ModeConfig`, `ConfigLoader`, `ConfigWriter`, `ContentSerializer`, `ContentDeserializer`, `BaselineLinkSweeper`. Zero source-file overlap with Wave 1.
  - Wave 3 (Plan 03): B.1/B.2/B.3/B.4/C.1 investigations + C.1 `FlatFileStore` dedup fix. Zero overlap with Wave 1.
  - Wave 4 (Plan 04): D.3 PowerShell smoke tool. Zero overlap.
  - Wave 5 (Plan 05): D-38-16 config flip — removes `"strictMode": false` from `swift2.2-combined.json`. Gates on Waves 2 + 3 closing.
- Live E2E round-trip (Swift 2.2 → CleanDB) not re-run in this wave; that is the Wave 5 final verification step per the plan's success criteria.

## Self-Check

- [x] `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` — modified (D-38-11 fallback + D-38-12 refactor)
- [x] `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` — modified (D-38-11 fallback + strictMode fallback + D-38-12 refactor)
- [x] `tests/DynamicWeb.Serializer.Tests/AdminUI/SynthOrchestratorResult.cs` — created
- [x] `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSerializeCommandTests.cs` — created (5 Facts)
- [x] `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerDeserializeCommandTests.cs` — created (4 Facts)
- [x] `docs/baselines/Swift2.2-baseline.md` — modified (E.1 section lines 178–235)
- [x] `docs/baselines/env-bucket.md` — created (166 lines, 8 H2 sections)
- [x] Commit `048e78c` present in `git log --oneline HEAD~3..HEAD`
- [x] Commit `29a644e` present in `git log --oneline HEAD~3..HEAD`
- [x] Commit `e5681c7` present in `git log --oneline HEAD~3..HEAD`

## Self-Check: PASSED

---
*Phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37*
*Plan: 01*
*Completed: 2026-04-21*

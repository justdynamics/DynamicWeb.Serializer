---
phase: 05-integration
plan: 02
subsystem: testing
tags: [xunit, integration-tests, scheduled-tasks, yaml, dynamicweb]

# Dependency graph
requires:
  - phase: 05-integration-plan-01
    provides: NuGet packaging, Dynamicweb 10.23.9 reference, count summary logging
  - phase: 03-serialization
    provides: ContentSerializer, SerializeScheduledTask
  - phase: 04-deserialization
    provides: ContentDeserializer, DeserializeScheduledTask, integration test project infra
provides:
  - E2E integration test class for both DW scheduled task entry points
  - OPS-01 test: SerializeScheduledTask.Run() byte-identical output verification vs direct ContentSerializer call
  - OPS-02 test: DeserializeScheduledTask.Run() roundtrip completeness verification
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "[Collection] xunit attribute for sequential test execution — prevents log file contention between tests writing ContentSync.log"
    - "Config placed at AppDomain.CurrentDomain.BaseDirectory for FindConfigFile() discovery (third candidate path)"
    - "AssertDirectoryTreesEqual helper: compares relative .yml file lists then byte-compares each file pair"

key-files:
  created:
    - tests/Dynamicweb.ContentSync.IntegrationTests/ScheduledTasks/ScheduledTaskEndToEndTests.cs
  modified: []

key-decisions:
  - "[Collection(\"ScheduledTaskTests\")] ensures sequential execution — both tasks write to ContentSync.log in BaseDirectory, concurrent runs would corrupt the log"
  - "Config written to AppDomain.CurrentDomain.BaseDirectory (not CWD) — matches third FindConfigFile() candidate, reliable across dotnet test working directory variations"
  - "OPS-01 uses byte-exact file comparison (not content string compare) — catches any encoding/line-ending divergence between task and direct paths"
  - "OPS-02 test serializes first to produce YAML input, then runs DeserializeScheduledTask — self-contained, no pre-committed YAML fixtures required"

patterns-established:
  - "Scheduled task tests: write config to BaseDirectory, run task, assert return value and output"
  - "AssertDirectoryTreesEqual: enumerate relative paths from both dirs, assert same set, then byte-compare each file"

requirements-completed: [OPS-01, OPS-02]

# Metrics
duration: 5min
completed: 2026-03-20
---

# Phase 05 Plan 02: Scheduled Task End-to-End Integration Tests Summary

**E2E xunit integration tests for SerializeScheduledTask and DeserializeScheduledTask: OPS-01 asserts byte-identical YAML output vs direct ContentSerializer call, OPS-02 asserts successful roundtrip through the deserialize entry point.**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-19T23:13:55Z
- **Completed:** 2026-03-19T23:18:55Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- `ScheduledTaskEndToEndTests.cs` created in new `ScheduledTasks/` folder under integration tests project
- OPS-01 test (`SerializeScheduledTask_Run_ProducesSameOutputAsContentSerializer`) runs ContentSerializer directly to a `_direct` temp dir, then runs the scheduled task to a `_task` temp dir, and asserts byte-identical file trees via `AssertDirectoryTreesEqual`
- OPS-02 test (`DeserializeScheduledTask_Run_CompletesWithoutErrors`) serializes Customer Center to YAML first, then runs `DeserializeScheduledTask.Run()` and asserts it returns `true`
- `[Collection("ScheduledTaskTests")]` attribute ensures sequential test execution, preventing log file contention
- Config written to `AppDomain.CurrentDomain.BaseDirectory` — the exact path checked by `FindConfigFile()` (third candidate)

## Task Commits

Each task was committed atomically:

1. **Task 1: E2E integration tests for SerializeScheduledTask and DeserializeScheduledTask** - `d993625` (feat)

**Plan metadata:** _(see final commit below)_

## Files Created/Modified
- `tests/Dynamicweb.ContentSync.IntegrationTests/ScheduledTasks/ScheduledTaskEndToEndTests.cs` - E2E tests for both scheduled task entry points, following IDisposable/temp-dir pattern from existing tests

## Decisions Made
- `[Collection("ScheduledTaskTests")]` used for sequential execution — both scheduled tasks append to `ContentSync.log` in `BaseDirectory`; parallel runs risk truncating or interleaving log entries
- Config placed at `AppDomain.CurrentDomain.BaseDirectory` (not `CWD`) — `FindConfigFile()` checks BaseDirectory as its third candidate, and BaseDirectory is consistent regardless of the working directory `dotnet test` uses
- Byte-level comparison via `AssertDirectoryTreesEqual` — catches any encoding or line-ending divergence that a string-content comparison might miss
- OPS-02 test is self-contained: it serializes first using `ContentSerializer` directly (no task), then runs the deserialize task — no pre-committed YAML fixtures required

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None. Build succeeded on first attempt with zero warnings or errors.

## User Setup Required

None — no external service configuration required. Tests require a running DW instance per the header comment in the test file.

## Next Phase Readiness
- Phase 05 is complete — all requirements (INF-01, OPS-01, OPS-02, OPS-03) are satisfied
- Both scheduled tasks are fully covered by integration tests
- NuGet package is generated on build and ready for AppStore submission

---
*Phase: 05-integration*
*Completed: 2026-03-20*

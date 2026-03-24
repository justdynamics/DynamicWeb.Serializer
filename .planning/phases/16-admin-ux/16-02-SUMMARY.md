---
phase: 16-admin-ux
plan: 02
subsystem: admin-ui
tags: [tree-node, logging, advice-generator, admin-navigation]

# Dependency graph
requires:
  - phase: 16-01
    provides: "DynamicWeb.Serializer namespace and Serializer_* node IDs"
provides:
  - "Tree node at Settings > Database > Serialize (SystemSection)"
  - "LogFileWriter for per-run timestamped log files with JSON summary headers"
  - "LogFileSummary/PredicateSummary models for structured log data"
  - "AdviceGenerator for error-to-advice rule engine"
  - "Per-run logging in both Serialize and Deserialize commands"
affects: [16-03, 16-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Per-run log files with timestamped filenames and JSON summary headers between markers"
    - "Buffered log lines flushed after operation with summary prepended"
    - "Error pattern matching for actionable user advice"

key-files:
  created:
    - "src/DynamicWeb.Serializer/Infrastructure/LogFileWriter.cs"
    - "src/DynamicWeb.Serializer/Models/LogFileSummary.cs"
    - "src/DynamicWeb.Serializer/Infrastructure/AdviceGenerator.cs"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/LogFileWriterTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/AdviceGeneratorTests.cs"
  modified:
    - "src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs"
    - "src/DynamicWeb.Serializer/AdminUI/Tree/SerializerNavigationNodePathProvider.cs"
    - "src/DynamicWeb.Serializer/AdminUI/Tree/PredicateNavigationNodePathProvider.cs"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs"

key-decisions:
  - "Log lines buffered in List<string> then flushed with summary header prepended (avoids file rewrite)"
  - "AdviceGenerator matches error strings case-insensitively for FOREIGN KEY, group/not found, duplicate patterns"
  - "LogViewer node deferred to Plan 03 when screen class exists (avoids forward reference compile error)"

patterns-established:
  - "Per-run log files: {Operation}_{yyyy-MM-dd_HHmmss}.log with JSON summary between === SERIALIZER SUMMARY === markers"
  - "SystemSection base class for Settings > Database tree nodes"

requirements-completed: [UX-03, UX-04, UX-01]

# Metrics
duration: 5min
completed: 2026-03-24
---

# Phase 16 Plan 02: Tree Node Relocation and Log Infrastructure Summary

**Admin tree relocated to Settings > Database > Serialize with per-run log files, JSON summary headers, and error-to-advice generation**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-24T12:52:56Z
- **Completed:** 2026-03-24T12:58:00Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments
- Tree node relocated from Settings > Content to Settings > Database > Serialize (AreasSection to SystemSection)
- LogFileWriter creates timestamped per-run log files with JSON summary headers between markers
- AdviceGenerator maps FK constraint, missing group, duplicate key, and generic errors to actionable guidance
- Both API commands now produce per-run log files instead of overwriting a single Serializer.log
- 14 new unit tests covering all log and advice behaviors

## Task Commits

Each task was committed atomically:

1. **Task 1: Create LogFileWriter, LogFileSummary, and AdviceGenerator with tests** - `51fc6d9` (feat)
2. **Task 2: Relocate tree node + update commands to per-run logging** - `172d564` (feat)

## Files Created/Modified
- `src/DynamicWeb.Serializer/Models/LogFileSummary.cs` - Structured log summary and predicate summary records
- `src/DynamicWeb.Serializer/Infrastructure/LogFileWriter.cs` - Per-run log file creation, summary header write/parse, line append
- `src/DynamicWeb.Serializer/Infrastructure/AdviceGenerator.cs` - Error pattern to actionable advice mapping
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/LogFileWriterTests.cs` - 8 tests for log file operations
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/AdviceGeneratorTests.cs` - 6 tests for advice generation
- `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` - SystemSection base, DatabaseRootId, SerializeNodeId
- `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerNavigationNodePathProvider.cs` - Updated path to SystemSection/Settings_Database
- `src/DynamicWeb.Serializer/AdminUI/Tree/PredicateNavigationNodePathProvider.cs` - Updated path to SystemSection/Settings_Database
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` - Per-run logging with LogFileSummary
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` - Per-run logging with AdviceGenerator

## Decisions Made
- Log lines buffered in memory during operation, then flushed with summary header prepended -- simpler than file rewriting
- AdviceGenerator uses case-insensitive string matching for error pattern detection
- LogViewer tree node deferred to Plan 03 when the screen class will exist (avoids compile errors from forward references)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- LogFileWriter and LogFileSummary ready for Plan 03 (log viewer screen) to consume
- AdviceGenerator ready for Plan 03 to display in log viewer UI
- Per-run log files enable Plan 04 (zip deserialize) to write operation-specific logs
- Tree node at correct location for all future admin UI work

---
*Phase: 16-admin-ux*
*Completed: 2026-03-24*

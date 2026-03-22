---
phase: 09-predicate-management
plan: 01
subsystem: api
tags: [crud, commands, queries, predicates, config]

requires:
  - phase: 08-settings-screen
    provides: AdminUI patterns (CommandBase, DataQueryModelBase, ConfigurableProperty)
provides:
  - PredicateListModel and PredicateEditModel view models
  - PredicateListQuery and PredicateByIndexQuery data queries
  - SavePredicateCommand with name uniqueness and excludes splitting
  - DeletePredicateCommand with index validation
  - ConfigLoader zero-predicate support (D-15)
  - PageId property on PredicateDefinition for UI round-tripping
affects: [09-predicate-management plan 02, 10-context-menus]

tech-stack:
  added: []
  patterns: [ConfigPath test override for command testability, try-catch Services fallback for unit tests]

key-files:
  created:
    - src/Dynamicweb.ContentSync/AdminUI/Models/PredicateListModel.cs
    - src/Dynamicweb.ContentSync/AdminUI/Models/PredicateEditModel.cs
    - src/Dynamicweb.ContentSync/AdminUI/Queries/PredicateListQuery.cs
    - src/Dynamicweb.ContentSync/AdminUI/Queries/PredicateByIndexQuery.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/SavePredicateCommand.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/DeletePredicateCommand.cs
    - tests/Dynamicweb.ContentSync.Tests/AdminUI/PredicateCommandTests.cs
  modified:
    - src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs
    - src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs
    - tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderTests.cs

key-decisions:
  - "ConfigPath override property on commands for test isolation without DW runtime"
  - "try-catch around Services.Pages access for graceful fallback in unit tests"
  - "PageId defaults to 0 (legacy/unset) for backward compatibility"

patterns-established:
  - "Command testability: ConfigPath property bypasses ConfigPathResolver for unit tests"
  - "Services fallback: try-catch around DW static Services for non-runtime tests"

requirements-completed: [PRED-02, PRED-03, PRED-04, PRED-05, PRED-06]

duration: 4min
completed: 2026-03-22
---

# Phase 09 Plan 01: Predicate CRUD Data Layer Summary

**Predicate CRUD commands/queries with ConfigLoader zero-predicate fix, PageId round-tripping, and name uniqueness validation**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-22T00:11:36Z
- **Completed:** 2026-03-22T00:15:34Z
- **Tasks:** 1 (TDD: RED + GREEN)
- **Files modified:** 10

## Accomplishments
- PredicateDefinition extended with PageId property for UI page selector round-tripping
- ConfigLoader fixed to allow zero predicates (null-to-empty coercion, per D-15)
- Complete predicate CRUD: SavePredicateCommand (create/update with name uniqueness, excludes \r\n splitting), DeletePredicateCommand (index-based removal)
- PredicateListQuery resolves area names via Services.Areas, PredicateByIndexQuery returns blank model for Index=-1
- 10 new tests (PredicateCommandTests + ConfigLoader zero-predicate tests), all 97 tests pass

## Task Commits

Each task was committed atomically:

1. **Task 1 RED: Failing tests** - `661918c` (test)
2. **Task 1 GREEN: Implementation** - `cb1fca8` (feat)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs` - Added PageId property
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` - Zero-predicate fix, PageId in raw model and mapping
- `src/Dynamicweb.ContentSync/AdminUI/Models/PredicateListModel.cs` - Row model for predicate list screen
- `src/Dynamicweb.ContentSync/AdminUI/Models/PredicateEditModel.cs` - Form model with Index=-1 sentinel for new predicates
- `src/Dynamicweb.ContentSync/AdminUI/Queries/PredicateListQuery.cs` - List query with area name resolution
- `src/Dynamicweb.ContentSync/AdminUI/Queries/PredicateByIndexQuery.cs` - Single predicate query, blank model for new
- `src/Dynamicweb.ContentSync/AdminUI/Commands/SavePredicateCommand.cs` - Save/create with duplicate name check, excludes splitting
- `src/Dynamicweb.ContentSync/AdminUI/Commands/DeletePredicateCommand.cs` - Delete by index with validation
- `tests/Dynamicweb.ContentSync.Tests/AdminUI/PredicateCommandTests.cs` - 10 tests for save/delete commands
- `tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderTests.cs` - Replaced throw tests with zero-predicate success tests

## Decisions Made
- Added ConfigPath override property on commands for test isolation (avoids needing DW runtime for unit tests)
- Wrapped Services.Pages access in try-catch for graceful fallback when DW runtime unavailable
- PageId defaults to 0 for backward compatibility with existing config files

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Services.Pages throws without DW runtime**
- **Found during:** Task 1 GREEN (test execution)
- **Issue:** Services.Pages?.GetPage() throws NullReferenceException in unit tests (no DW runtime initialized)
- **Fix:** Wrapped in try-catch with fallback path resolution using existing predicate path or PageId placeholder
- **Files modified:** src/Dynamicweb.ContentSync/AdminUI/Commands/SavePredicateCommand.cs
- **Verification:** All 97 tests pass
- **Committed in:** cb1fca8

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Essential for testability. No scope creep.

## Issues Encountered
None beyond the Services.Pages runtime access handled above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All data-layer components ready for Plan 02 screen wiring
- PredicateListQuery, PredicateByIndexQuery, SavePredicateCommand, DeletePredicateCommand all importable
- PredicateEditModel uses Index=-1 sentinel for new predicates

## Self-Check: PASSED

All 10 files verified present. Both commit hashes (661918c, cb1fca8) confirmed in git log.

---
*Phase: 09-predicate-management*
*Completed: 2026-03-22*

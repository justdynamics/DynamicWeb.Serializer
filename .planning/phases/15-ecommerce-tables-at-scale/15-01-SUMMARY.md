---
phase: 15-ecommerce-tables-at-scale
plan: 01
subsystem: database
tags: [topological-sort, kahns-algorithm, fk-dependency, cache-invalidation, sys-foreign-keys]

requires:
  - phase: 13-provider-architecture
    provides: "ISqlExecutor, SqlTableProvider, DataGroupMetadataReader"
provides:
  - "FkDependencyResolver for topological FK ordering of SQL tables"
  - "CacheInvalidator with ICacheResolver abstraction for DW cache clearing"
  - "ServiceCaches config field on ProviderPredicateDefinition"
affects: [15-02-orchestrator-wiring, ecommerce-tables]

tech-stack:
  added: []
  patterns: ["Kahn's algorithm for topological sort", "ICacheResolver/ICacheInstance abstraction over DW AddInManager"]

key-files:
  created:
    - src/Dynamicweb.ContentSync/Providers/SqlTable/FkDependencyResolver.cs
    - src/Dynamicweb.ContentSync/Providers/CacheInvalidator.cs
    - tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/FkDependencyResolverTests.cs
    - tests/Dynamicweb.ContentSync.Tests/Providers/CacheInvalidatorTests.cs
  modified:
    - src/Dynamicweb.ContentSync/Models/ProviderPredicateDefinition.cs
    - src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs
    - tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderTests.cs

key-decisions:
  - "ICacheResolver/ICacheInstance interfaces abstract DW AddInManager static calls for testability"
  - "Self-referencing FK filtering in C# as defense-in-depth alongside SQL WHERE clause"
  - "Canonical casing preserved from input table names through topological sort output"

patterns-established:
  - "ICacheResolver pattern: wrap DW static AddInManager calls behind injectable interface"
  - "Kahn's algorithm with OrdinalIgnoreCase HashSet for case-insensitive FK table matching"

requirements-completed: [SQL-03, CACHE-01]

duration: 6min
completed: 2026-03-24
---

# Phase 15 Plan 01: Infrastructure Components Summary

**FK dependency resolver via Kahn's topological sort on sys.foreign_keys, plus DW cache invalidation via ICacheResolver abstraction**

## Performance

- **Duration:** 6 min
- **Started:** 2026-03-24T11:00:49Z
- **Completed:** 2026-03-24T11:06:31Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- FkDependencyResolver produces correct topological order for chain, diamond, self-ref, external FK, and cycle detection scenarios
- CacheInvalidator clears DW service caches via testable ICacheResolver abstraction with deduplication and graceful error handling
- ProviderPredicateDefinition extended with ServiceCaches field, ConfigLoader deserializes it from JSON

## Task Commits

Each task was committed atomically:

1. **Task 1: FkDependencyResolver with topological sort + ServiceCaches** - `9c39ebc` (feat)
2. **Task 2: CacheInvalidator wrapping DW AddInManager pattern** - `ccfdb97` (feat)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Providers/SqlTable/FkDependencyResolver.cs` - Queries sys.foreign_keys, builds dependency graph, sorts via Kahn's algorithm
- `src/Dynamicweb.ContentSync/Providers/CacheInvalidator.cs` - ICacheResolver/ICacheInstance interfaces + CacheInvalidator class
- `src/Dynamicweb.ContentSync/Models/ProviderPredicateDefinition.cs` - Added ServiceCaches property
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` - Added ServiceCaches to RawPredicateDefinition and BuildPredicate
- `tests/Dynamicweb.ContentSync.Tests/Providers/SqlTable/FkDependencyResolverTests.cs` - 9 tests covering all FK ordering scenarios
- `tests/Dynamicweb.ContentSync.Tests/Providers/CacheInvalidatorTests.cs` - 6 tests covering cache invalidation behaviors
- `tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderTests.cs` - 2 new tests for ServiceCaches config round-trip

## Decisions Made
- Created ICacheResolver/ICacheInstance interfaces to abstract DW AddInManager static calls, enabling full unit test coverage without DW runtime
- Added self-referencing FK filtering in C# code as defense-in-depth (SQL WHERE clause also filters these, but mocked tests need the C# check)
- Preserved canonical table name casing from input through topological sort output using GetCanonical helper

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Self-referencing FK filtering in C# code**
- **Found during:** Task 1 (FkDependencyResolver implementation)
- **Issue:** SQL WHERE clause filters self-refs, but test mocks bypass SQL. The self-ref test case (A->A edge) caused false cycle detection.
- **Fix:** Added OrdinalIgnoreCase self-ref check in QueryForeignKeyEdges C# filtering, defense-in-depth alongside SQL.
- **Files modified:** src/Dynamicweb.ContentSync/Providers/SqlTable/FkDependencyResolver.cs
- **Verification:** Self-referencing FK test passes, no false cycle error
- **Committed in:** 9c39ebc (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Essential for correctness when testing with mocked ISqlExecutor. No scope creep.

## Issues Encountered
- Intermittent MSB3492 build cache errors due to parallel agent execution. Resolved by retrying builds.

## Known Stubs
None - all classes are fully implemented with real logic.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- FkDependencyResolver ready for Plan 02 to wire into SerializerOrchestrator (sort SqlTable predicates before dispatch)
- CacheInvalidator ready for Plan 02 to call after each predicate's deserialization completes
- ServiceCaches config field ready for ecommerce predicate configurations

---
*Phase: 15-ecommerce-tables-at-scale*
*Completed: 2026-03-24*

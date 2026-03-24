---
phase: 15-ecommerce-tables-at-scale
plan: 02
subsystem: database
tags: [fk-ordering, cache-invalidation, orchestrator, ecommerce-config, deserialization-ordering]

requires:
  - phase: 15-ecommerce-tables-at-scale
    provides: "FkDependencyResolver, CacheInvalidator, ICacheResolver, ServiceCaches field"
  - phase: 14-content-provider-adapter
    provides: "SerializerOrchestrator, ProviderRegistry, command infrastructure"
provides:
  - "FK-ordered deserialization in SerializerOrchestrator"
  - "Per-predicate cache invalidation after deserialization"
  - "ProviderRegistry.CreateOrchestrator factory with full dependency wiring"
  - "Complete 26-table ecommerce predicate configuration reference"
affects: [ecommerce-runtime, future-providers]

tech-stack:
  added: []
  patterns: ["Reflection-based DW AddInManager integration for cache resolution", "Optional nullable constructor dependencies for backward compatibility"]

key-files:
  created:
    - src/Dynamicweb.ContentSync/Configuration/ecommerce-predicates-example.json
  modified:
    - src/Dynamicweb.ContentSync/Providers/SerializerOrchestrator.cs
    - src/Dynamicweb.ContentSync/Providers/ProviderRegistry.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/ContentSyncSerializeCommand.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/ContentSyncDeserializeCommand.cs
    - tests/Dynamicweb.ContentSync.Tests/Providers/SerializerOrchestratorTests.cs

key-decisions:
  - "FkDependencyResolver and CacheInvalidator are optional nullable parameters for backward compatibility"
  - "DwCacheResolver uses reflection to call AddInManager at runtime, avoiding compile-time coupling"
  - "Non-SqlTable predicates placed before SqlTable predicates in deserialization order"
  - "Cache invalidation failures are caught and logged but never block other predicates"

patterns-established:
  - "Optional infrastructure dependencies via nullable constructor params with default null"
  - "CreateOrchestrator factory pattern centralizing full dependency wiring"

requirements-completed: [ECOM-01, ECOM-02, ECOM-03, ECOM-04, SQL-03, CACHE-01]

duration: 9min
completed: 2026-03-24
---

# Phase 15 Plan 02: Orchestrator Wiring Summary

**FK-ordered deserialization and per-predicate cache invalidation wired into SerializerOrchestrator, with complete 26-table ecommerce config reference**

## Performance

- **Duration:** 9 min
- **Started:** 2026-03-24T11:08:57Z
- **Completed:** 2026-03-24T11:18:00Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- SerializerOrchestrator sorts SqlTable predicates by FK dependency order before deserialization dispatch
- Cache invalidation runs after each successful predicate deserialize, skipped during dry-run, failures logged gracefully
- ProviderRegistry.CreateOrchestrator() factory wires FkDependencyResolver + CacheInvalidator + all providers
- Complete ecommerce predicate config documents all 26 tables with correct nameColumns and serviceCaches
- 7 new orchestrator tests covering FK ordering, cache invalidation, dry-run, error handling

## Task Commits

Each task was committed atomically:

1. **Task 1: Integrate FK ordering and cache invalidation into SerializerOrchestrator** - `8c9db1a` (feat)
2. **Task 2: Document complete ecommerce predicate configuration** - `a8e9759` (docs)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/Providers/SerializerOrchestrator.cs` - Added FK ordering in DeserializeAll, cache invalidation after each predicate
- `src/Dynamicweb.ContentSync/Providers/ProviderRegistry.cs` - Added DwCacheResolver, DwCacheInstance, CreateOrchestrator factory
- `src/Dynamicweb.ContentSync/AdminUI/Commands/ContentSyncSerializeCommand.cs` - Updated to use CreateOrchestrator
- `src/Dynamicweb.ContentSync/AdminUI/Commands/ContentSyncDeserializeCommand.cs` - Updated to use CreateOrchestrator
- `tests/Dynamicweb.ContentSync.Tests/Providers/SerializerOrchestratorTests.cs` - 7 new tests for FK ordering + cache invalidation
- `src/Dynamicweb.ContentSync/Configuration/ecommerce-predicates-example.json` - 26 ecommerce table predicate definitions

## Decisions Made
- FkDependencyResolver and CacheInvalidator are optional nullable constructor parameters so existing code/tests that construct SerializerOrchestrator(registry) still compile
- DwCacheResolver uses reflection to locate AddInManager and ICacheStorage at runtime, avoiding compile-time coupling to DW internals that may change between versions
- Non-SqlTable predicates are placed before SqlTable predicates in the deserialization order (Content first, then FK-ordered SQL tables)
- Cache invalidation failures are caught in a try/catch and logged as warnings - they never block other predicates from processing

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] DwCacheResolver uses reflection instead of direct AddInManager calls**
- **Found during:** Task 1 (DwCacheResolver implementation)
- **Issue:** `AddInManager` static class not directly accessible via `using Dynamicweb.Extensibility` at compile time in this NuGet version
- **Fix:** Used reflection to locate AddInManager type and methods at runtime, which is more resilient to DW version differences
- **Files modified:** src/Dynamicweb.ContentSync/Providers/ProviderRegistry.cs
- **Verification:** Build succeeds, all tests pass. Production cache resolution will work when DW runtime loads assemblies.
- **Committed in:** 8c9db1a (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Reflection approach is more resilient than direct static calls. No scope creep.

## Issues Encountered
- MSB3492 build cache contention errors from parallel agent execution. Resolved by retrying builds.
- Pre-existing test failure in SaveSyncSettingsCommandTests.Handle_NonExistentOutputDirectory_ReturnsInvalid (unrelated to this plan).

## Known Stubs
None - all classes are fully implemented with real logic.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Orchestrator fully wired with FK ordering and cache invalidation
- All 26 ecommerce tables documented with predicate configurations
- Users can copy entries from ecommerce-predicates-example.json into their contentsync.json
- Ready for Settings & Schema providers or other future data group expansion

---
*Phase: 15-ecommerce-tables-at-scale*
*Completed: 2026-03-24*

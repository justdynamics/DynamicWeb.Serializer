---
phase: 18-predicate-config-multi-provider
plan: 01
subsystem: admin-ui
tags: [predicate, multi-provider, sqltable, content, validation, data-layer]

# Dependency graph
requires:
  - phase: 14-content-provider-migration
    provides: ProviderPredicateDefinition model with all provider fields
provides:
  - PredicateEditModel with ProviderType, Table, NameColumn, CompareColumns, ServiceCaches properties
  - PredicateByIndexQuery mapping all ProviderPredicateDefinition fields for edit round-trip
  - SavePredicateCommand with provider-branched validation and construction
  - D-02 ProviderType lock enforcement on update
affects: [18-02, predicate-edit-screen, predicate-list-screen]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Provider-branched validation in save commands (Content vs SqlTable)"
    - "D-02 ProviderType lock: existing type preserved on update, Model.ProviderType used only for new"

key-files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs
    - src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs
    - src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs

key-decisions:
  - "Provider-branched validation: Content requires AreaId/PageId, SqlTable requires Table, unknown types rejected"
  - "D-02 enforcement in SavePredicateCommand: on update, ProviderType read from existing config predicate, not from model"
  - "Removed [Required] from AreaId/PageId on PredicateEditModel — validation moved to command for provider-specific logic"

patterns-established:
  - "Provider-branched validation: switch on providerType before field-specific validation"
  - "D-02 type lock: if Model.Index >= 0, use existing predicate's ProviderType"

requirements-completed: [PRED-01, PRED-02, PRED-03]

# Metrics
duration: 3min
completed: 2026-03-24
---

# Phase 18 Plan 01: Predicate Data Layer Summary

**Multi-provider predicate data layer with provider-branched validation, full field round-tripping, and D-02 type lock enforcement**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-24T19:14:20Z
- **Completed:** 2026-03-24T19:17:10Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- PredicateEditModel extended with 5 new properties (ProviderType, Table, NameColumn, CompareColumns, ServiceCaches)
- PredicateByIndexQuery maps all ProviderPredicateDefinition fields for complete edit round-trip
- SavePredicateCommand uses provider-branched validation and construction instead of hardcoded Content
- D-02 ProviderType lock: existing type preserved on update even if model is tampered
- 7 new tests covering SqlTable save, Content save, validation edges, and type lock

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend PredicateEditModel, PredicateByIndexQuery, and SavePredicateCommand** - `7d77f1d` (feat)
2. **Task 2: Add tests for multi-provider save command paths** - `12e31fe` (test)

## Files Created/Modified
- `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` - Added ProviderType, Table, NameColumn, CompareColumns, ServiceCaches; removed [Required] from AreaId/PageId
- `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs` - Maps all ProviderPredicateDefinition fields including SqlTable fields
- `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` - Provider-branched validation and construction with D-02 type lock
- `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` - 7 new tests, existing tests updated with ProviderType

## Decisions Made
- Removed [Required] attributes from AreaId/PageId since they are only required for Content type; validation moved to SavePredicateCommand
- D-02 type lock reads existing predicate's ProviderType from config on update, ignoring Model.ProviderType
- Unknown provider types return Invalid result rather than silently defaulting

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Data layer complete for both Content and SqlTable predicates
- Ready for Plan 02: UI screens (edit screen with ProviderType dropdown, list screen with type column)
- PredicateEditModel has all properties needed for provider-specific field groups

---
*Phase: 18-predicate-config-multi-provider*
*Completed: 2026-03-24*

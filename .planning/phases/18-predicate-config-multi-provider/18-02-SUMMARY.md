---
phase: 18-predicate-config-multi-provider
plan: 02
subsystem: admin-ui
tags: [predicate, multi-provider, edit-screen, list-screen, conditional-fields, provider-type]

# Dependency graph
requires:
  - phase: 18-predicate-config-multi-provider
    plan: 01
    provides: PredicateEditModel with all provider fields, SavePredicateCommand with provider-branched validation
provides:
  - PredicateEditScreen with ProviderType dropdown and conditional Content/SqlTable field groups
  - PredicateListModel with Type and Target columns replacing Path/AreaName
  - PredicateListQuery with provider-aware mapping
  - PredicateListScreen with Type and Target columns, renamed to "Serializer Predicates"
affects: [predicate-management, admin-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Conditional field groups in BuildEditScreen based on model property value"
    - "WithReloadOnChange on Select for new-only predicates (D-02 type lock UX)"
    - "Type+Target column pattern for multi-provider list display"

key-files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
    - src/DynamicWeb.Serializer/AdminUI/Models/PredicateListModel.cs
    - src/DynamicWeb.Serializer/AdminUI/Queries/PredicateListQuery.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateListScreen.cs

key-decisions:
  - "ProviderType Select with WithReloadOnChange only for new predicates (Index < 0), existing predicates show value without reload"
  - "Type+Target list column layout chosen per D-08: unified view for both Content and SqlTable"
  - "Null-conditional on Services.Areas for test environment safety"

patterns-established:
  - "Conditional field groups: check Model?.PropertyValue in BuildEditScreen to show/hide ComponentGroups"
  - "WithReloadOnChange scoped to new items only for locked-after-creation fields"

requirements-completed: [PRED-04, PRED-05]

# Metrics
duration: 2min
completed: 2026-03-24
---

# Phase 18 Plan 02: Provider-Type-Aware UI Screens Summary

**ProviderType dropdown with conditional Content/SqlTable field groups on edit screen, Type+Target columns on list screen**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-24T19:19:15Z
- **Completed:** 2026-03-24T19:20:58Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- PredicateEditScreen rebuilt with conditional field groups: Content shows Area/Page/Excludes, SqlTable shows Table/NameColumn/CompareColumns/ServiceCaches
- ProviderType dropdown with WithReloadOnChange for new predicates only (D-02 type lock UX)
- Empty ProviderType shows no provider-specific fields (D-09)
- ServiceCaches renders as Textarea matching Excludes pattern
- PredicateListModel/Query/Screen updated with Type and Target columns replacing old Path/AreaName
- List screen renamed from "Content Sync Predicates" to "Serializer Predicates"

## Task Commits

Each task was committed atomically:

1. **Task 1: Build provider-type-aware PredicateEditScreen with conditional field groups** - `e0b374e` (feat)
2. **Task 2: Update PredicateListModel, PredicateListQuery, and PredicateListScreen for type-aware display** - `6247825` (feat)

## Files Created/Modified
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` - ProviderType dropdown, conditional Content/SqlTable field groups, CreateProviderTypeSelect
- `src/DynamicWeb.Serializer/AdminUI/Models/PredicateListModel.cs` - Replaced Path/AreaName with Type/Target properties
- `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateListQuery.cs` - Provider-aware mapping: type label and target derivation from ProviderType
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateListScreen.cs` - Updated column mappings and screen name

## Decisions Made
- ProviderType Select uses WithReloadOnChange only for new predicates (Index < 0); existing predicates show current value without triggering form reload, complementing D-02 type lock in SavePredicateCommand
- Type+Target column layout chosen for list screen per D-08: "SQL Table" label for SqlTable, "Content" for Content; Target shows table name or area-qualified path
- Added null-conditional on Services.Areas in PredicateListQuery for test environment safety

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Known Stubs
None - all fields are wired to real data sources.

## Next Phase Readiness
- Phase 18 complete: both plans delivered
- Predicate edit and list screens fully support Content and SqlTable provider types
- DataGroup XML hints (D-04, D-05) deferred as optional enhancement; free-text input works for all tables

---
*Phase: 18-predicate-config-multi-provider*
*Completed: 2026-03-24*

## Self-Check: PASSED
- All 4 modified files exist on disk
- Commit e0b374e found (Task 1)
- Commit 6247825 found (Task 2)

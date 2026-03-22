---
phase: 09-predicate-management
plan: 02
subsystem: ui
tags: [admin-ui, list-screen, edit-screen, selectors, breadcrumb, predicates]

requires:
  - phase: 09-predicate-management
    provides: PredicateListModel, PredicateEditModel, PredicateListQuery, PredicateByIndexQuery, SavePredicateCommand, DeletePredicateCommand
  - phase: 08-settings-screen
    provides: AdminUI patterns (EditScreenBase, SyncSettingsNodeProvider tree wiring)
provides:
  - PredicateListScreen with Name/Path/Area columns, row navigation, context menu, and Add button
  - PredicateEditScreen with area selector (reload-on-change), page tree picker, and excludes textarea
  - Predicates tree node wired to PredicateListScreen via NavigateScreenAction
  - PredicateNavigationNodePathProvider for breadcrumb path
affects: [10-context-menus]

tech-stack:
  added: []
  patterns: [SelectorBuilder.CreateAreaSelector with WithReloadOnChange for dependent field reload, SelectorBuilder.CreatePageSelector with area filtering]

key-files:
  created:
    - src/Dynamicweb.ContentSync/AdminUI/Screens/PredicateListScreen.cs
    - src/Dynamicweb.ContentSync/AdminUI/Screens/PredicateEditScreen.cs
    - src/Dynamicweb.ContentSync/AdminUI/Tree/PredicateNavigationNodePathProvider.cs
  modified:
    - src/Dynamicweb.ContentSync/AdminUI/Tree/SyncSettingsNodeProvider.cs

key-decisions:
  - "Used PredicateListModel as NavigationNodePathProvider type (covers list screen breadcrumb)"
  - "Area selector placed before page selector in form layout for reload-on-change to update page tree scope"

patterns-established:
  - "SelectorBuilder pattern: CreateAreaSelector with WithReloadOnChange + CreatePageSelector with areaId filtering"
  - "NavigationNodePathProvider extends parent path by appending child node ID"

requirements-completed: [PRED-01, PRED-02, PRED-03, PRED-04, PRED-05, PRED-06]

duration: 2min
completed: 2026-03-22
---

# Phase 09 Plan 02: Predicate Admin UI Screens Summary

**Predicate list and edit screens with area/page selectors, tree node wiring, and breadcrumb navigation**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-22T00:17:57Z
- **Completed:** 2026-03-22T00:19:55Z
- **Tasks:** 1
- **Files modified:** 4

## Accomplishments
- PredicateListScreen showing Name, Path, Area columns with row-click navigation to edit, context menu (Edit + Delete with confirmation dialog), and Add button in toolbar
- PredicateEditScreen with area selector (reload-on-change updates page tree scope), page tree picker via SelectorBuilder, and multi-line excludes textarea
- Predicates tree node in SyncSettingsNodeProvider wired with NavigateScreenAction to PredicateListScreen
- PredicateNavigationNodePathProvider for breadcrumb path (Settings > Content > Sync > Predicates)

## Task Commits

Each task was committed atomically:

1. **Task 1: List screen, edit screen, tree node wiring, and breadcrumb** - `1e04f07` (feat)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/AdminUI/Screens/PredicateListScreen.cs` - List screen with RowViewMapping columns, primary action, context actions, create action
- `src/Dynamicweb.ContentSync/AdminUI/Screens/PredicateEditScreen.cs` - Edit screen with area selector, page selector, excludes textarea, and save command
- `src/Dynamicweb.ContentSync/AdminUI/Tree/PredicateNavigationNodePathProvider.cs` - Breadcrumb path provider extending sync path with Predicates node
- `src/Dynamicweb.ContentSync/AdminUI/Tree/SyncSettingsNodeProvider.cs` - Predicates node wired with NavigateScreenAction to PredicateListScreen

## Decisions Made
- Used PredicateListModel as the NavigationNodePathProvider type parameter (covers list screen breadcrumb; edit screen may need a separate provider at runtime if breadcrumb doesn't resolve, documented as potential concern)
- Placed AreaId field before PageId in form layout so WithReloadOnChange on area selector reloads the screen before page selector renders, ensuring page tree is scoped to correct area

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Pre-existing flaky test (`SaveSyncSettingsCommandTests.Handle_NonExistentOutputDirectory_ReturnsInvalid`) fails in full suite due to test ordering but passes in isolation. Not caused by this plan's changes. All 15 AdminUI tests pass.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Complete predicate CRUD admin UI is ready: list, edit, delete with tree node and breadcrumb
- Phase 09 (predicate-management) is fully complete
- Ready for Phase 10 (context-menus) which adds serialize/deserialize actions on content tree nodes

---
*Phase: 09-predicate-management*
*Completed: 2026-03-22*

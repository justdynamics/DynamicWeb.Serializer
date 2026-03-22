---
phase: 10-context-menu-actions
plan: 02
subsystem: ui
tags: [deserialize, file-upload, modal, prompt-screen, validate-then-apply, dry-run]

# Dependency graph
requires:
  - phase: 04-deserialization
    provides: ContentDeserializer with isDryRun support
  - phase: 10-context-menu-actions plan 01
    provides: ListScreenInjector context menu with serialize action
provides:
  - DeserializeModel with PageId, AreaId, UploadedFilePath, ImportMode
  - DeserializePromptScreen with FileUpload and mode Select editors
  - DeserializeSubtreeCommand with validate-then-apply (dry-run + real pass)
affects: [10-context-menu-actions plan 03]

# Tech tracking
tech-stack:
  added: []
  patterns: [PromptScreenBase with GetEditorForCommand override, Alert component for inline warnings, validate-then-apply via dual ContentDeserializer passes]

key-files:
  created:
    - src/Dynamicweb.ContentSync/AdminUI/Models/DeserializeModel.cs
    - src/Dynamicweb.ContentSync/AdminUI/Queries/DeserializePromptQuery.cs
    - src/Dynamicweb.ContentSync/AdminUI/Screens/DeserializePromptScreen.cs
    - src/Dynamicweb.ContentSync/AdminUI/Commands/DeserializeSubtreeCommand.cs
  modified: []

key-decisions:
  - "Override GetEditorForCommand to provide FileUpload and Select editors for command property binding"
  - "Used Alert component (AlertType.Warning) from Dynamicweb.CoreUI.Displays.Information for overwrite warning"
  - "DataQueryModelBase<T> is the correct base class for queries (not QueryBase<T>)"

patterns-established:
  - "GetEditorForCommand override: return custom editor types (FileUpload, Select) matched by property name for PromptScreenBase"
  - "Alert component for inline warnings in modal screens"

requirements-completed: [ACT-06, ACT-07, ACT-08]

# Metrics
duration: 2min
completed: 2026-03-22
---

# Phase 10 Plan 02: Deserialize Prompt Screen and Command Summary

**Deserialize modal with FileUpload (.zip), 3-mode Select, overwrite Alert warning, and validate-then-apply command using dual ContentDeserializer passes (dry-run + real)**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-22T12:43:33Z
- **Completed:** 2026-03-22T12:45:58Z
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments
- DeserializePromptScreen with properly bound FileUpload and Select editors via EditorForCommand + GetEditorForCommand override
- Alert warning component always visible for overwrite mode destructive action notice (per D-07)
- DeserializeSubtreeCommand validates entire zip via dry-run pass before touching DB (per D-08)
- Summary message with success/failure counts returned to user (per D-09)
- ContentDeserializer reused for both passes -- no deserialization logic duplication (ACT-08)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create DeserializeModel and DeserializePromptQuery** - `3882b9c` (feat)
2. **Task 2: Create DeserializePromptScreen and DeserializeSubtreeCommand** - `31b6a8f` (feat)

## Files Created/Modified
- `src/Dynamicweb.ContentSync/AdminUI/Models/DeserializeModel.cs` - DataViewModelBase with PageId, AreaId, UploadedFilePath, ImportMode
- `src/Dynamicweb.ContentSync/AdminUI/Queries/DeserializePromptQuery.cs` - Query carrying page context to prompt screen
- `src/Dynamicweb.ContentSync/AdminUI/Screens/DeserializePromptScreen.cs` - PromptScreenBase modal with file upload, mode select, and overwrite warning
- `src/Dynamicweb.ContentSync/AdminUI/Commands/DeserializeSubtreeCommand.cs` - Command with zip extraction, dry-run validation, and real apply

## Decisions Made
- Override `GetEditorForCommand` to provide `FileUpload` and `Select` editors for command property binding -- this is the correct PromptScreenBase pattern for custom editor types
- Used `Alert` component (`Dynamicweb.CoreUI.Displays.Information.Alert`) with `AlertType.Warning` for the overwrite warning tip -- confirmed via DW10 source it extends `UiComponentBase` and can be added via `AddComponent`
- Used `DataQueryModelBase<T>` (not `QueryBase<T>`) as the query base class -- consistent with existing project queries (SyncSettingsQuery)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Fixed query base class from QueryBase to DataQueryModelBase**
- **Found during:** Task 1
- **Issue:** Plan specified `QueryBase<DeserializeModel>` but the project uses `DataQueryModelBase<T>` -- `QueryBase<T>` does not exist in the DW CoreUI namespace
- **Fix:** Changed to `DataQueryModelBase<DeserializeModel>` matching existing project pattern
- **Files modified:** src/Dynamicweb.ContentSync/AdminUI/Queries/DeserializePromptQuery.cs
- **Verification:** Build succeeded with 0 errors
- **Committed in:** 3882b9c (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Necessary correction for compilation. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Deserialize prompt screen and command ready for integration with ListScreenInjector (Plan 03)
- The injector will use `OpenDialogAction.To<DeserializePromptScreen>().With(new DeserializePromptQuery { ... })` to open this modal

## Self-Check: PASSED

All 4 files exist. Both commit hashes verified in git log.

---
*Phase: 10-context-menu-actions*
*Completed: 2026-03-22*

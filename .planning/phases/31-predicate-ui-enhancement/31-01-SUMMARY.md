---
phase: 31-predicate-ui-enhancement
plan: 01
subsystem: admin-ui
tags: [ui, predicate-edit, filtering, textarea]
dependency_graph:
  requires: [phase-27, phase-28, phase-29]
  provides: [predicate-ui-filtering-fields]
  affects: [predicate-edit-screen, predicate-save-command, predicate-query]
tech_stack:
  added: []
  patterns: [textarea-newline-list, conditional-visibility-by-provider]
key_files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs
    - src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs
    - src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
decisions:
  - XmlColumns textarea only in SqlTable Filtering group (not Content)
  - Shared parsing block for all three fields before provider-type branch
  - Filtering group placed after provider-specific settings group
metrics:
  duration: 2min
  completed: "2026-04-07T20:36:34Z"
  tasks: 2
  files: 5
---

# Phase 31 Plan 01: Predicate UI Enhancement Summary

Three textarea editors (ExcludeFields, XmlColumns, ExcludeXmlElements) added to predicate edit screen with full save/load round-trip and conditional XmlColumns visibility for SqlTable only.

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | 7f18f72 | Add model properties, query loading, save command persistence, and 3 new tests |
| 2 | e70b72e | Add Filtering group with textarea editors to predicate edit screen |

## Task Details

### Task 1: Model, Query, Command, and Tests

Added `ExcludeFields`, `XmlColumns`, `ExcludeXmlElements` string properties to `PredicateEditModel` with `[ConfigurableProperty]` attributes. Updated `PredicateByIndexQuery` to join lists into newline-separated strings on load. Updated `SavePredicateCommand` with shared textarea parsing for all three fields, with `XmlColumns` only assigned in the SqlTable branch. Added 3 tests proving Content/SqlTable round-trip and Content XmlColumns exclusion.

### Task 2: Screen Textarea Editors

Added "Filtering" layout group to both Content and SqlTable branches of `BuildEditScreen()`. Content gets ExcludeFields + ExcludeXmlElements. SqlTable gets XmlColumns + ExcludeFields + ExcludeXmlElements. Added three `Textarea` editor cases in `GetEditor()` switch expression.

## Verification

- Build: zero errors
- Tests: 20/20 passed (17 existing + 3 new)
- All acceptance criteria met (grep checks for field presence in all files)

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Self-Check: PASSED

All 5 modified files exist. Both commits (7f18f72, e70b72e) verified in git log.

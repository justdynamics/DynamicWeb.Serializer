---
phase: 19-source-id-serialization
plan: 01
subsystem: serialization
tags: [yaml, dto, content-mapper, source-id, backward-compat]

requires:
  - phase: 18-predicate-config-multi-provider
    provides: current codebase with SerializedPage/SerializedParagraph DTOs and ContentMapper
provides:
  - SourcePageId (int?) on SerializedPage DTO
  - SourceParagraphId (int?) on SerializedParagraph DTO
  - ContentMapper populates source IDs from DW Page.ID and Paragraph.ID
  - ContentTreeBuilder.BuildSinglePageWithSourceId test helper
affects: [20-link-resolution-core, 21-paragraph-anchor-resolution]

tech-stack:
  added: []
  patterns: [nullable source ID fields for backward-compatible YAML extension]

key-files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/Models/SerializedPage.cs
    - src/DynamicWeb.Serializer/Models/SerializedParagraph.cs
    - src/DynamicWeb.Serializer/Serialization/ContentMapper.cs
    - tests/DynamicWeb.Serializer.Tests/Models/DtoTests.cs
    - tests/DynamicWeb.Serializer.Tests/Infrastructure/YamlRoundTripTests.cs
    - tests/DynamicWeb.Serializer.Tests/Fixtures/ContentTreeBuilder.cs

key-decisions:
  - "SourcePageId and SourceParagraphId are nullable int? for backward compatibility with existing YAML"

patterns-established:
  - "Nullable source ID pattern: add int? properties to DTOs when extending YAML format without breaking existing files"

requirements-completed: [SER-01, SER-02]

duration: 3min
completed: 2026-04-03
---

# Phase 19 Plan 01: Source ID Serialization Summary

**Nullable SourcePageId/SourceParagraphId added to DTOs and ContentMapper for source-to-target ID mapping in deserialization**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-03T12:25:27Z
- **Completed:** 2026-04-03T12:28:11Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- Added `int? SourcePageId` to SerializedPage DTO (nullable for backward compatibility)
- Added `int? SourceParagraphId` to SerializedParagraph DTO (nullable for backward compatibility)
- ContentMapper.MapPage populates SourcePageId from `page.ID`
- ContentMapper.MapParagraph populates SourceParagraphId from `paragraph.ID`
- 8 new unit tests (4 DTO + 4 YAML round-trip) covering source IDs and backward compat
- ContentTreeBuilder.BuildSinglePageWithSourceId helper for future test convenience

## Task Commits

Each task was committed atomically:

1. **Task 1: Add SourcePageId and SourceParagraphId to DTOs and ContentMapper** - `1dead97` (feat)
2. **Task 2: Add unit tests for source ID fields and YAML round-trip** - `9aed07a` (test)

## Files Created/Modified
- `src/DynamicWeb.Serializer/Models/SerializedPage.cs` - Added `int? SourcePageId` after PageUniqueId
- `src/DynamicWeb.Serializer/Models/SerializedParagraph.cs` - Added `int? SourceParagraphId` after ParagraphUniqueId
- `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs` - Populate SourcePageId from page.ID and SourceParagraphId from paragraph.ID
- `tests/DynamicWeb.Serializer.Tests/Models/DtoTests.cs` - 4 new DTO tests for source ID defaults and values
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/YamlRoundTripTests.cs` - 4 new YAML round-trip tests for source IDs
- `tests/DynamicWeb.Serializer.Tests/Fixtures/ContentTreeBuilder.cs` - BuildSinglePageWithSourceId helper

## Decisions Made
- SourcePageId and SourceParagraphId are nullable `int?` (not `required`) so existing YAML files without these fields still deserialize correctly to null

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- **Pre-existing test compilation errors:** The test project has 31 compile errors in `Providers/SqlTable/` test files (IdentityResolutionTests.cs, FlatFileStoreTests.cs, SqlTableProviderDeserializeTests.cs) that prevent `dotnet test` from running. These errors are pre-existing and unrelated to Phase 19 changes. All errors are `CS0029` (string[] to List<string>) and `CS0854` (expression tree optional arguments). The main project builds successfully with 0 errors. New test code compiles cleanly (verified by checking no errors outside SqlTable files). Logged to `deferred-items.md`.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None - all properties are wired to real data sources (page.ID, paragraph.ID).

## Next Phase Readiness
- Phase 20 (Link Resolution Core) can now read SourcePageId from serialized YAML to build source-to-target ID mapping
- Phase 21 (Paragraph Anchor Resolution) can now read SourceParagraphId for paragraph fragment resolution
- Pre-existing SqlTable test errors should be fixed before Phase 20 to enable full test suite execution

## Self-Check: PASSED

- All 6 modified files exist on disk
- Commit 1dead97 (Task 1) found in git log
- Commit 9aed07a (Task 2) found in git log

---
*Phase: 19-source-id-serialization*
*Completed: 2026-04-03*

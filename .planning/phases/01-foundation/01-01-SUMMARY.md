---
phase: 01-foundation
plan: "01"
subsystem: models
tags: [yamldotnet, dotnet, csharp, dto, yaml, serialization, xunit]

# Dependency graph
requires: []
provides:
  - Five plain C# record DTOs (SerializedArea, SerializedPage, SerializedGridRow, SerializedGridColumn, SerializedParagraph) with no Dynamicweb dependencies
  - YamlConfiguration.BuildSerializer() and BuildDeserializer() factory methods with CamelCase naming and ForceStringScalarEmitter
  - ForceStringScalarEmitter: DoubleQuoted for all strings except LF-only multiline (Literal)
  - ContentTreeBuilder test fixture with tricky string samples (tilde, HTML, CRLF)
  - Proven YAML round-trip fidelity for tilde, CRLF, HTML, double quotes, bang, and empty string
affects:
  - 01-02 (FileSystemStore — will consume DTOs and YamlConfiguration)
  - Phase 2 (Configuration — will use SerializedArea/Page for predicate evaluation)
  - Phase 3 (DW serialization — will map DW API objects to these DTOs)
  - Phase 4 (Deserialization — will read these DTOs from YAML)

# Tech tracking
tech-stack:
  added:
    - "YamlDotNet 16.3.0 (YAML serialization/deserialization)"
    - "xunit 2.9.3 (unit test framework)"
    - "xunit.runner.visualstudio 3.1.5 (test runner)"
    - "Moq 4.20.72 (mocking)"
    - "Microsoft.Extensions.Configuration.Json 8.0.1 (JSON config loading)"
    - "Microsoft.NET.Test.Sdk 17.11.1"
  patterns:
    - "Plain C# record DTOs with no framework dependencies as the isolation boundary"
    - "ChainedEventEmitter for YamlDotNet scalar style configuration"
    - "YamlConfiguration static factory — BuildSerializer/BuildDeserializer called once per use site"
    - "ContentTreeBuilder fixture pattern for shared test data construction"

key-files:
  created:
    - "Dynamicweb.ContentSync.sln"
    - "src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj"
    - "src/Dynamicweb.ContentSync/Models/SerializedArea.cs"
    - "src/Dynamicweb.ContentSync/Models/SerializedPage.cs"
    - "src/Dynamicweb.ContentSync/Models/SerializedGridRow.cs"
    - "src/Dynamicweb.ContentSync/Models/SerializedGridColumn.cs"
    - "src/Dynamicweb.ContentSync/Models/SerializedParagraph.cs"
    - "src/Dynamicweb.ContentSync/Infrastructure/ForceStringScalarEmitter.cs"
    - "src/Dynamicweb.ContentSync/Infrastructure/YamlConfiguration.cs"
    - "tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj"
    - "tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs"
    - "tests/Dynamicweb.ContentSync.Tests/Infrastructure/YamlRoundTripTests.cs"
    - "tests/Dynamicweb.ContentSync.Tests/Models/DtoTests.cs"
    - ".gitignore"
  modified: []

key-decisions:
  - "ForceStringScalarEmitter uses DoubleQuoted (not Literal) for CRLF strings — YAML literal block scalars normalize \\r\\n to \\n per spec, losing the carriage return"
  - "Literal block style only for LF-only multiline strings (contains \\n but not \\r)"
  - "DoubleQuoted is the safe default for all other strings including tilde, bang, and empty string"

patterns-established:
  - "DTO pattern: plain C# records with required keyword, init-only properties, and nullable annotations"
  - "YAML safety: always use YamlConfiguration factory methods — never configure serializer inline"
  - "Test fixtures: static ContentTreeBuilder class with BuildSampleTree() and BuildSinglePage() methods"

requirements-completed:
  - SER-01

# Metrics
duration: 5min
completed: 2026-03-19
---

# Phase 1 Plan 01: Project Scaffolding, DTOs, and YAML Infrastructure Summary

**Five plain C# record DTOs + YamlDotNet serializer with ForceStringScalarEmitter proving round-trip fidelity for tilde, CRLF, HTML, quotes, and bang**

## Performance

- **Duration:** 5 minutes
- **Started:** 2026-03-19T13:00:04Z
- **Completed:** 2026-03-19T13:05:42Z
- **Tasks:** 2
- **Files modified:** 14

## Accomplishments

- Created .NET 8.0 solution with two projects (main library + xunit test project), all NuGet packages restored
- Five plain C# record DTOs covering the full DynamicWeb content hierarchy with no Dynamicweb.* dependencies
- YamlConfiguration factory with ForceStringScalarEmitter — proven safe YAML scalar style for all known-tricky CMS strings
- 15 tests all passing: 10 round-trip fidelity tests (tilde, CRLF, HTML, double quotes, bang, normal, empty, full page, dictionary, determinism) + 5 DTO shape tests

## Task Commits

Each task was committed atomically:

1. **Task 1: Project scaffolding, DTO records, and YAML infrastructure** - `ca84911` (feat)
2. **Task 2: YAML round-trip fidelity tests and DTO shape tests** - `97dfea3` (feat)

_Note: Task 2 included an auto-fix to ForceStringScalarEmitter (CRLF handling) committed in the same task commit._

## Files Created/Modified

- `Dynamicweb.ContentSync.sln` - Solution file referencing both projects
- `src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj` - Main project (net8.0, YamlDotNet 16.3.0, Extensions.Configuration.Json 8.0.1)
- `src/Dynamicweb.ContentSync/Models/SerializedArea.cs` - DTO: AreaId, Name, SortOrder, Pages list
- `src/Dynamicweb.ContentSync/Models/SerializedPage.cs` - DTO: PageUniqueId, Name, MenuText, UrlName, SortOrder, IsActive, audit fields, Fields dict, GridRows list
- `src/Dynamicweb.ContentSync/Models/SerializedGridRow.cs` - DTO: Id, SortOrder, Columns list
- `src/Dynamicweb.ContentSync/Models/SerializedGridColumn.cs` - DTO: Id, Width, Paragraphs list
- `src/Dynamicweb.ContentSync/Models/SerializedParagraph.cs` - DTO: ParagraphUniqueId, SortOrder, ItemType, Header, Fields dict, audit fields
- `src/Dynamicweb.ContentSync/Infrastructure/ForceStringScalarEmitter.cs` - Custom ChainedEventEmitter for scalar style control
- `src/Dynamicweb.ContentSync/Infrastructure/YamlConfiguration.cs` - BuildSerializer/BuildDeserializer factory methods
- `tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj` - Test project (xunit 2.9.3, Moq 4.20.72)
- `tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs` - Static fixture builder with tricky string samples
- `tests/Dynamicweb.ContentSync.Tests/Infrastructure/YamlRoundTripTests.cs` - 10 round-trip fidelity tests
- `tests/Dynamicweb.ContentSync.Tests/Models/DtoTests.cs` - 5 DTO shape and hierarchy tests
- `.gitignore` - Excludes bin/, obj/, .vs/, TestResults/

## Decisions Made

- Used `DoubleQuoted` scalar style for strings containing `\r` (CRLF or CR alone): YAML literal block scalars normalize `\r\n` to `\n` per spec, silently dropping carriage returns. DoubleQuoted preserves `\r` via backslash escaping.
- Used `Literal` block style only for LF-only multiline strings (contains `\n` but not `\r`): cleaner git diffs for HTML/text content.
- Created `.gitignore` at project root to prevent build artifacts from being committed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ForceStringScalarEmitter CRLF round-trip data loss**
- **Found during:** Task 2 (YAML round-trip fidelity tests)
- **Issue:** The emitter used `ScalarStyle.Literal` for any string containing `\n` or `\r`. YAML literal block scalars normalize `\r\n` to `\n` per spec, so `Hello\r\nWorld` deserialized as `Hello\nWorld`, causing the `Yaml_RoundTrips_TrickyString` and `Yaml_RoundTrips_FullPage_WithPopulatedFields` tests to fail.
- **Fix:** Changed condition to use `Literal` only when string contains `\n` but NOT `\r`; all other strings (including CRLF) fall through to `DoubleQuoted` which preserves `\r` via escape sequence.
- **Files modified:** `src/Dynamicweb.ContentSync/Infrastructure/ForceStringScalarEmitter.cs`
- **Verification:** All 15 tests pass including both CRLF round-trip cases.
- **Committed in:** `97dfea3` (Task 2 commit)

**2. [Rule 3 - Blocking] Added explicit xunit using directives to test files**
- **Found during:** Task 2 (first test run)
- **Issue:** `ImplicitUsings` enabled in test csproj but xunit namespaces (`Xunit`) are not included in implicit usings — `[Fact]`, `[Theory]`, `Assert` not found.
- **Fix:** Added `using Xunit;` to both test files.
- **Files modified:** `YamlRoundTripTests.cs`, `DtoTests.cs`
- **Verification:** Build succeeded, all tests run.
- **Committed in:** `97dfea3` (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking)
**Impact on plan:** Both auto-fixes essential for correctness. The CRLF fix is particularly important — it proves the emitter handles all known-tricky strings correctly before any file I/O is written.

## Issues Encountered

- dotnet CLI (v10.0) now generates `.slnx` files by default instead of `.sln`. The plan specifies `Dynamicweb.ContentSync.sln`, so the solution file was created manually in classic Visual Studio format.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- DTOs are stable and ready for consumption by Phase 1 Plan 02 (FileSystemStore)
- YamlConfiguration is the proven serialization layer for all subsequent phases
- ContentTreeBuilder fixture is reusable for FileSystemStore tests in next plan
- CRLF handling proven correct before any YAML files are written to disk

## Self-Check: PASSED

All 14 files created and found. Both task commits (ca84911, 97dfea3) verified in git log.

---
*Phase: 01-foundation*
*Completed: 2026-03-19*

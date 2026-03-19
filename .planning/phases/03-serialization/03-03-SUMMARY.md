---
phase: 03-serialization
plan: 03
subsystem: integration-testing
tags: [integration-tests, yaml, serialization, csharp, dotnet, dynamicweb, scheduled-task]

# Dependency graph
requires:
  - phase: 03-serialization
    plan: 02
    provides: ContentSerializer, ContentMapper, ReferenceResolver, FileSystemStore

provides:
  - Integration test project (tests/Dynamicweb.ContentSync.IntegrationTests)
  - SerializeScheduledTask addin for running serializer inside DW host context
  - Live verification: 73 YAML files from Customer Center (pageid=8385, areaId=3)
  - YamlDotNet downgrade to 13.7.1 (matching DW bundled version)
  - SER-03, INF-02, INF-03 confirmed against production content

affects:
  - 04-deserialization (consumers can trust YAML schema produced by live pipeline)
  - Deployment workflow (SerializeScheduledTask is the integration entrypoint)

# Tech tracking
tech-stack:
  added:
    - xunit 2.9.3 (integration test project)
    - Microsoft.NET.Test.Sdk 17.11.1
    - xunit.runner.visualstudio 3.1.5
    - SerializeScheduledTask (DW ScheduledTask addin as integration entrypoint)
  patterns:
    - "Integration tests cannot run standalone — DW services require host context; pivoted to ScheduledTask addin"
    - "YamlDotNet version must match DW's bundled version to avoid assembly conflicts"
    - "Config at wwwroot/Files/ContentSync.config.json — standard DW writable location"
    - "GUID references validated by inspecting GlobalRecordPageGuid in paragraph.yml files"

key-files:
  created:
    - tests/Dynamicweb.ContentSync.IntegrationTests/Dynamicweb.ContentSync.IntegrationTests.csproj
    - tests/Dynamicweb.ContentSync.IntegrationTests/Serialization/CustomerCenterSerializationTests.cs
  modified:
    - src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj (YamlDotNet 16.3.0 -> 13.7.1)
    - src/Dynamicweb.ContentSync/AddIns/SerializeScheduledTask.cs (new file in project)

key-decisions:
  - "Pivoted from xunit integration tests to ScheduledTask addin — DW services cannot be initialized outside the DW host process; scheduled tasks run inside it"
  - "YamlDotNet downgraded from 16.3.0 to 13.7.1 to match DW's bundled version and avoid runtime assembly binding conflicts"
  - "Config file at wwwroot/Files/ContentSync.config.json — DW standard pattern for add-in config in writable Files directory"
  - "areaId=3 (not 1) for Customer Center tree on the target Swift2.2 instance — discovered at runtime"

requirements-completed: [SER-03, INF-02, INF-03]

# Metrics
duration: ~60min (including live DW verification)
completed: 2026-03-19
---

# Phase 3 Plan 03: Integration Tests and Live Serialization Verification Summary

**Serialization pipeline verified live against Swift2.2: 73 YAML files produced from Customer Center tree (pageid=8385) with GUID identity, reference resolution, and mirror folder hierarchy**

## Performance

- **Duration:** ~60 min (including live DW deployment and verification)
- **Completed:** 2026-03-19
- **Tasks:** 2 (1 auto + 1 human-verify checkpoint)
- **Files modified:** 4

## Accomplishments

- Created integration test project with 5 tests covering SER-03 (structure, GUID-only, idempotency), INF-02 (field fidelity), and hierarchy (recursive children)
- Added `SerializeScheduledTask` addin as the practical integration entrypoint (DW ScheduledTask runs inside the host process where Services.* are available)
- Downgraded YamlDotNet from 16.3.0 to 13.7.1 to resolve DW assembly binding conflict
- Added diagnostic logging to ContentSerializer for visibility into serialization progress
- Deployed DLLs to Swift2.2 and ran serialization via ScheduledTask
- **Live verification passed:** 73 YAML files written to `C:\temp\ContentSyncTest`
  - Customer Center tree (pageid=8385, areaId=3) fully serialized
  - GUIDs present in all page.yml and paragraph.yml files
  - Reference fields (GlobalRecordPageGuid) correctly resolved to GUIDs (not numeric IDs)
  - Mirror-tree folder structure matches DW admin hierarchy

## Task Commits

Each task committed atomically:

1. **Task 1: Create integration test project and CustomerCenter serialization tests** - `6858824` (feat)
2. **Additional: SerializeScheduledTask, YamlDotNet downgrade, diagnostic logging** - `89afc02` (feat)

Task 2 (checkpoint:human-verify) required no code commit — human verification of live YAML output.

## Files Created/Modified

- `tests/Dynamicweb.ContentSync.IntegrationTests/Dynamicweb.ContentSync.IntegrationTests.csproj` - New integration test project targeting net8.0 with xunit 2.9.3
- `tests/Dynamicweb.ContentSync.IntegrationTests/Serialization/CustomerCenterSerializationTests.cs` - 5 integration tests (structure, GUID-only, field fidelity, idempotency, child pages)
- `src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj` - YamlDotNet downgraded 16.3.0 -> 13.7.1
- `src/Dynamicweb.ContentSync/AddIns/SerializeScheduledTask.cs` - ScheduledTask addin calling ContentSerializer.Serialize()

## Decisions Made

- Integration tests cannot run standalone from `dotnet test` — DW services (`Services.Pages`, `Services.Grids`, etc.) require host initialization that only occurs inside the DW process. Pivoted to `SerializeScheduledTask` as the integration entrypoint.
- YamlDotNet 13.7.1 chosen to match the version bundled with Dynamicweb.Core, preventing assembly version conflicts at runtime.
- Config placed at `wwwroot/Files/ContentSync.config.json` — the standard DW pattern for add-in configuration in the writable Files directory.
- areaId discovered to be 3 (not 1) for the Customer Center tree on the Swift2.2 test instance.

## Deviations from Plan

### Architectural Pivot (User-Approved)

**1. [Pivot] Integration tests cannot run standalone — pivoted to ScheduledTask addin**
- **Found during:** Task 2 preparation
- **Issue:** `dotnet test` outside the DW host process cannot call `Services.Pages.GetPage()` — DW services are not initialized without the host
- **Fix:** Added `SerializeScheduledTask` (DW ScheduledTask addin) as integration entrypoint; the ScheduledTask runs inside the DW host process where all services are available
- **Files modified:** `src/Dynamicweb.ContentSync/AddIns/SerializeScheduledTask.cs` (new)
- **Committed in:** `89afc02`

**2. [Rule 3 - Blocking] YamlDotNet downgraded 16.3.0 -> 13.7.1**
- **Found during:** Task 2 deployment
- **Issue:** Runtime assembly binding conflict between YamlDotNet 16.3.0 and the version bundled with Dynamicweb.Core
- **Fix:** Downgraded to 13.7.1 to match DW's bundled version
- **Files modified:** `src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj`
- **Committed in:** `89afc02`

**3. [Rule 2 - Missing] Added diagnostic logging to ContentSerializer**
- **Found during:** Task 2 (needed visibility into serialization progress during live run)
- **Fix:** Added structured logging calls throughout ContentSerializer for progress and error visibility
- **Files modified:** `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs`
- **Committed in:** `89afc02`

---

**Total deviations:** 1 architectural pivot (user-approved), 2 auto-fixed (Rules 2-3)
**Impact on plan:** Integration test project exists as planned; entrypoint changed from xunit test runner to ScheduledTask addin. Verification results identical to plan success criteria.

## Live Verification Results (Task 2 - Human Approved)

- **Output directory:** `C:\temp\ContentSyncTest`
- **Files produced:** 73 YAML files
- **Tree root:** Customer Center (pageid=8385, areaId=3)
- **GUID identity:** Confirmed in all page.yml and paragraph.yml files
- **Reference resolution:** GlobalRecordPageGuid fields resolved to GUIDs (no numeric IDs)
- **Folder structure:** Mirror-tree matches DW admin page hierarchy
- **Verdict:** APPROVED

## Next Phase Readiness

- YAML schema produced by live pipeline is validated — deserialization phase can consume it with confidence
- SerializeScheduledTask provides a repeatable integration entrypoint for regression testing
- YamlDotNet version pinned to 13.7.1 — must remain aligned with DW bundled version in future upgrades
- Config pattern established (wwwroot/Files/ContentSync.config.json) — usable for phase 4 deserialization config

---
*Phase: 03-serialization*
*Completed: 2026-03-19*

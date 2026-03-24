---
phase: 16-admin-ux
plan: 01
subsystem: infra
tags: [rename, namespace, csproj, sln, dotnet]

# Dependency graph
requires:
  - phase: 15-ecommerce-sql
    provides: "Complete provider architecture and ecommerce table predicates"
provides:
  - "DynamicWeb.Serializer namespace across all source files"
  - "DynamicWeb.Serializer.dll assembly output"
  - "DynamicWeb.Serializer NuGet package"
  - "SerializerSerialize/SerializerDeserialize API commands"
  - "Serializer.config.json with ContentSync.config.json backward compat"
  - "Updated README with new identity and upgrade path"
affects: [16-02, 16-03, 16-04, all-future-phases]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-step git mv for Windows case-sensitivity changes"
    - "Config file backward compat via candidate path array (new name first, old name fallback)"

key-files:
  created:
    - "DynamicWeb.Serializer.sln"
    - "src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj"
    - "tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj"
    - "tests/DynamicWeb.Serializer.IntegrationTests/DynamicWeb.Serializer.IntegrationTests.csproj"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs"
    - "src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs"
  modified:
    - "All 89 .cs files in src/ and tests/ (namespace + using + class renames)"
    - "README.md"

key-decisions:
  - "Node IDs in tree provider changed from ContentSync_* to Serializer_* (breaking change for any persisted nav state, acceptable for rename)"
  - "Log file renamed from ContentSync.log to Serializer.log"
  - "Zip export prefix changed from ContentSync_ to Serializer_"
  - "1 pre-existing flaky test (Handle_NonExistentOutputDirectory_ReturnsInvalid) left unchanged -- environment-dependent path validation issue on Windows"

patterns-established:
  - "Config backward compat: Serializer.config.json checked first, ContentSync.config.json as fallback"
  - "DynamicWeb.Serializer root namespace convention for all future code"

requirements-completed: [REN-01]

# Metrics
duration: 9min
completed: 2026-03-24
---

# Phase 16 Plan 01: Rename to DynamicWeb.Serializer Summary

**Full project rename from Dynamicweb.ContentSync to DynamicWeb.Serializer: 96 files across solution, projects, namespaces, classes, API commands, config paths, and README**

## Performance

- **Duration:** 9 min
- **Started:** 2026-03-24T12:41:22Z
- **Completed:** 2026-03-24T12:50:35Z
- **Tasks:** 1
- **Files modified:** 96

## Accomplishments
- Renamed entire project identity: solution, 3 projects, 3 directories, all namespaces and usings
- Renamed 10+ classes from ContentSync/Sync prefix to Serializer prefix (commands, screens, models, queries, tree nodes)
- Config file backward compatibility: Serializer.config.json checked first, ContentSync.config.json as fallback
- Updated README with new API endpoints, deployment upgrade notes, and Settings > Database > Serialize navigation
- Build succeeds with 0 errors, 220/221 tests pass (1 pre-existing flaky test)

## Task Commits

Each task was committed atomically:

1. **Task 1: Rename solution, projects, directories, and all namespace/using references** - `84ecda2` (feat)

## Files Created/Modified
- `DynamicWeb.Serializer.sln` - Renamed solution with updated project paths
- `src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` - Updated RootNamespace, AssemblyName, PackageId, metadata
- `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` - Updated RootNamespace, ProjectReference
- `tests/DynamicWeb.Serializer.IntegrationTests/DynamicWeb.Serializer.IntegrationTests.csproj` - Updated RootNamespace, ProjectReference
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs` - Renamed from ContentSyncSerializeCommand
- `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs` - Renamed from ContentSyncDeserializeCommand
- `src/DynamicWeb.Serializer/AdminUI/Commands/SaveSerializerSettingsCommand.cs` - Renamed from SaveSyncSettingsCommand
- `src/DynamicWeb.Serializer/AdminUI/Injectors/SerializerPageEditInjector.cs` - Renamed from ContentSyncPageListInjector
- `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` - Renamed from SyncSettingsNodeProvider
- `src/DynamicWeb.Serializer/AdminUI/Screens/SerializerSettingsEditScreen.cs` - Renamed from SyncSettingsEditScreen
- `src/DynamicWeb.Serializer/AdminUI/Models/SerializerSettingsModel.cs` - Renamed from SyncSettingsModel
- `src/DynamicWeb.Serializer/AdminUI/Queries/SerializerSettingsQuery.cs` - Renamed from SyncSettingsQuery
- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` - Renamed from SyncConfiguration
- `src/DynamicWeb.Serializer/Configuration/ConfigPathResolver.cs` - Serializer.config.json first, ContentSync.config.json fallback
- `README.md` - Complete rewrite with new identity, API endpoints, upgrade path, Settings > Database > Serialize

## Decisions Made
- Node IDs in tree provider changed from ContentSync_* to Serializer_* (clean break, no backward compat needed for nav state)
- Log file changed to Serializer.log (ContentSync.log references in commands updated)
- All error messages updated to reference Serializer.config.json with "(also checked ContentSync.config.json)" note
- Screen name changed from "Content Sync Settings" to "Serialize Settings" (per D-10)
- Action group name changed from "Content Sync" to "Serialize"

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Git stash conflict during test investigation (resolved by resetting and re-adding file)
- One pre-existing flaky test `Handle_NonExistentOutputDirectory_ReturnsInvalid` fails because `\NonExistent\Path\...` resolves to a valid root-drive path on Windows. This is not caused by the rename and exists in the prior codebase.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All source files use DynamicWeb.Serializer namespace
- Assembly output is DynamicWeb.Serializer.dll
- API commands are SerializerSerialize / SerializerDeserialize
- Config backward compat in place for existing deployments
- Ready for Plan 02 (log viewer) and subsequent admin UX work

---
*Phase: 16-admin-ux*
*Completed: 2026-03-24*

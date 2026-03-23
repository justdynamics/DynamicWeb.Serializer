---
phase: 12-permission-deserialization-docs
plan: 01
subsystem: serialization
tags: [permissions, deserialization, security, safety-fallback]

# Dependency graph
requires:
  - phase: 11-permission-serialization
    provides: PermissionMapper with MapPermissions, SerializedPermission model, GetLevelName
provides:
  - ParseLevelName static method reversing GetLevelName for all 6 permission levels
  - ApplyPermissions instance method restoring role/group permissions from YAML
  - Group name cache (lazy, case-insensitive) for efficient group resolution
  - Safety fallback denying Anonymous access when groups are missing on target
  - ContentDeserializer wiring in both CREATE and UPDATE page paths
affects: [12-permission-deserialization-docs]

# Tech tracking
tech-stack:
  added: []
  patterns: [lazy-cached-group-lookup, safety-fallback-on-missing-groups, source-wins-permission-clear]

key-files:
  created:
    - tests/Dynamicweb.ContentSync.Tests/Serialization/PermissionDeserializationTests.cs
  modified:
    - src/Dynamicweb.ContentSync/Serialization/PermissionMapper.cs
    - src/Dynamicweb.ContentSync/Serialization/ContentDeserializer.cs

key-decisions:
  - "Lazy group name cache built on first ApplyPermissions call and reused across pages"
  - "Existing permissions cleared before applying serialized state (source-wins model)"
  - "Safety fallback applied once per page when any group is unresolvable"

patterns-established:
  - "ParseLevelName/GetLevelName symmetry for permission level round-trip"
  - "Lazy field cache pattern (_groupNameCache) for cross-page reuse in batch operations"

requirements-completed: [PERM-04, PERM-05, PERM-06, PERM-07]

# Metrics
duration: 3min
completed: 2026-03-23
---

# Phase 12 Plan 01: Permission Deserialization Summary

**Permission restoration from YAML with role-name matching, group-name resolution via cached lookup, and Anonymous=None safety fallback for missing groups**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-23T08:47:29Z
- **Completed:** 2026-03-23T08:50:30Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- ParseLevelName reverses all 6 permission level names with case-insensitive matching
- ApplyPermissions restores role permissions by name, resolves groups via cached name lookup
- Safety fallback sets Anonymous=None when any referenced group is missing on target
- ContentDeserializer calls ApplyPermissions after page save in both CREATE and UPDATE paths
- Dry-run mode logs permission counts without applying changes
- 15 unit tests for ParseLevelName pass (all levels, case variants, unknown string throws)

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED): Add failing tests for ParseLevelName** - `1c33080` (test)
2. **Task 1 (GREEN): Add ParseLevelName + ApplyPermissions to PermissionMapper** - `c6110e9` (feat)
3. **Task 2: Wire ApplyPermissions into ContentDeserializer pipeline** - `2755bf2` (feat)

_Note: Task 1 used TDD (RED then GREEN commits)_

## Files Created/Modified
- `tests/Dynamicweb.ContentSync.Tests/Serialization/PermissionDeserializationTests.cs` - Unit tests for ParseLevelName (15 tests)
- `src/Dynamicweb.ContentSync/Serialization/PermissionMapper.cs` - Added ParseLevelName, ApplyPermissions, BuildGroupNameCache
- `src/Dynamicweb.ContentSync/Serialization/ContentDeserializer.cs` - Wired PermissionMapper into deserialization pipeline

## Decisions Made
- Lazy group name cache built on first ApplyPermissions call and reused across pages in the same run
- Existing explicit permissions cleared before applying serialized state (source-wins model)
- Safety fallback applied once per page (not per missing group) when any group is unresolvable
- ParseLevelName throws ArgumentException for unknown strings; caller falls back to LevelValue cast

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Permission round-trip complete: serialize (Phase 11) and deserialize (this plan)
- Ready for Plan 02 (README documentation of permission handling behavior)

---
*Phase: 12-permission-deserialization-docs*
*Completed: 2026-03-23*

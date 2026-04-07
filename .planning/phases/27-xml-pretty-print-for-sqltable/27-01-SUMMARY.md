---
phase: 27-xml-pretty-print-for-sqltable
plan: 01
subsystem: infra
tags: [xml, yaml-literal-block, pretty-print, sqltable-pipeline, config-mapping]

# Dependency graph
requires:
  - phase: 26-xml-pretty-print-for-content
    provides: "XmlFormatter utility (PrettyPrint + Compact) and ForceStringScalarEmitter"
provides:
  - "SqlTable XML pretty-printing via config-driven xmlColumns list"
  - "FlatFileStore literal block scalar support for multiline SQL YAML values"
  - "SqlTableProvider XML compact on deserialize for idempotent round-trips"
affects: [28-field-level-filtering, 31-admin-ui]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Config-driven xmlColumns list for SqlTable predicates", "Three-class mapping pattern extended for xmlColumns (Pitfall P7)"]

key-files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs
    - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
    - src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs
    - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs
    - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreTests.cs
    - tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs

key-decisions:
  - "XML formatting at SqlTableProvider boundary (Serialize/Deserialize methods), not in FlatFileStore -- keeps FlatFileStore general-purpose"
  - "ForceStringScalarEmitter added to FlatFileStore for literal block scalars on all multiline strings"
  - "CompactXmlColumns as separate static method called after CoerceRowTypes in deserialize path"

patterns-established:
  - "xmlColumns config pattern: per-predicate list of column names, explicit not heuristic"
  - "Three-class config mapping verified with round-trip test (Pitfall P7 prevention)"

requirements-completed: [XML-02]

# Metrics
duration: 5min
completed: 2026-04-07
---

# Phase 27 Plan 01: XML Pretty-Print for SqlTable Summary

**Config-driven xmlColumns on SqlTable predicates with XmlFormatter.PrettyPrint in serialize, XmlFormatter.Compact in deserialize, and ForceStringScalarEmitter in FlatFileStore for readable YAML literal block scalars**

## Performance

- **Duration:** 5 min
- **Started:** 2026-04-07T19:38:44Z
- **Completed:** 2026-04-07T19:44:05Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- Added XmlColumns property to ProviderPredicateDefinition with full three-class config mapping (Model + RawPredicateDefinition + BuildPredicate)
- Added ForceStringScalarEmitter to FlatFileStore serializer for YAML literal block scalar emission on multiline strings
- Integrated XmlFormatter.PrettyPrint into SqlTableProvider.Serialize loop for xmlColumns
- Integrated XmlFormatter.Compact into SqlTableProvider.Deserialize after type coercion for idempotent round-trips
- Added 4 new tests: literal block scalar emission, multiline XML round-trip, config loading with/without xmlColumns

## Task Commits

Each task was committed atomically:

1. **Task 1: Add xmlColumns config + ForceStringScalarEmitter to FlatFileStore** - `1315afa` (feat)
2. **Task 2: Integrate XML pretty-print/compact into SqlTableProvider serialize/deserialize** - `21a200c` (feat)

## Files Created/Modified
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` - Added XmlColumns list property
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` - Added XmlColumns to RawPredicateDefinition and BuildPredicate mapping
- `src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs` - Added ForceStringScalarEmitter to serializer builder
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` - PrettyPrint in Serialize, CompactXmlColumns in Deserialize, using Infrastructure
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreTests.cs` - 2 new tests for literal block and round-trip, fixed pre-existing assertion
- `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` - 2 new tests for xmlColumns config mapping

## Decisions Made
- XML formatting applied at SqlTableProvider boundary (Serialize/Deserialize methods), consistent with Phase 26 pattern of formatting at mapping boundary
- ForceStringScalarEmitter added to FlatFileStore rather than duplicating XML-specific emitter logic
- CompactXmlColumns implemented as separate static method since CoerceRowTypes is static and does not have predicate access

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed WriteRow_PreservesNullValues test assertion for ForceStringScalarEmitter format**
- **Found during:** Task 2 (verification)
- **Issue:** Adding ForceStringScalarEmitter to FlatFileStore changed YAML output format (keys now double-quoted), causing existing test assertion to fail on format check
- **Fix:** Updated assertion to check for column name presence regardless of quoting style
- **Files modified:** tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreTests.cs
- **Verification:** Test passes, round-trip test confirms null values still survive correctly
- **Committed in:** 21a200c (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug fix)
**Impact on plan:** Necessary fix for test compatibility with ForceStringScalarEmitter. No scope creep.

## Issues Encountered

5 pre-existing test failures detected (4 SqlTableProviderDeserializeTests + 1 SaveSerializerSettingsCommandTests). Verified these fail on the prior commit without any Phase 27 changes. Out of scope -- not caused by this plan's changes.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- SqlTable XML pretty-printing complete end-to-end via config-driven xmlColumns
- Config example files can add xmlColumns to SqlTable predicates (e.g., `"xmlColumns": ["ShippingXml", "SettingsXml"]`)
- Ready for Phase 28 (field-level filtering) which may add more config fields using the same three-class pattern

---
*Phase: 27-xml-pretty-print-for-sqltable*
*Completed: 2026-04-07*

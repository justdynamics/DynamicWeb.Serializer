---
phase: 28-field-level-filtering-core
plan: 01
subsystem: serialization
tags: [field-filtering, config, deserialize-guard, xml-filtering]
dependency_graph:
  requires: [Phase 26 XmlFormatter, Phase 27 three-class config pattern]
  provides: [ExcludeFields config, ExcludeXmlElements config, deserialize skip guard, XmlFormatter.RemoveElements]
  affects: [ContentMapper, ContentSerializer, ContentDeserializer, ConfigLoader, ProviderPredicateDefinition]
tech_stack:
  added: []
  patterns: [field exclusion via IReadOnlySet, XML element stripping via XDocument.Descendants, WriteContext-carried excludeFields]
key_files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs
    - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
    - src/DynamicWeb.Serializer/Serialization/ContentMapper.cs
    - src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs
    - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
    - src/DynamicWeb.Serializer/Infrastructure/XmlFormatter.cs
    - tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs
    - tests/DynamicWeb.Serializer.Tests/Infrastructure/XmlFormatterTests.cs
decisions:
  - ExcludeFields carried via WriteContext.ExcludeFields rather than threading through every method parameter
  - XmlFormatter.RemoveElements implemented as standalone static method for reuse by future SqlTable filtering
  - ApplyXmlElementFilter helper in ContentMapper wraps RemoveElements with null/empty check
metrics:
  duration: 8min
  completed: 2026-04-07
  tasks: 3
  files: 8
---

# Phase 28 Plan 01: Field-Level Filtering Core Summary

ExcludeFields and ExcludeXmlElements config with three-class mapping, serialize-side field filtering in ContentMapper, atomic deserialize skip guard in ContentDeserializer, and XmlFormatter.RemoveElements for XML element stripping.

## What Was Done

### Task 1: Config + Serialize-Side Field Filtering (FILT-01)
- Added `ExcludeFields` and `ExcludeXmlElements` list properties to `ProviderPredicateDefinition`
- Extended `RawPredicateDefinition` and `BuildPredicate()` in ConfigLoader (three-class mapping, Pitfall P7)
- Added `excludeFields` parameter (IReadOnlySet<string>?) to ContentMapper: MapArea, MapPage, MapParagraph, BuildColumns, ExtractItemFields, ExtractPropertyItemFields
- Added `excludeXmlElements` parameter (IReadOnlyList<string>?) to MapPage, MapParagraph, BuildColumns
- ContentSerializer builds HashSet from predicate.ExcludeFields and threads through SerializePage to all mapper calls
- Added `ApplyXmlElementFilter` helper and `XmlFormatter.RemoveElements` method
- 3 new ConfigLoaderTests verify config round-trip
- **Commit:** 66a8317

### Task 2: Deserialize Skip Guard (FILT-03 -- CRITICAL)
- Added `ExcludeFields` property to WriteContext, built with OrdinalIgnoreCase in DeserializePredicate
- Added skip guard to `SaveItemFields`: `if (excludeFields != null && excludeFields.Contains(fieldName)) continue;`
- Added identical skip guard to `SavePropertyItemFields`
- Updated all 10 call sites (1 area, 4 page, 3 gridrow, 2 paragraph) to pass `ctx.ExcludeFields`
- **Commit:** 5844662

### Task 3: XmlFormatter.RemoveElements Tests (FILT-04)
- 7 new unit tests: null, empty, non-XML, matching elements, case-insensitive, empty names list, nested elements
- All 23 XmlFormatterTests pass (16 existing + 7 new)
- **Commit:** 2f34e18

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build` | 0 errors |
| ConfigLoaderTests (26 total) | All pass |
| XmlFormatterTests (23 total) | All pass |
| excludeFields in ContentDeserializer (case-insensitive) | 17 references |
| Skip guard pattern (2 locations) | SaveItemFields + SavePropertyItemFields |
| Full test suite | 335 pass, 4 pre-existing failures (SqlTable/AdminUI unrelated) |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] XmlFormatter.RemoveElements implemented in Task 1 instead of Task 3**
- **Found during:** Task 1
- **Issue:** ContentMapper.ApplyXmlElementFilter calls RemoveElements, which must exist for compile
- **Fix:** Implemented RemoveElements in Task 1 alongside the mapper changes; Task 3 added only tests
- **Files modified:** src/DynamicWeb.Serializer/Infrastructure/XmlFormatter.cs

**2. [Rule 3 - Blocking] WriteContext used instead of parameter threading for excludeFields**
- **Found during:** Task 2
- **Issue:** 10 call sites span 4 levels of nesting; threading an extra parameter through every method is fragile
- **Fix:** Added ExcludeFields to WriteContext which already flows through all deserialization methods
- **Files modified:** src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs

## Self-Check: PASSED

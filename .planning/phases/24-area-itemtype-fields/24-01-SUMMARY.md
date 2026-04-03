---
phase: 24-area-itemtype-fields
plan: 01
subsystem: serialization
tags: [area, itemtype, item-fields, link-resolution]
dependency_graph:
  requires: []
  provides: [area-itemtype-serialization, area-item-link-resolution]
  affects: [content-mapper, content-deserializer, serialized-area-dto]
tech_stack:
  added: []
  patterns: [item-field-extraction-via-SerializeTo, SaveItemFields-reuse, ResolveLinksInItemFields-reuse]
key_files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/Models/SerializedArea.cs
    - src/DynamicWeb.Serializer/Serialization/ContentMapper.cs
    - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
    - tests/DynamicWeb.Serializer.Tests/Infrastructure/YamlRoundTripTests.cs
decisions:
  - Reuse existing Services.Items.GetItem + SerializeTo pattern from GridRow for area item extraction
  - Reuse existing SaveItemFields for area item deserialization (no new save logic)
  - Reuse existing ResolveLinksInItemFields for area link resolution (no new resolver logic)
  - Area ItemType is NOT set during deserialization -- only field values are written (ItemType must already be configured)
metrics:
  duration: 2min
  completed: "2026-04-03T18:04:37Z"
---

# Phase 24 Plan 01: Area ItemType Fields Summary

Area ItemType and item field serialization/deserialization with page ID link resolution in field values, reusing existing item field patterns from GridRow and page handling.

## What Was Done

### Task 1: Extend SerializedArea DTO + ContentMapper.MapArea + ContentDeserializer area item saving (AREA-01)
**Commit:** 61876db

- Added `ItemType` (string?) and `ItemFields` (Dictionary<string, object>) properties to SerializedArea DTO
- Extended ContentMapper.MapArea to extract area item fields using Services.Items.GetItem + item.SerializeTo pattern (same as GridRow)
- Extended ContentDeserializer.DeserializePredicate to save area item fields via existing SaveItemFields method before page processing
- Added YAML round-trip test verifying area with ItemType and ItemFields serializes/deserializes correctly

### Task 2: Resolve page ID links in area ItemType fields (AREA-02)
**Commit:** 2d19220

- Added area-level link resolution at the top of ResolveLinksInArea method
- Loads target area, checks for ItemType and ItemId, calls existing ResolveLinksInItemFields
- Area item field links (e.g., HeaderPage="Default.aspx?ID=121") are rewritten to target environment IDs
- Resolution runs before per-page resolution for correct dependency ordering

## Deviations from Plan

None -- plan executed exactly as written.

## Known Stubs

None -- all functionality is fully wired.

## Verification

- Build: 0 errors (16 warnings, all pre-existing)
- Tests: 296 passed, 5 failed (all pre-existing SqlTable/Orchestrator failures unrelated to this plan)
- New test Yaml_RoundTrips_AreaWithItemTypeFields: PASSED

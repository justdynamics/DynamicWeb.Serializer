---
phase: 20-link-resolution-core
plan: 02
subsystem: serialization
tags: [link-resolution, deserialization, internal-links]
dependency_graph:
  requires: [20-01-InternalLinkResolver]
  provides: [phase-2-link-resolution-in-deserializer]
  affects: [ContentDeserializer]
tech_stack:
  added: []
  patterns: [two-phase-deserialization, universal-field-scanning]
key_files:
  modified:
    - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
decisions:
  - Separate ResolveLinksInPropertyItem method instead of unified call (DW Page lacks PropertyItemType property)
  - Universal string field scanning with no field-type allowlist
metrics:
  duration: 2min
  completed: "2026-04-03"
---

# Phase 20 Plan 02: Wire Phase 2 Link Resolution Summary

Phase 2 internal link resolution wired into ContentDeserializer.DeserializePredicate after page tree walk, scanning all string fields universally across pages, paragraphs, and PropertyItems with dry-run gating and stats logging.

## What Was Done

### Task 1: Wire Phase 2 link resolution into DeserializePredicate

Added Phase 2 link resolution block in DeserializePredicate after the page foreach loop. Builds source-to-target map from area.Pages + PageGuidCache, then scans all item fields for Default.aspx?ID=NNN patterns.

Three new private methods added:
- `ResolveLinksInArea` - iterates all pages in area, processes page items, PropertyItems, and paragraph items
- `ResolveLinksInItemFields` - loads item via Services.Items.GetItem, serializes fields, scans strings, re-saves if changed
- `ResolveLinksInPropertyItem` - loads PropertyItem directly from page, same scan/replace/save pattern

**Commit:** f39a6e9

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Separate ResolveLinksInPropertyItem method | DW Page class has no PropertyItemType property; PropertyItem accessed directly via page.PropertyItem |
| Universal string field scanning | All string field values scanned per LINK-03; no field-type allowlist |
| Empty map skips resolution | sourceToTarget.Count == 0 gracefully skips (backward compat with pre-v0.3.1 YAML) |
| Only saves changed items | anyChanged flag prevents unnecessary writes per Pitfall 5 |
| Dry-run gating | _isDryRun check before item.Save() per Pitfall 6 |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] PropertyItemType not available on DW Page**
- **Found during:** Task 1
- **Issue:** Plan code referenced `page.PropertyItemType` which does not exist on the DW Page class
- **Fix:** Created separate `ResolveLinksInPropertyItem(Page page, InternalLinkResolver resolver)` method that accesses `page.PropertyItem` directly (matching existing `SavePropertyItemFields` pattern)
- **Files modified:** ContentDeserializer.cs
- **Commit:** f39a6e9

## Verification

- `dotnet build src/DynamicWeb.Serializer --no-restore` -- 0 errors
- `dotnet test --filter InternalLinkResolver` -- 16/16 passed
- Full test suite: 278 passed, 6 pre-existing failures (SqlTable, AdminUI, Configuration -- unrelated)
- grep confirms: BuildSourceToTargetMap(1), ResolveLinksInArea(2), ResolveLinksInItemFields(3), InternalLinkResolver(5), Link resolution stats(1), _isDryRun gating present

## Known Stubs

None - all code is fully wired with no placeholder data.

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | f39a6e9 | Wire Phase 2 link resolution into ContentDeserializer |

## Self-Check: PASSED

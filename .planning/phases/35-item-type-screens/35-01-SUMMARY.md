---
phase: 35-item-type-screens
plan: 01
subsystem: admin-ui
tags: [item-types, tree-node, live-api, category-nesting]
dependency_graph:
  requires: [configuration, admin-ui-tree]
  provides: [item-type-list-screen, item-type-models, item-type-tree-node]
  affects: [35-02]
tech_stack:
  added: []
  patterns: [live-api-discovery, category-tree-nesting]
key_files:
  created:
    - src/DynamicWeb.Serializer/AdminUI/Models/ItemTypeListModel.cs
    - src/DynamicWeb.Serializer/AdminUI/Models/ItemTypeEditModel.cs
    - src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeListQuery.cs
    - src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeListScreen.cs
    - src/DynamicWeb.Serializer/AdminUI/Tree/ItemTypeNavigationNodePathProvider.cs
  modified:
    - src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs
decisions:
  - Live API discovery via ItemManager.Metadata.GetMetadata() -- no scan or persist pattern
  - Category nesting uses full path segments from ItemTypeCategory.FullName
  - Leaf nodes navigate to list screen temporarily until Plan 02 creates edit screen
metrics:
  duration: ~2min
  completed: "2026-04-15T08:49:38Z"
  tasks_completed: 2
  tasks_total: 2
---

# Phase 35 Plan 01: Item Type List and Tree Node Summary

Live-discovered item type browsing via DW ItemManager API with category-based tree nesting under Serialize node.

## What Was Done

### Task 1: Models and List Query (ea875df)
Created three files for item type data representation and live API discovery:
- **ItemTypeListModel**: DataViewModelBase with SystemName, DisplayName, Category, FieldCount, ExcludedFieldCount columns
- **ItemTypeEditModel**: DataViewModelBase + IIdentifiable with ExcludedFields property for Plan 02
- **ItemTypeListQuery**: Live discovery using ItemManager.Metadata.GetMetadata().Items, no SQL, case-insensitive config lookup for excluded field counts, ordered by Category then DisplayName

### Task 2: List Screen, Tree Node, Breadcrumb (b771bd4)
Created list screen and wired item types into the admin tree:
- **ItemTypeListScreen**: ListScreenBase with 5 columns, no scan/create actions (live discovery)
- **SerializerSettingsNodeProvider**: Added ItemTypes node at Sort=12, with recursive category nesting from DW API. Top-level groups by first category segment, sub-folders for nested categories, leaf nodes for individual types. Uncategorized folder for types without category. Try-catch for graceful degradation.
- **ItemTypeNavigationNodePathProvider**: Breadcrumb path through Settings > Database > Serialize > Item Types

## Deviations from Plan

None - plan executed exactly as written.

## Self-Check: PASSED

- All 6 files verified present on disk
- Commits ea875df and b771bd4 verified in git log
- Build succeeds with 0 errors

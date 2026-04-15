---
phase: 35-item-type-screens
fixed_at: 2026-04-14T00:00:00Z
review_path: .planning/phases/35-item-type-screens/35-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 35: Code Review Fix Report

**Fixed at:** 2026-04-14
**Source review:** .planning/phases/35-item-type-screens/35-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 4
- Fixed: 4
- Skipped: 0

## Fixed Issues

### WR-01: Unhandled exception in `ItemTypeListQuery` when config is corrupt

**Files modified:** `src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeListQuery.cs`
**Commit:** a23f93a
**Applied fix:** Wrapped the `ConfigLoader.Load(configPath)` call inside a `try/catch` block. On exception the catch body leaves `excludeMap` as `null`, so the list renders without exclusion counts rather than crashing the screen.

### WR-02: Field count in list screen diverges from edit screen

**Files modified:** `src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeListQuery.cs`
**Commit:** a23f93a
**Applied fix:** Added a two-line comment above `FieldCount = t.Fields?.Count ?? 0` explicitly documenting that this is declared-fields-only and that `GetItemFields` (which includes inherited fields) is intentionally avoided here due to per-row cost. This is the documentation path recommended by the reviewer as the cheaper alternative.

### WR-03: `ConfigLoader.Load` called without try/catch in navigation tree branches

**Files modified:** `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs`
**Commit:** 2483a04
**Applied fix:** In the `PredicatesNodeId` branch and the `EmbeddedXmlNodeId` branch, replaced the bare `var config = ConfigLoader.Load(configPath)` with an explicit `SerializerConfiguration config; try { config = ConfigLoader.Load(configPath); } catch { yield break; }` pattern, matching the reviewer's suggested fix exactly. A corrupt config now causes a graceful empty branch rather than an exception into the DW navigation renderer.

### WR-04: `StartsWith` without `StringComparison` in node path matching

**Files modified:** `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs`
**Commit:** 2483a04
**Applied fix:** Changed `parentNodePath.Last.StartsWith(ItemTypeCatPrefix)` to `parentNodePath.Last.StartsWith(ItemTypeCatPrefix, StringComparison.Ordinal)` to avoid locale-sensitive case-folding on internal ASCII node ID strings.

---

_Fixed: 2026-04-14_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

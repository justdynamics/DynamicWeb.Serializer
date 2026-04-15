# Phase 36: Area Screens - Context

**Gathered:** 2026-04-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Add per-area column exclusions to the Content predicate edit screen via a `SelectMultiDual` section. No separate "Areas" tree node — area column exclusions are managed directly on each Content predicate. Column discovery uses DW Area API properties, not SQL. Storage is per-predicate on `ProviderPredicateDefinition`.

**Scope change from ROADMAP.md:** The roadmap originally specified a separate "Areas" tree node. After discussion, the user decided to integrate area column exclusions into the existing predicate edit screen instead. This is a simplification — fewer files, no new tree node, no new screens. Requirements AREA-06/07/08 are still met but through the predicate UI rather than a standalone screen.

</domain>

<decisions>
## Implementation Decisions

### Architecture
- **D-01:** **No separate "Areas" tree node or screen.** Area column exclusions are added as a new section on the existing `PredicateEditScreen` for Content predicates.
- **D-02:** Storage is **per-predicate** — add `excludeAreaColumns` (List<string>) to `ProviderPredicateDefinition`. Each Content predicate can exclude different area columns independently.

### Area Discovery
- **D-03:** Discover areas via **DW Area API** (`Services.Areas`). Consistent with existing usage in `PredicateListQuery`.

### Column Discovery
- **D-04:** Discover area columns via **DW Area API properties** — reflect on the `Area` object's properties rather than SQL `INFORMATION_SCHEMA`. Shows only properties DW exposes.

### UI
- **D-05:** Add a **SelectMultiDual** section to `PredicateEditScreen` for Content predicates (those with an `AreaId`). Populated with area property names from DW API. Pre-selected values from the predicate's `excludeAreaColumns`.
- **D-06:** `SelectMultiDual` is the standard control (per Phase 34 D-01/D-03).

### Claude's Discretion
- How to enumerate Area properties (reflection on Area class, or a known property list)
- Where in the predicate edit screen to place the area columns section (after existing fields, before XML elements)
- Whether to show area name/ID as read-only context above the selector
- How to handle predicates where AreaId is not set or area doesn't exist

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Predicate Edit Screen (current)
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` — Current edit screen with Content/SqlTable sections, SelectMultiDual for columns
- `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` — Model with [ConfigurableProperty] attributes
- `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` — Save command parsing model to config
- `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs` — Query loading predicate data

### Config Infrastructure
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` — Per-predicate model (add ExcludeAreaColumns here)
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` — Config loading with RawPredicateDefinition
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` — Atomic JSON save

### DW Area API (research needed)
- `C:/Projects/temp/dw10source/` — Search for Area class, Services.Areas, area properties to discover column names

### Phase 34/35 Patterns
- `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs` — SelectMultiDual usage pattern
- `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs` — Live API discovery + SelectMultiDual

### Serialization Pipeline
- `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs` — Where area column exclusions need to be applied
- `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` — Area serialization entry point

### Requirements
- `.planning/REQUIREMENTS.md` — AREA-06 through AREA-08

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PredicateEditScreen` already has Content/SqlTable conditional sections — add Area columns section to Content path
- `SelectMultiDual` pattern established in Phase 34/35
- `SavePredicateCommand` already handles parsing model properties to `ProviderPredicateDefinition`
- `Services.Areas` already used in `PredicateListQuery` for area name resolution

### Established Patterns
- Content predicates have AreaId — use this to look up the area and discover its properties
- Per-predicate fields stored as `List<string>` on `ProviderPredicateDefinition` (same as ExcludeFields, XmlColumns)
- Edit model uses newline-separated string, save command splits to list

### Integration Points
- `PredicateEditModel` — add ExcludeAreaColumns property
- `PredicateEditScreen.BuildEditScreen()` — add SelectMultiDual section in Content group
- `SavePredicateCommand` — parse and save ExcludeAreaColumns
- `PredicateByIndexQuery` — load ExcludeAreaColumns from config
- `ProviderPredicateDefinition` — add ExcludeAreaColumns field
- `ContentMapper` / `ContentSerializer` — apply area column exclusions during serialization

</code_context>

<specifics>
## Specific Ideas

- Simpler than originally planned — no new tree node, no new screens, just extending the existing predicate edit screen
- Per-predicate storage means different Content predicates can exclude different area columns

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 36-area-screens*
*Context gathered: 2026-04-15*

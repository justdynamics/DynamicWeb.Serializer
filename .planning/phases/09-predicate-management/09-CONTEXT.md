# Phase 9: Predicate Management - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

CRUD management for content sync predicates from the DW admin UI. The Predicates sub-node already exists under Settings > Content > Sync (Phase 7, D-09) but has no NodeAction. This phase adds a list screen, edit screen, and delete command — all persisting to ContentSync.config.json via the existing ConfigWriter/ConfigLoader infrastructure. The existing PredicateDefinition model (Name, Path, AreaId, Excludes) is the data shape. Index-based identity (no DB IDs).

</domain>

<decisions>
## Implementation Decisions

### List Screen
- **D-01:** List screen uses `ListScreenBase<T>` with `RowViewMapping` — same pattern as ExpressDelivery PresetListScreen
- **D-02:** Columns: Name, Path, Area Name (resolved from AreaId via `Services.Areas`)
- **D-03:** No sorting or filtering — flat list, most installs will have 1-5 predicates
- **D-04:** Empty state uses DW's default empty list rendering (no custom message) — Add button always visible in toolbar
- **D-05:** Row click navigates to edit screen. Right-click context menu shows Edit + Delete (ActionBuilder pattern from ExpressDelivery)
- **D-06:** Add button in toolbar creates new predicate (navigates to blank edit screen)

### Edit Screen (Add/Edit Form)
- **D-07:** Edit screen uses `EditScreenBase<T>` with fields: Name (text, required), Path (content tree picker, required), Area (dropdown of DW areas, required), Excludes (multi-line textarea, optional)
- **D-08:** Area field is a Select dropdown populated from `Dynamicweb.Content.Services.Areas` — shows area names, stores numeric AreaId
- **D-09:** Path field uses DW's content tree picker (page selection UI) — NOT free-text. This is a hard requirement for usability.
- **D-10:** Excludes entered as multi-line text (one path per line). Split on newlines when saving to the `Excludes` string list.
- **D-11:** Validation: Name required, Path required, Area required. Unique name enforced (reject duplicate predicate names). Path existence validated against the selected area's content tree.
- **D-12:** Save writes immediately to ContentSync.config.json via ConfigWriter (same pattern as Phase 8 SaveSyncSettingsCommand)

### Delete Behavior
- **D-13:** Standard DW confirmation dialog via `ActionBuilder.Delete` — "Are you sure you want to delete predicate '{Name}'?"
- **D-14:** Delete is immediate — writes to config file on confirm (no batched save)
- **D-15:** Deleting the last predicate is allowed — zero predicates = nothing syncs, valid state

### Identity
- **D-16:** Predicates are identified by array index in the JSON config (no DB-assigned IDs). Edit/delete commands use the index to locate the target predicate.

### Claude's Discretion
- How to implement the content tree picker (research DW's page selection UI pattern — check DW10 source for TreeView/PagePicker components)
- How to resolve area name from AreaId for the list column display
- DataViewModelBase vs DataQueryModelBase for the list model
- Navigation wiring between list screen and edit screen (query parameter passing for index-based identity)
- How to handle the "new predicate" case (index = -1 or similar sentinel)

</decisions>

<specifics>
## Specific Ideas

- Content tree picker is a hard requirement — if DW's internal picker is not discoverable, research must find the pattern before planning
- ExpressDelivery PresetListScreen is the primary pattern reference for list + CRUD
- The Predicates sub-node already exists in SyncSettingsNodeProvider (line 42-47) — just needs NodeAction wired
- PredicateDefinition record already has the right shape — no model changes needed
- Excludes as multi-line text is a UX simplification; the underlying model is `List<string>`

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing ContentSync Infrastructure (extend these)
- `src/Dynamicweb.ContentSync/AdminUI/Tree/SyncSettingsNodeProvider.cs` — Predicates sub-node at line 42-47 needs NodeAction
- `src/Dynamicweb.ContentSync/AdminUI/Tree/SyncNavigationNodePathProvider.cs` — Breadcrumb path needs Predicates entry
- `src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs` — Data model: Name, Path, AreaId, Excludes
- `src/Dynamicweb.ContentSync/Configuration/ContentPredicate.cs` — Runtime predicate evaluation (read-only, no changes needed)
- `src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs` — Predicates list property
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` — JSON deserialization
- `src/Dynamicweb.ContentSync/Configuration/ConfigWriter.cs` — JSON serialization
- `src/Dynamicweb.ContentSync/Configuration/ConfigPathResolver.cs` — Config file discovery

### DW Extension Patterns (reference implementations)
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Screens\ExpressDeliveryPresetListScreen.cs` — ListScreenBase with RowViewMapping, context actions, create button
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Screens\ExpressDeliveryPresetEditScreen.cs` — EditScreenBase pattern
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Commands\DeleteExpressDeliveryPresetCommand.cs` — Delete command pattern
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Commands\SaveExpressDeliveryPresetCommand.cs` — Save command pattern
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Queries\ExpressDeliveryByIdQuery.cs` — Query by ID for edit screen
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Models\ExpressDeliveryPresetDataModel.cs` — List data model

### DW Source & Patterns (for picker research)
- `C:\Projects\temp\dw10source\` — Search for content tree picker, page selection UI, TreeView components
- `C:\VibeCode\DynamicWeb.AIDiagnoser\` — Prior project with DW admin UI integration learnings

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SyncSettingsNodeProvider`: Already has Predicates sub-node shell — just add NodeAction
- `SyncNavigationNodePathProvider`: Already handles Sync breadcrumb — extend for Predicates
- `ConfigWriter.Save()` + `ConfigLoader.Load()`: Full round-trip for Predicates array already works
- `ConfigPathResolver.FindConfigFile()`: Config discovery shared across all UI screens
- Phase 8's CQRS pattern (Query loads fresh from disk, Command saves via ConfigWriter): Reuse for predicate CRUD

### Established Patterns
- `ListScreenBase<T>` with `RowViewMapping` for list columns
- `EditScreenBase<T>` with `EditorFor()` for form fields
- `ActionBuilder.Edit<>()` and `ActionBuilder.Delete()` for context menu actions
- `NavigateScreenAction.To<>().With(query)` for screen navigation with query parameters
- `CommandBase<T>` returning `CommandResult` for save/delete operations
- `ConfigurableProperty` attribute for field labels and descriptions

### Integration Points
- Predicates sub-node wiring in SyncSettingsNodeProvider
- Config file read/write via ConfigLoader/ConfigWriter (Predicates array)
- Area name resolution via `Dynamicweb.Content.Services.Areas` API
- Content tree picker via DW's page selection UI (needs research)

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 09-predicate-management*
*Context gathered: 2026-03-22*

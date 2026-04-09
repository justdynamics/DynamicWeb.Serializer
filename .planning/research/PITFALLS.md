# Pitfalls Research: UI Configuration Improvements (v0.6.0)

**Domain:** Adding structured UI configuration to DynamicWeb.Serializer -- tab injection, config schema migration, auto-discovery, structured pickers
**Researched:** 2026-04-07
**Confidence:** HIGH (verified against DW10 source code at C:\Projects\temp\dw10source\)

## Critical Pitfalls

### Pitfall 1: EditScreenInjector Cannot Save Custom Properties to External Config

**What goes wrong:**
The plan calls for a "Serialization" tab on PageEditScreen/ParagraphEditScreen for per-item-type field exclusion. The natural approach is `EditScreenInjector<PageEditScreen, PageDataModel>` to add a new tab with editors. However, **injectors can only add editors for properties already on the screen's DataModel**. `PageDataModel` is owned by DW's `Dynamicweb.Content.UI` assembly. The save command (`PageSaveCommand`) reads from `GetModel()` which only knows `PageDataModel` properties. Custom serializer config properties have nowhere to be persisted through the standard save flow.

Verified in DW10 source: `PageEditScreenInjector` (Dynamicweb.Global.UI) adds ecommerce navigation editors, but all those fields (`NavigationUseEcomGroups`, `NavigationProvider`, etc.) are properties already defined on `PageDataModel` itself. The injector pattern is designed for cross-module UI composition where the model already has the fields -- not for adding entirely new data stores.

The `EditScreenInjector` interface provides:
- `OnBuildEditScreen(EditScreenBuilder builder)` -- adds UI components
- `GetEditor(string propertyName, TModel? model)` -- provides editor instances
- `GetScreenActions()` -- adds action buttons/links

There is NO save hook. No `OnSave`, `OnBeforeSave`, or `OnAfterSave`. The save is entirely handled by the screen's own `CommandBase<TModel>`.

**Why it happens:**
The injector API surface (`builder.AddComponent`, `builder.AddComponents`) looks like it supports arbitrary tab/field injection. Without reading the save flow, it appears any editor added to the screen will be saved. In reality, `CommandBase<TModel>.GetModel()` only deserializes known `TModel` properties from the form submission.

**How to avoid:**
Do NOT use a tab on PageEditScreen for per-item-type field exclusion. Instead:
1. Create standalone screens accessible via tree nodes under Settings > Database > Serialize
2. Use the injector's `GetScreenActions()` to add a navigation action (link/button) on PageEditScreen that opens the standalone screen -- this pattern is already proven by the existing `SerializerPageEditInjector` which adds "Serialize subtree"
3. For the "Serialization" concept on Item Edit, use a dedicated screen at Serialize > Item Types > [TypeName] that edits serializer config.json directly

**Warning signs:**
- Editors appear on the injected tab but values are never saved
- No errors thrown -- form submits successfully, but custom fields are silently ignored
- Editors show default/empty values on every reload

**Phase to address:**
Phase 1 (architecture decision) -- must be resolved before any UI work begins. The "Serialization tab on Item Edit" concept needs redesign as standalone screens.

---

### Pitfall 2: Config Schema Migration Breaks Existing Installations

**What goes wrong:**
Current config.json has `excludeFields` and `excludeXmlElements` as flat string arrays on each predicate. Moving to per-item-type exclusions changes the JSON structure fundamentally (from `"excludeFields": ["field1", "field2"]` to something like `"itemTypeExclusions": { "Swift_Article": ["field1"], "Swift_Poster": ["field2"] }`). Any existing config file will fail to deserialize if the schema changes without a migration path.

The `ConfigLoader` uses `System.Text.Json` with `PropertyNameCaseInsensitive = true`. Unknown properties in JSON are silently ignored (default STJ behavior). Missing properties become null/default. This means:
- Adding new properties: safe (old configs just get defaults)
- Removing/renaming properties: breaks (old configs have orphaned keys, new code gets null)
- Changing property types: breaks (STJ throws on type mismatch)

**Why it happens:**
PROJECT.md notes this is a beta (0.x) product where "YAML format can change freely" (per memory `feedback_no_backcompat.md`). Developers may assume config.json gets the same treatment. However, config files contain manual user decisions that cannot be re-derived, unlike YAML output which is regenerated from the database.

**How to avoid:**
Use an **additive schema only** approach:
1. Keep existing `excludeFields`/`excludeXmlElements` on predicates as "global exclusions" (apply to all types)
2. Add NEW fields alongside: `itemTypeExclusions` (object map), `xmlTypeExclusions` (object map)
3. At runtime, merge both: flat excludes apply globally, structured excludes apply per-type
4. `ConfigLoader.BuildPredicate()` handles both formats: if only flat arrays exist, they become global; if structured sections exist, use those too
5. STJ silently ignores unknown keys, so old configs with only flat arrays load fine into the new schema

**Warning signs:**
- `JsonSerializer.Deserialize` throws on existing config files after upgrade
- Users report "Configuration is invalid" errors after updating the NuGet package
- Config that worked in v0.5.0 stops working in v0.6.0

**Phase to address:**
Phase 1 (config schema design) -- new schema must be designed before UI screens are built, because screens depend on the data structure.

---

### Pitfall 3: Auto-Discovery of XML Types via Service API Loads All Paragraphs

**What goes wrong:**
Auto-discovering embedded XML types requires scanning paragraphs for distinct `ModuleSystemName` values. DW's content API (`Services.Paragraphs`) returns full `Paragraph` objects including all fields, module settings XML, and item data. Loading ~22K paragraphs into memory to extract one string column is wasteful -- each Paragraph object includes the full ModuleSettings XML blob.

**Why it happens:**
DW has no lightweight "get distinct column values" API for paragraphs. The service layer returns fully hydrated objects. Developers follow the "MUST use DW APIs, never SqlTable" guidance (from memory `feedback_content_not_sql.md`) and use `Services.Paragraphs.GetParagraphsByPageId()` in a loop.

**How to avoid:**
For read-only admin discovery queries, use `Dynamicweb.Data.Database.CreateDataReader()` with direct SQL:
```sql
SELECT DISTINCT ParagraphModuleSystemName 
FROM Paragraph 
WHERE ParagraphModuleSystemName != '' AND ParagraphModuleSystemName IS NOT NULL
```

This is justified because:
- The "use DW APIs" rule is about write operations that bypass critical side effects (cache invalidation, notifications, etc.)
- DW itself uses `Database.CreateDataReader()` for admin read operations throughout its codebase
- The query returns ~15 rows instantly vs loading 22K full paragraph objects

Cache the result per-request or with a short TTL. For `UrlDataProvider` types on pages, use a similar `SELECT DISTINCT PageUrlDataProviderType FROM Page WHERE PageUrlDataProviderType != ''`.

**Warning signs:**
- Edit screen takes >2 seconds to load when auto-discovery runs
- Memory spike in IIS worker process
- Admin UI becomes sluggish after adding the auto-discovery feature

**Phase to address:**
Phase 2 (Embedded XML screen) -- implement and benchmark the discovery query before building the UI.

---

### Pitfall 4: Multi-Select Page Picker Value Binding (String vs Int)

**What goes wrong:**
`SelectorBuilder.CreatePageSelector(multiselect: true)` exists and works (verified in DW10 source at `SelectorBuilder.cs` line 65). However, multi-select changes the value format. Single-select binds to an `int` (page ID). Multi-select returns a comma-separated string of IDs. If the model property is `int`, the framework cannot bind multi-select values.

Additionally, from memory (`feedback_dw_patterns.md`): "DW Select dropdown values are strings. If the model property is an int, the framework can't match the selected value back on reload." This applies doubly to multi-select where the value is `"42,57,103"`.

**Why it happens:**
The current `PredicateEditModel.PageId` is an `int`. The existing `Excludes` is a textarea string (one path per line). Switching to a multi-select page picker requires understanding the value format change.

**How to avoid:**
1. Add a NEW `string` property `ExcludePageIds` to `PredicateEditModel` (do not reuse `int PageId`)
2. Store in config.json as an integer array: `"excludePageIds": [42, 57, 103]`
3. In the query: convert `List<int>` from config to comma-separated string for the model property
4. In the save command: parse comma-separated string back to `List<int>` for config persistence
5. Keep existing path-based `Excludes` working as fallback (some users may have path-based excludes)

**Warning signs:**
- Page selector appears but selected values lost on save/reload
- "The selected option no longer exists" error on screen reload
- Multi-select opens but only allows single selection (wrong editor type)

**Phase to address:**
Phase 3 (predicate page exclusion enhancement) -- after config schema is settled.

---

### Pitfall 5: Area Edit Screen Injection Has Same Save Limitation

**What goes wrong:**
Adding area column exclusions via `EditScreenInjector<AreaEditScreen, AreaDataModel>` hits the same wall as Pitfall 1. `AreaDataModel` is owned by `Dynamicweb.Content.UI`. The `AreaSaveCommand` only saves `AreaDataModel` properties. Serializer config properties cannot be persisted through this save flow.

Verified: `AreaEditScreenInjector` (Dynamicweb.Global.UI) successfully adds an "Ecommerce" tab with 7 fields -- but all fields (`EcomShopId`, `EcomLanguageId`, `EcomCurrencyId`, etc.) are properties already on `AreaDataModel`.

**Why it happens:**
Same root cause as Pitfall 1. Developers see the ecommerce tab working on AreaEditScreen and assume the same pattern works for serializer config with separate persistence.

**How to avoid:**
Same strategy as Pitfall 1:
- Use `GetScreenActions()` on an `AreaEditScreenInjector` to add a navigation link to a standalone serializer screen
- OR place area column exclusion config on the predicate edit screen itself (as a "Content Settings > Area Columns" section when ProviderType is "Content"), avoiding injection entirely
- The predicate-based approach is simpler because area config is already predicate-scoped (each content predicate targets a specific area)

**Warning signs:**
- Same as Pitfall 1: fields appear but never save
- User confusion about where area config "lives" (AreaEditScreen vs predicate screen)

**Phase to address:**
Phase 1 (architecture) -- decide whether area config goes on predicate screen or standalone screen before building.

---

### Pitfall 6: Read-Only Summary on Predicate Screen Shows Stale Data

**What goes wrong:**
The plan calls for a read-only "Filtering" section on predicate edit that aggregates per-item-type and per-XML-type exclusions (sourced from separate config sections). The `EditScreenBase` model pattern expects all displayed data from the screen's own `DataViewModelBase`. Showing computed data from a different config location requires the `PredicateEditModel` query to load and aggregate it.

The problem: if a user edits item-type exclusions in Screen A, then navigates to the predicate screen, the predicate screen may show stale data because:
- The aggregation happened at query time (when screen loaded)
- DW's ShadowEdit system tracks unsaved changes for the current screen only
- Cross-screen data freshness has no built-in mechanism

**Why it happens:**
DW edit screens are form-centric: one model, one save command. There is no built-in "subscribe to changes from another screen" mechanism.

**How to avoid:**
1. Add computed read-only properties to `PredicateEditModel` (populated fresh in `PredicateByIndexQuery`, never saved by `SavePredicateCommand`)
2. Use `CreateMapping(m => m.FilteringSummary) with { ReadOnly = true }` in `GetEditorMappings()`
3. Accept that the summary is a point-in-time snapshot -- add a "Refresh" action button that reloads the screen
4. For "link to edit" functionality, use action buttons (not editors) that navigate to the Item Type or Embedded XML screens

**Warning signs:**
- Read-only fields show stale/outdated data after edits in other screens
- Save command accidentally persists the aggregated summary back to config, corrupting the structure

**Phase to address:**
Phase 4 (predicate screen enhancement) -- after item-type and XML-type screens exist and have stable data to aggregate.

---

### Pitfall 7: Injector Class Discovery Requires Correct Visibility and Inheritance

**What goes wrong:**
`ScreenInjectorHandler` discovers injectors via `AddInManager.GetInstances<ScreenInjector<T>>()` (verified in `ScreenInjectorHandler.cs` line 17). This uses reflection on loaded assemblies. If a new injector class is `internal` instead of `public`, abstract instead of concrete, or extends the wrong base class, it silently fails to register.

**Why it happens:**
`AddInManager` scans for concrete, public types. There are no compile-time errors or runtime warnings when an injector is not discovered -- it just does not appear.

**How to avoid:**
- Always make injector classes `public sealed`
- Extend `EditScreenInjector<TScreen, TModel>` (not `ScreenInjector<TScreen>` directly) for edit screen injection
- Copy the exact class structure of the working `SerializerPageEditInjector`
- Add a debug log in the injector constructor to verify it was instantiated

**Warning signs:**
- Injected tabs/actions simply do not appear on the target screen
- No errors in any logs -- just silent absence
- Works in dev project but not in packaged NuGet deployment

**Phase to address:**
Every phase that adds a new injector -- verify in a clean DW instance.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Flat `excludeFields` on predicate (current) | Simple JSON, easy to understand | Cannot exclude field X only for item type Y | Acceptable until v0.6.0 |
| Free-text textarea for excludes/fields | No complex UI work | Typos, no validation, no auto-completion | Never after v0.6.0 |
| Loading all paragraphs for type discovery | Uses only DW service APIs | Memory waste, slow on large DBs | Never -- use direct SQL for read-only discovery |
| Storing page exclusions as paths instead of IDs | Human-readable config | Breaks when pages move or are renamed | Acceptable as legacy fallback only |
| Single standalone screen per config area | Consistent navigation, clear ownership | More tree nodes, more screens to maintain | Always -- this is the correct pattern for DW |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| DW AddInManager injector discovery | Making injector class `internal` or `abstract` | Must be `public sealed` class extending `EditScreenInjector<TScreen,TModel>` |
| DW Select editor value binding | Using `int` model property with Select (values are strings) | Use `string` property for Select-bound fields, parse in save command |
| DW SelectorBuilder.CreatePageSelector multiselect | Assuming multi-select returns `List<int>` | Returns comma-separated string; use string model property |
| DW WithReloadOnChange in dialogs | Using ReloadOnChange inside PromptScreenBase | Only works in EditScreenBase; use NavigateScreenAction for dialogs |
| DW config file path resolution | Using `Path.Combine(AppContext.BaseDirectory, ...)` | Use `SystemInformation.MapPath()` for DW virtual paths |
| DW SelectorBuilder.CreateAreaSelector in dialogs | Selector panel goes behind dialog overlay | Use plain `Select` with `ListOption` from `Services.Areas.GetAreas()` |
| EditScreenInjector save flow | Expecting injected fields to be included in save command | Injectors only add UI; save command only knows its own TModel properties |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Loading all paragraphs for distinct ModuleSystemName | 2-5 sec delay, memory spike | Direct SQL `SELECT DISTINCT` query | >10K paragraphs |
| Loading all pages for UrlDataProvider discovery | Similar to above | Direct SQL `SELECT DISTINCT PageUrlDataProviderType` | >500 pages |
| Querying item type metadata for every field on every type | N+1 queries, slow screen load | Cache `MetadataManager.Current.GetItemType()` results | >20 item types |
| Re-reading config.json on every screen load | Disk I/O per admin request | Cache parsed config with file watcher invalidation | High admin traffic |
| Building content tree for page path display in selectors | Tree traversal per page | Use DW's built-in page selector which handles this | >500 pages |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Splitting exclusion config across many screens without a summary view | User cannot see the full picture of what is excluded | Predicate screen shows aggregated read-only summary with links to source screens |
| Auto-discovery showing ALL module types including unused ones | Overwhelming list of XML types to configure | Only show types that exist in paragraphs within the predicate's area/path scope |
| No visual indicator of exclusion count per type | User must open each type to see what is excluded | Show count badges in tree/list (e.g., "Swift_Article (3 excluded)") |
| Config changes take effect only after re-serialization | User expects immediate file changes | Show clear "pending changes" status and prompt to re-serialize |
| Navigation action on Page Edit opens a screen outside the content tree | User loses context in the content tree | Open the serializer config in a slide-over or new browser tab, not as a tree navigation |

## "Looks Done But Isn't" Checklist

- [ ] **Tab injection on Item Edit:** Editors appear but values do not persist after save -- verify by saving, navigating away, returning to the screen
- [ ] **Multi-select page picker:** Selector opens in multi-select mode but verify selected values survive save-reload cycle (string vs int binding)
- [ ] **Config migration:** Test loading a v0.5.0 config.json in v0.6.0 code -- verify no errors, flat excludes still work
- [ ] **Auto-discovery performance:** Test on Swift 2.2 (22K paragraphs) -- verify screen loads in <500ms
- [ ] **Auto-discovery empty state:** Test on a clean DW instance with no paragraphs -- verify graceful empty state, no crashes
- [ ] **Read-only predicate summary:** Verify the summary updates when navigating back to predicate screen after editing item-type exclusions
- [ ] **Injector discovery in NuGet:** Build NuGet package, install in fresh DW instance, verify all injected actions appear
- [ ] **Additive config schema:** Verify that adding `itemTypeExclusions` to config does not break loading of predicates without it
- [ ] **Area column config persistence:** Verify area-level exclusions save to config.json and survive app restart

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Config schema break (Pitfall 2) | LOW | Add dual-path deserialization in ConfigLoader; existing configs work unchanged |
| Tab injection save failure (Pitfall 1) | MEDIUM | Rewrite as standalone screen; requires new tree nodes, queries, commands |
| Multi-select binding failure (Pitfall 4) | LOW | Change model property type from int to string; update save/load code |
| Auto-discovery performance (Pitfall 3) | LOW | Replace service API call with direct SQL; isolated change |
| Stale read-only summary (Pitfall 6) | LOW | Add refresh button; accept point-in-time nature |
| Injector not discovered (Pitfall 7) | LOW | Fix class visibility/inheritance; no data loss |
| Area injection save failure (Pitfall 5) | MEDIUM | Move config to predicate screen; requires UI rework |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| P1: Injector cannot save custom data | Phase 1 (architecture) | Standalone screen saves and loads config correctly |
| P2: Config schema migration | Phase 1 (schema design) | v0.5.0 config loads without errors in v0.6.0 code |
| P3: Auto-discovery performance | Phase 2 (XML type screen) | Discovery query returns in <100ms on 22K paragraph DB |
| P4: Multi-select picker binding | Phase 3 (predicate enhancement) | Select 3 pages, save, reload -- all 3 still selected |
| P5: Area injection save limits | Phase 1 (architecture) | Area config accessible and persistable via chosen approach |
| P6: Stale read-only summary | Phase 4 (predicate enhancement) | Summary refreshes when screen reloads |
| P7: Injector discovery | Every injector phase | Test in packaged NuGet on clean DW instance |

## Sources

- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\EditScreenBase.cs` -- line 65: `IEditScreenInjector` internal property; lines 104-113: injector invocation in `GetDefinitionInternal()` (injectors run AFTER `BuildEditScreen`, can add tabs/actions but NOT modify save behavior)
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\EditScreenInjector.cs` -- full interface: `OnBuildEditScreen`, `GetEditor`, `GetScreenActions`. No save hooks.
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\ScreenInjectorHandler.cs` -- line 17: discovery via `AddInManager.GetInstances<ScreenInjector<T>>()`
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\ScreenBuilders\EditScreenBuilder.cs` -- line 52: injectors filtered to `EditScreenInjector<TScreen, TModel>` type
- `C:\Projects\temp\dw10source\Dynamicweb.Global.UI\Content\PageEditScreenInjector.cs` -- reference implementation: ecommerce fields are on `PageDataModel`, NOT external config
- `C:\Projects\temp\dw10source\Dynamicweb.Global.UI\Content\AreaEditScreenInjector.cs` -- reference implementation: ecommerce fields on `AreaDataModel`
- `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\Screens\PageEditScreen.cs` -- 5 hardcoded tabs (General, Layout, SEO, Publication, Advanced)
- `C:\Projects\temp\dw10source\Dynamicweb.Content.UI\Screens\AreaEditScreen.cs` -- 4 hardcoded tabs (General, Domain and URL, Layout, Advanced)
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Editors\Selectors\SelectorBuilder.cs` -- line 65: `CreatePageSelector(multiselect: true)` confirmed
- `C:\VibeCode\DynamicWeb.Serializer\src\DynamicWeb.Serializer\AdminUI\Injectors\SerializerPageEditInjector.cs` -- existing working action-only injector
- `C:\VibeCode\DynamicWeb.Serializer\src\DynamicWeb.Serializer\Configuration\ConfigLoader.cs` -- current config schema with flat arrays
- Memory: `feedback_dw_patterns.md` -- Select string value type, WithReloadOnChange dialog limitation, SelectorBuilder dialog issue
- Memory: `feedback_content_not_sql.md` -- DW API vs SqlTable guidance (write-only restriction)
- Memory: `feedback_no_backcompat.md` -- beta 0.x, format can change, but config.json is user-managed

---
*Pitfalls research for: DynamicWeb.Serializer v0.6.0 -- UI Configuration Improvements*
*Researched: 2026-04-07*

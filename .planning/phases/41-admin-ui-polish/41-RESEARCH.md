# Phase 41: Admin-UI polish + cross-page consistency - Research

**Researched:** 2026-05-01
**Domain:** DynamicWeb 10 admin UI (Dynamicweb.CoreUI 10.23.9), .NET 8, AdminUI screens / queries / commands; live-host repro of five UX defects from Phase 40 manual verification
**Confidence:** HIGH for D-01, D-03, D-05, D-13 root cause; MEDIUM for D-08/D-09 editor choice; HIGH for D-12 tooltip pattern (the established knob is `Hint` / `Explanation`, not a `Tooltip` type — that type does not exist in CoreUI)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Item Types -> Item Type Excludes rename (D-01)**
- D-01: Rename scope is **tree + list screen + breadcrumb**. Plan touches:
  - `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` (line 75: `Name = "Item Types"`)
  - `ItemTypeListScreen` title / heading
  - `ItemTypeEditScreen` title / heading (when viewing a specific ItemType's exclusion set, "Item Type Excludes - {systemName}" or similar - exact copy is researcher's call)
  - Any breadcrumb-path provider that emits the segment label
  - The label `Item Types` is misleading because the page does NOT manage Item Types (those are owned by the DW10 ItemManager pipeline) - it manages **per-ItemType field exclusions** for serialization. The new label states that intent directly.
- D-02: Internal C# identifier `ItemTypesNodeId` (the const) does NOT need to be renamed - it's an internal id, not user-visible. Leave it as `Serializer_ItemTypes` to avoid churn in `SerializerSettingsNodeProviderModeTreeTests`.

**Empty Phase 40 baseline excludes - investigate first (D-03)**
- D-03: Run a content audit of the post-Phase-40 `swift2.2-combined.json` `excludeFieldsByItemType` and `excludeXmlElementsByType` against the **pre-Phase-40** baseline that was deleted in commit `c5d9a8c`. Surface that file via `git show c5d9a8c~:src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json` and diff its exclusion content against the new flat shape. If content was dropped during the 40-04 rewrite, restore it; if the new shape is intentionally empty, the planner documents why in `docs/baselines/Swift2.2-baseline.md` and confirms with the user before closing.
- D-04: The decision between "restore" and "document empty" lives in CONTEXT and the executor follows what the audit shows. NOT a checkpoint - autonomous resolution as long as the audit produces a clear answer.

**eCom_CartV2 list-vs-detail dual-list mismatch (D-05)**
- D-05: Trust the **list count** (21 excluded) and fix the **detail-page dual-list state load**. Researcher reproduces + identifies, executor fixes.
- D-06: Check whether the **same lookup pattern exists for ItemType field excludes** (`ItemTypeBySystemNameQuery` reads from `config.ExcludeFieldsByItemType`). If the same bug is present, fix both; if not, document why ItemType escaped.
- D-07: Does NOT need a checkpoint - fix-and-go once root cause is identified.

**Sample XML editor - fill the tab (D-08)**
- D-08: The `Label = "Sample XML from database"` editor at `XmlTypeEditScreen.cs:62` should fill the reference-tab content area by default rather than rely on a fixed `WithRows(N)`.
- D-09: If switching to a Code/Monaco-style editor lands cleanly *and* gives syntax highlighting + line numbers as a side benefit, that's preferred. If it fights the fill-parent layout or breaks the rest of the screen, fall back to a large fixed-rows `Textarea` (e.g., `WithRows(40)`) - pragmatic over pretty.
- D-10: This editor is **read-only** in intent (it's a sample for reference). If the chosen editor has a read-only mode, set it.

**Mode dropdown - drop suffix + tooltip (D-11)**
- D-11: At `PredicateEditScreen.cs:88-89`, change option labels:
  - `"Deploy (source-wins)"` -> `"Deploy"`
  - `"Seed (field-level merge)"` -> `"Seed"`
- D-12: Surface the explanatory text as a **tooltip on the select's label** (the field title rendered next to the dropdown). Tooltip text:
  `"Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields)."`
- D-13: Researcher confirms the actual screen-error root cause matches the hypothesis (parens in option labels confusing the Select component or breaking value/label normalization). If the root cause turns out to be different, the fix follows the actual cause - but the label cleanup + tooltip stay as the desired UX regardless.

### Claude's Discretion
- Exact copy text for renamed UI labels and the tooltip wording
- Choice between `Code` editor and large `Textarea` for the Sample XML field
- Whether to bundle the ItemType bySystemName lookup audit (D-06) as a single plan with the eCom_CartV2 fix or split into two plans
- Whether the rename + the dropdown fix go in the same plan or split

### Deferred Ideas (OUT OF SCOPE)
- "Help text below the select" alternative for the Mode dropdown - rejected in favor of the tooltip
- Switching to a full Code/Monaco-style editor for **all** XML/JSON-rendering admin fields - broader UX phase
- Auto-formatting the displayed sample XML on render
- The 9 environmental `DependencyResolverException` integration-test failures - separate test-infra phase
</user_constraints>

## Project Constraints (from CLAUDE.md)

CLAUDE.md lives at `C:\VibeCode\DynamicWeb.Serializer\CLAUDE.md` (PROJECT.md is the actual project doc; there is no separate CLAUDE.md root file - the project follows PROJECT.md conventions).

Directives carried forward into Phase 41:
- **Docstring nibble rule:** Trim verbose XML docstrings opportunistically when touching files; do not sweep-refactor.
- **Tech stack:** .NET 8.0, DynamicWeb 10.23.9 (`Dynamicweb` + `Dynamicweb.CoreUI` + `Dynamicweb.CoreUI.Rendering` + `Dynamicweb.Files.UI` + `Dynamicweb.Content.UI` packages).
- **Conflict resolution:** Source (files) always wins for Deploy mode; field-level merge for Seed (Phase 39).
- **No backward compatibility** (project memory `feedback_no_backcompat.md`): no migration shims, no deprecation paths, no version probes. Just change things.
- **DW patterns** (project memory `feedback_dw_patterns.md`):
  - Use `SystemInformation.MapPath()` for `/Files/...` virtual paths.
  - `WithReloadOnChange()` does NOT work inside dialogs (PromptScreenBase/OpenDialogAction); use `EditScreenBase` + `NavigateScreenAction`.
  - `SelectorBuilder.CreateAreaSelector()` opens a panel that goes behind dialogs; use plain `Select` for inline area selection.
  - **DW Select dropdown values are strings.** If the model property is an `int`, the framework can't match the selected value back on reload. (This rule extends to enums - see D-13 root cause below.)
  - ShadowEdit overlay pattern: on `WithReloadOnChange`, framework calls `GetModel()` first, then overlays ShadowEdit values; act on the overlaid value via a reload method called from `BuildEditScreen()`.

## Summary

Five admin-UI defects surfaced during manual verification of the Phase 40 flat-shape Deploy/Seed deploy. Four of them are mechanical fixes; one (D-05 dual-list mismatch) has a non-obvious root cause that's NOT in the dict-lookup site CONTEXT pointed at - it's in the Select editor's element-discovery short-circuit. The fix is a 5-line change once the cause is understood.

**Five fixes, ranked by complexity:**

1. **D-01 rename** (mechanical, 4 strings): `Name = "Item Types"` -> `"Item Type Excludes"` in `SerializerSettingsNodeProvider.cs:75`; `GetScreenName() => "Item Types"` -> `"Item Type Excludes"` in `ItemTypeListScreen.cs:13`; `GetScreenName() => $"Item Type: {Model.SystemName}"` -> `$"Item Type Excludes - {Model.SystemName}"` in `ItemTypeEditScreen.cs:131`; tree breadcrumb label flows from the `Name` property on the `NavigationNode` so no separate breadcrumb provider edit needed (the path providers reference `ItemTypesNodeId` which is the C# const, not the display name). Test impact: `SerializerSettingsNodeProviderModeTreeTests` does not assert the display string of the Item Types node, so no test edit needed.

2. **D-03 baseline audit** (one-off audit, possibly zero file edits): the diff shows `excludeFieldsByItemType` was an explicit empty `{}` in pre-Phase-40 baseline (`c5d9a8c~`), entirely **absent** in post-Phase-40 `swift2.2-combined.json`. ConfigWriter omits empty dicts via `WhenWritingNull` so the absence is intentional. `excludeXmlElementsByType` content was *expanded* during Phase 40 (commit `d57d474`), not lost - it has 21 elements for `eCom_CartV2` plus richer per-type lists for UserCreate/UserAuthentication/etc. Recommendation: **document empty `excludeFieldsByItemType` as intentional** in `docs/baselines/Swift2.2-baseline.md`. No content restore needed.

3. **D-11/D-12 dropdown labels + tooltip** (mechanical 2-line label change + add explanation): label change at `PredicateEditScreen.cs:88-89` is trivial. The tooltip-on-the-label requirement is delivered via `[ConfigurableProperty("Mode", explanation: "...")]` on `PredicateEditModel.Mode` (already wired). The Select editor's `Hint` property is also available if a separate hover-tip is wanted.

4. **D-05 dual-list mismatch** (root cause is NOT the `XmlTypeByNameQuery.cs:31` `TryGetValue` site): when `XmlTypeDiscovery.DiscoverElementsForType("eCom_CartV2")` returns 0 elements from the live DB scan, `XmlTypeEditScreen.CreateElementSelector` short-circuits with `return editor` (line 110-112) before consulting `Model.ExcludedElements`. The 21 saved exclusions never reach the dual-list. Fix: when discovery is empty but `Model.ExcludedElements` is non-empty, build Options from the saved exclusions so the user can see and unselect them.

5. **D-13 Mode dropdown screen error** (root cause is the **enum-vs-string binding** memory rule, NOT parens in labels): `PredicateEditModel.Mode` is typed `DeploymentMode` (an enum). The Select editor's `ListOption.Value = nameof(DeploymentMode.Deploy)` produces a **string**. DW's framework expects model property type and Select Value type to match (per project memory `feedback_dw_patterns.md`: "DW Select dropdown values are strings. If the model property is an `int`, the framework can't match the selected value back"). Same rule extends to enums. Existing precedent in the codebase shows parens-in-labels work fine (e.g., `ItemTypeEditScreen.cs:109` ships `Label = $"{f.Name} ({f.SystemName})"` to a string-bound Select with no error). The fix is to change `PredicateEditModel.Mode` to `string` (or expose a `string ModeValue` shim that round-trips via `Enum.Parse`); the parens cleanup remains correct UX regardless.

**Primary recommendation:** Two plans - one for D-01/D-03/D-11/D-12 (label + docs polish, low-risk parallel-safe), one for D-05/D-06/D-13 (state-load and binding fixes, both touching `PredicateEditScreen` / `XmlTypeEditScreen` / `PredicateEditModel` / `XmlTypeByNameQuery` / `ItemTypeBySystemNameQuery`). Wave them sequentially because both plans touch admin-UI files that overlap on `PredicateEditScreen.cs` and the rename touches files the second plan does not.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Tree node display labels | Admin UI - Tree provider | - | `SerializerSettingsNodeProvider.GetSubNodes` is the only place the tree builds the `NavigationNode.Name` string |
| List screen titles | Admin UI - Screen | - | `ListScreenBase.GetScreenName()` is the only override point for list page title |
| Edit screen titles | Admin UI - Screen | - | `EditScreenBase.GetScreenName()` is the only override point for detail page title |
| Breadcrumb segment | DW Framework | - | Breadcrumb is rendered by the framework from `NavigationNodePath` - it picks up `NavigationNode.Name` - so the rename in the node provider flows into the breadcrumb automatically |
| Editor field rendering | Admin UI - Screen.GetEditor + Model.[ConfigurableProperty] | DW CoreUI rendering | Screen returns `EditorBase` instance via `GetEditor(string)`; framework binds it via reflection on the model property |
| Dual-list element discovery | Admin UI - Infrastructure (`XmlTypeDiscovery`) | SQL via `DwSqlExecutor` | Live database scan; not a config-layer concern |
| Saved-exclusion rendering | Admin UI - Screen.CreateElementSelector | Model.ExcludedElements | Editor builds Options from discovered set, pre-selects from saved set; mismatch is a screen-side bug (D-05) |
| Mode editor binding | Admin UI - Screen.GetEditor + Model.Mode | DW CoreUI Select renderer | Framework matches `Select.ListOption.Value` (string) against model property value (currently `DeploymentMode` enum -> mismatch per D-13) |
| Baseline JSON content | Configuration files (data) | - | `swift2.2-combined.json` is data; ConfigWriter is responsible for emit, ConfigLoader for read |

## Phase Requirements

This phase is goal-driven, not REQ-ID driven. The five "requirements" are the five admin-UI issues. Map below mirrors the CONTEXT D-IDs.

| ID | Description | Research Support |
|----|-------------|------------------|
| D-01 | Rename "Item Types" to "Item Type Excludes" in tree node + list/edit screen titles | `SerializerSettingsNodeProvider.cs:75`, `ItemTypeListScreen.cs:13`, `ItemTypeEditScreen.cs:131` are the three exact strings. No other user-visible occurrences. Internal `ItemTypesNodeId` const stays per D-02. |
| D-03 | Audit `excludeFieldsByItemType` and `excludeXmlElementsByType` content vs pre-Phase-40 baseline | Pre-Phase-40 (commit `c5d9a8c~`) had `excludeFieldsByItemType: {}` (empty). Post-Phase-40 has it absent. ConfigWriter omits empty dicts. Net intent unchanged. `excludeXmlElementsByType` was expanded in commit `d57d474` (pre-Phase-40 had keys with empty arrays; current has 21 elements for eCom_CartV2 + content for 7 of 17 keys). No restoration needed. |
| D-05 | Fix eCom_CartV2 detail-page dual-list 0-elements display | Root cause is `XmlTypeEditScreen.CreateElementSelector` short-circuiting at lines 108-112 when `XmlTypeDiscovery.DiscoverElementsForType` returns 0 elements (no live paragraph rows or page rows have `ParagraphModuleSystemName='eCom_CartV2'` / `PageUrlDataProvider='eCom_CartV2'`). The 21 saved exclusions in `Model.ExcludedElements` never reach the dual-list. |
| D-06 | Check whether `ItemTypeBySystemNameQuery` has the same lookup pattern | `ItemTypeBySystemNameQuery` uses `FirstOrDefault(kvp => string.Equals(kvp.Key, SystemName, OrdinalIgnoreCase))` - case-insensitive linear scan, NOT TryGetValue. Looks safer at the dict-lookup site. **However, the screen-side `ItemTypeEditScreen.CreateFieldSelector` has the same short-circuit pattern** as XmlTypeEditScreen: if `ItemManager.Metadata.GetItemFields(itemType)` returns no fields, it short-circuits with explanation text and the saved exclusions never render. Same fix shape applies. |
| D-08 | Enlarge "Sample XML from database" editor to fill reference tab | `Textarea` in CoreUI has settable `Rows` and `Height` properties (confirmed via DLL strings dump). `CodeEditor` exists but is bound to a `FilePath` relative to `/Files`, not an in-memory string - not a fit for live DB-pulled XML samples without writing to a temp file. Recommended: large `Textarea` with `Rows = 30` (or higher); skip `CodeEditor`. |
| D-09 | Prefer Code editor if it drops in cleanly with read-only + syntax highlighting | `CodeEditor.FilePath` requirement makes drop-in unviable for ephemeral DB samples. Fall back to large `Textarea`. |
| D-10 | Editor is read-only | `Textarea` has `Readonly` property (already used at `XmlTypeEditScreen.cs:65`). |
| D-11 | Drop "(source-wins)" / "(field-level merge)" suffixes from Mode option labels | Trivial 2-line label edit at `PredicateEditScreen.cs:88-89`. |
| D-12 | Surface explanatory text as a tooltip on the field label | **No `Tooltip` type or property exists in CoreUI.** `Hint` and `Explanation` are the available properties on EditorBase. `[ConfigurableProperty("Mode", explanation: "...")]` is already wired on `PredicateEditModel.Mode` and renders as label-adjacent help. The decision is whether to use `Hint` (typically inline label hint), `Explanation` (typically below-input hint), or refine the existing attribute text. The CONTEXT calls for "tooltip on the select's label" - the closest CoreUI primitive is `Hint`, set on the Select instance via `new Select { ..., Hint = "..." }` or `[ConfigurableProperty("Mode", hint: "...")]`. |
| D-13 | Confirm parens-in-label is the actual screen-error root cause | **NO - parens are not the cause.** Existing screen `ItemTypeEditScreen.cs:109` ships parens-in-label (`$"{f.Name} ({f.SystemName})"`) and works. Real root cause: `PredicateEditModel.Mode` is `DeploymentMode` enum; Select `ListOption.Value` is `string`; DW framework's value-match-on-render fails the same way it does for `int`-typed model properties (per project memory `feedback_dw_patterns.md`). Fix: change `PredicateEditModel.Mode` to `string` and convert to enum on save / from enum on query. |

## Standard Stack

This phase ships pure C# admin-UI changes - no new packages.

### Core (already in csproj, no version changes)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Dynamicweb | 10.23.9 | DW core types (`Items.ItemManager`, `Services.Pages`, `Areas`) | DW CoreUI requires same major.minor [VERIFIED: src/DynamicWeb.Serializer.csproj line 47] |
| Dynamicweb.CoreUI | 10.23.9 | EditorBase, Select, Textarea, ListOption, ConfigurablePropertyAttribute, NavigationNode, EditScreenBase, ListScreenBase | The admin-UI surface every screen and command depends on [VERIFIED: csproj line 48-50] |
| Dynamicweb.CoreUI.Rendering | 10.23.9 | Renderer for the editor/screen abstractions | Already wired |
| Dynamicweb.Files.UI | 10.23.9 | File picker (used in unrelated screens) | No Phase 41 use |
| Dynamicweb.Content.UI | 10.23.9 | Content tree integration | No Phase 41 use (admin-UI nav is under SystemSection) |
| YamlDotNet | 13.7.1 | YAML serialize/deserialize | No Phase 41 use |
| Microsoft.Extensions.Configuration.Json | 8.0.1 | JSON config reading - actually NOT used by `ConfigLoader` (uses System.Text.Json directly) | Pinned by transitive deps |

**No new packages.** All five fixes are pure C# changes inside `src/DynamicWeb.Serializer/AdminUI/`.

**Version verification (from `dotnet nuget locals` cache):** Dynamicweb.CoreUI versions present in local cache go up to 10.24.6, but the project pins 10.23.9 to match the rest of the DW stack. No upgrade indicated by Phase 41 (and any version bump is out of scope per CONTEXT).

### Supporting (already used, no Phase 41 change)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xUnit | 2.x | Unit + admin-UI integration tests | Tests live in `tests/DynamicWeb.Serializer.Tests/` |
| `ConfigLoaderValidatorFixtureBase` | (project test helper) | Permissive identifier-validator override for tests that call `ConfigLoader.Load(path)` | Already used by AdminUI test classes when round-tripping through ConfigLoader/ConfigWriter |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `Textarea` for Sample XML | `CodeEditor` | `CodeEditor` requires a `FilePath` relative to `/Files`. Live DB-pulled XML samples do not live on disk. Writing the sample to a temp file inside `/Files` per render would be wasteful and would create disk litter. **Reject.** |
| `Textarea` for Sample XML | `RichTextEditorDefinition` | Lives in the Lists namespace and is intended for grid cell editors, not standalone screen editors. **Reject.** |
| `string ModeValue` shim on PredicateEditModel | Custom enum-binding `ValueProvider` | Per Plan 03 commit notes, `PredicateEditScreen` Mode editor uses `Value = nameof(DeploymentMode.Deploy)` "so the framework's enum binding round-trips through the Model.Mode setter without a custom ValueProvider" - but in practice this round-trip fails per the live-host repro. The `string` property pattern is what every other working Select in this codebase uses (`ConflictStrategy`, `LogLevel`). **Adopt.** |

**Installation:** None - no new packages.

## Architecture Patterns

### System Architecture Diagram

```
User clicks predicate row in admin tree
    |
    v
[NavigationNodePath] -> SerializerSettingsNodeProvider.GetSubNodes(path)
    |                            |
    |                            +-- emits NavigationNode { Id, Name, NodeAction = NavigateScreenAction.To<ScreenT>().With(query) }
    |
    v (framework opens screen)
[ScreenT inherits EditScreenBase<ModelT>] -> Query.GetModel()
    |
    +-- ConfigPathResolver.FindConfigFile() -> ConfigLoader.Load() -> SerializerConfiguration
    +-- For XmlType / ItemType lookup: dict.TryGetValue(...) or .FirstOrDefault(... OrdinalIgnoreCase)
    +-- Returns ModelT populated
    |
    v
[ScreenT.BuildEditScreen()] -> AddComponents("Section", LayoutWrapper(group, fields))
    |
    +-- For each field: EditorFor(m => m.X) -> framework calls Screen.GetEditor("X")
    |       +-- Returns specific EditorBase: Textarea, Select, SelectMultiDual, etc.
    |       +-- For SelectMultiDual: editor.Options come from live DB scan (XmlTypeDiscovery / ItemManager.Metadata),
    |           editor.Value pre-selected from Model.X (newline-split)
    |
    v
[CoreUI Renderer] -> renders HTML from EditorBase tree

User edits field, clicks Save
    |
    v
[Screen.GetSaveCommand()] -> CommandT.Handle()
    |
    +-- ConfigLoader.Load() -> SerializerConfiguration
    +-- Mutate the relevant top-level dict OR Predicates list (with `config with { ... }`)
    +-- ConfigWriter.Save(updated, configPath) -> tmpfile + atomic rename
    |
    v
Returns CommandResult.Ok -> framework rerenders screen with refreshed Model
```

### Recommended Project Structure (already in place; no change)

```
src/DynamicWeb.Serializer/AdminUI/
|-- Tree/
|   +-- SerializerSettingsNodeProvider.cs   <- D-01 rename anchor (line 75)
|   +-- ItemTypeNavigationNodePathProvider.cs  <- breadcrumb path; const-based, no rename
|   +-- ItemTypeEditNavigationNodePathProvider.cs
|-- Screens/
|   +-- PredicateEditScreen.cs              <- D-08 anchor for Mode select (line 83-91); Sample XML editor lives in...
|   +-- XmlTypeEditScreen.cs                <- D-08 / D-09 anchor (line 60-66 Sample XML); D-05 root cause (line 90-135 CreateElementSelector)
|   +-- ItemTypeListScreen.cs               <- D-01 anchor (line 13)
|   +-- ItemTypeEditScreen.cs               <- D-01 anchor (line 131); D-06 same-shape bug check
|   +-- XmlTypeListScreen.cs                <- no Phase 41 change
|   +-- PredicateListScreen.cs              <- no Phase 41 change
|-- Queries/
|   +-- XmlTypeByNameQuery.cs               <- D-05 lookup site (line 31) - confirmed NOT the bug per below
|   +-- ItemTypeBySystemNameQuery.cs        <- D-06 lookup site - case-insensitive scan, OK
|-- Models/
|   +-- PredicateEditModel.cs               <- D-13 fix anchor (line 17 Mode property type)
|-- Commands/
|   +-- SavePredicateCommand.cs             <- D-13 fix support (line 161, 183 enum assignment to predicate.Mode)
|-- Infrastructure/
    +-- XmlTypeDiscovery.cs                 <- D-05 root cause source (DiscoverElementsForType returns 0 -> short-circuit)
```

### Pattern 1: Editor short-circuit when live discovery is empty (the bug pattern)

**What:** Editor builders that source their `Options` from a live DB scan return an empty editor with an explanation when the scan finds nothing.

**When it bites:** Saved exclusions exist in config for a type whose live DB content has rotated (eCom_CartV2 module no longer used by any current paragraph) or whose ItemType no longer registers fields the saved list references.

**Current (buggy) code:**

```csharp
// XmlTypeEditScreen.cs lines 102-127
try
{
    var discovery = new XmlTypeDiscovery(new DwSqlExecutor());
    var allElements = discovery.DiscoverElementsForType(Model.TypeName);

    if (allElements.Count == 0)
    {
        editor.Explanation = "No XML data found in database for this type. Elements will appear after data is available.";
        return editor;  // <-- BUG: drops the 21 saved exclusions
    }

    editor.Options = allElements
        .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
        .Select(e => new ListOption { Value = e, Label = e })
        .ToList();
    // ... pre-select from Model.ExcludedElements
}
```

**Fix shape:**

```csharp
var allElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
try
{
    var discovery = new XmlTypeDiscovery(new DwSqlExecutor());
    foreach (var e in discovery.DiscoverElementsForType(Model.TypeName))
        allElements.Add(e);
}
catch (Exception ex)
{
    editor.Explanation = $"Could not discover elements: {ex.Message}";
}

// Always merge saved exclusions into the option set so the user sees what's currently excluded,
// even when the live DB has no data for this type. The dual-list always shows the "excluded" side
// reflecting Model.ExcludedElements.
var selected = (Model.ExcludedElements ?? string.Empty)
    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
    .Select(v => v.Trim())
    .Where(v => v.Length > 0)
    .ToArray();
foreach (var s in selected)
    allElements.Add(s);

if (allElements.Count == 0)
{
    editor.Explanation = "No XML data found in database for this type and no saved exclusions. Elements will appear after data is available.";
    return editor;
}

editor.Options = allElements
    .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
    .Select(e => new ListOption { Value = e, Label = e })
    .ToList();

if (selected.Length > 0)
    editor.Value = selected;
```

The same shape applies to `ItemTypeEditScreen.CreateFieldSelector` (D-06).

### Pattern 2: Select with enum-typed model property (the binding bug)

**What:** `Select` editor with `ListOption.Value = "Deploy"` (string) bound via reflection to a model property of type `DeploymentMode` (enum). DW framework matches strings against strings; the value-match-on-render fails.

**Why it happens:** Per project memory `feedback_dw_patterns.md`: "DW Select dropdown values are strings. If the model property is an `int`, the framework can't match the selected value back on reload (`The selected option no longer exists`). Use a `string` property for Select-bound fields and parse to int when needed." The same rule extends to enums - C# `nameof(DeploymentMode.Deploy)` produces `"Deploy"` but the model property type comparison still fails.

**Current (buggy) code:**

```csharp
// PredicateEditModel.cs line 17
[ConfigurableProperty("Mode", explanation: "Deploy = source-wins...")]
public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;
```

```csharp
// PredicateEditScreen.cs lines 83-91
nameof(PredicateEditModel.Mode) => new Select
{
    SortOrder = OrderBy.Default,
    Options = new List<ListOption>
    {
        new() { Value = nameof(DeploymentMode.Deploy), Label = "Deploy (source-wins)" },
        new() { Value = nameof(DeploymentMode.Seed), Label = "Seed (field-level merge)" }
    }
},
```

**Fix shape:**

Option A (preferred - smallest diff, matches `LogLevel` / `ConflictStrategy` precedent):

```csharp
// PredicateEditModel.cs
[ConfigurableProperty("Mode", hint: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields).")]
public string Mode { get; set; } = nameof(DeploymentMode.Deploy);
```

Then `PredicateByIndexQuery.GetModel` line 32 changes to `Mode = pred.Mode.ToString()` and `SavePredicateCommand` lines 161 and 183 change `Mode = Model.Mode` -> `Mode = Enum.Parse<DeploymentMode>(Model.Mode, ignoreCase: true)`.

Option B (keep enum-typed model, custom binding) - rejected as larger surface; would require a `ValueProvider` plumbed into the framework's reflection-bound Select renderer, and there is no precedent for this in the codebase.

### Pattern 3: Tree node display label (the rename)

**What:** `NavigationNodeProvider.GetSubNodes` returns `NavigationNode { Id, Name, ... }`. `Id` is the C# const (stable, never user-visible). `Name` is the user-visible string and flows into the breadcrumb automatically.

**Current code (SerializerSettingsNodeProvider.cs lines 73-81):**

```csharp
yield return new NavigationNode
{
    Id = ItemTypesNodeId,         // <-- "Serializer_ItemTypes" - stays per D-02
    Name = "Item Types",          // <-- D-01 rename anchor
    Icon = Icon.ListUl,
    Sort = 20,
    HasSubNodes = true,
    NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
        .With(new ItemTypeListQuery())
};
```

**Fix:** `Name = "Item Type Excludes"`. Plus rename in `ItemTypeListScreen.GetScreenName()` (line 13) and `ItemTypeEditScreen.GetScreenName()` (line 131) for consistency with the new tree label.

### Anti-Patterns to Avoid

- **Don't add a `string` Mode property AND keep the enum.** That would create a synchronization burden and a dual source of truth. Replace the enum-typed property entirely; the model property type is the source of truth for the editor binding.
- **Don't rename the `Serializer_ItemTypes` const** (D-02 is locked). It's used as a stable id in `ItemTypeNavigationNodePathProvider`, `ItemTypeEditNavigationNodePathProvider`, and `SerializerSettingsNodeProviderModeTreeTests`. Renaming it churns tests for no UX benefit.
- **Don't write the Sample XML to a temp file under `/Files`** to fit `CodeEditor`. The disk litter and lifetime management cost outweighs the syntax-highlighting benefit. Stick with `Textarea`.
- **Don't try to make the dual-list show "saved exclusions only" when discovery is empty as a separate code path.** Merge the saved set into the discovered set so the data flow stays uniform.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Tooltip on field label | A custom HTML tooltip via JS or markup | `[ConfigurableProperty(label, hint: "...")]` or `EditorBase.Hint` property | The CoreUI framework owns hover/tooltip rendering; injecting raw HTML invites style-collision and accessibility regressions |
| Dict case-insensitive lookup helper | A new `CaseInsensitiveDict<T>` class | `new Dictionary<string, T>(source, StringComparer.OrdinalIgnoreCase)` (already the established pattern in `SaveItemTypeCommand`, `SaveXmlTypeCommand`, `ScanXmlTypesCommand`) | Pattern is already established; adding a wrapper type is needless |
| Enum<->string binding for Mode | A custom `IValueProvider` and reflection plumbing | Change `PredicateEditModel.Mode` to `string` (precedent: `SerializerSettingsModel.LogLevel`, `ConflictStrategy`) | DW Select binds strings; that's the documented pattern in project memory and the working precedent in this codebase |
| XML pretty-print | A new XML formatter | Existing `XmlFormatter.PrettyPrint(xml)` (used at `XmlTypeDiscovery.GetSampleXml` line 120) | Already formats consistently |
| Tree breadcrumb label | A separate breadcrumb provider edit | The `NavigationNode.Name` property in the existing node provider | DW framework derives breadcrumb from node names automatically |

**Key insight:** Every fix in Phase 41 is touching surface that already has a working precedent elsewhere in the codebase. There is nothing to invent.

## Runtime State Inventory

This phase touches:
1. C# source files (admin UI screens / queries / commands / models)
2. One JSON baseline file (`swift2.2-combined.json`) - audit only, may not need any edit
3. One markdown doc (`docs/baselines/Swift2.2-baseline.md`) - audit decision documented here

Nothing renames a string that's stored as a key, an OS-registered identifier, an env var, or a build artifact.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None - `ItemTypesNodeId` const stays "Serializer_ItemTypes" (D-02). The dict keys in `excludeXmlElementsByType` and `excludeFieldsByItemType` are NOT renamed. | None |
| Live service config | None - no n8n / Datadog / Tailscale / Cloudflare / etc. surface in this project | None |
| OS-registered state | None - no Task Scheduler / pm2 / systemd / launchd entries | None |
| Secrets and env vars | None - no SOPS / .env / CI env vars referenced in the rename surface | None |
| Build artifacts | None - assembly name / package id stay; no NuGet rename | None |

Nothing in the inventory category was found - state explicit.

## Common Pitfalls

### Pitfall 1: Misdiagnosing D-05 as a key-normalization bug
**What goes wrong:** Following the CONTEXT D-05 hypothesis literally and "fixing" `XmlTypeByNameQuery.cs:31` `TryGetValue` to use case-insensitive lookup. The TryGetValue site works correctly - the test `WriteConfig(... ["TypeA"] = ...)` exact-key roundtrips through the loader without case drift, and the live JSON has exact-case-match keys (`eCom_CartV2`).
**Why it happens:** The CONTEXT D-05 hypothesis points at a plausible but wrong site. The actual root cause is in `XmlTypeEditScreen.CreateElementSelector` early-return when `XmlTypeDiscovery` finds 0 elements.
**How to avoid:** Reproduce the actual screen, attach a debugger or add logging. Confirm `Model.ExcludedElements` arrives populated (21 entries). The bug is downstream in editor construction, not upstream in the query.
**Warning signs:** "I changed the lookup to case-insensitive but the dual-list still shows 0" -> you're fixing the wrong site.

### Pitfall 2: Misdiagnosing D-13 as a label-parens bug
**What goes wrong:** Removing the parens from labels and shipping; the screen error persists because the actual cause is the enum/string type mismatch.
**Why it happens:** CONTEXT D-13 explicitly says "researcher confirms... if the root cause turns out to be different... the fix follows the actual cause." Existing precedent (`ItemTypeEditScreen.cs:109`: `Label = $"{f.Name} ({f.SystemName})"`) shows parens-in-labels work fine.
**How to avoid:** Capture the actual screen error message + stack on the live host before changing anything. Confirm the message contains a value-binding term (e.g., "The selected option no longer exists", "Could not bind enum value", or similar) - that's the project-memory `feedback_dw_patterns.md` signature.
**Warning signs:** Screen still errors after label change; new predicate creation works (Mode default = Deploy, no enum->string round-trip needed) but editing an existing predicate errors (load reads enum, framework can't match string Value option).

### Pitfall 3: Renaming the C# const along with the display label
**What goes wrong:** Renaming `ItemTypesNodeId = "Serializer_ItemTypes"` to `ItemTypeExcludesNodeId = "Serializer_ItemTypeExcludes"` for "consistency".
**Why it bites:** Three production files reference `ItemTypesNodeId` (the two NavigationNodePathProviders + the node provider itself), and `SerializerSettingsNodeProviderModeTreeTests.cs` asserts the exact const value across multiple tests. CONTEXT D-02 explicitly forbids this rename.
**How to avoid:** Re-read D-02 before touching the const. Tree node display name and tree node id are independent - only the display name is user-visible.

### Pitfall 4: Restoring the empty `excludeFieldsByItemType: {}` to the JSON baseline
**What goes wrong:** Adding `"excludeFieldsByItemType": {}` to `swift2.2-combined.json` because the pre-Phase-40 baseline had it.
**Why it's wrong:** ConfigWriter.Save explicitly omits empty dicts via `WhenWritingNull` mapping (ConfigWriter.cs lines 35-36). On the next save (e.g., after a user edits any predicate), the empty dict would disappear again - making the "restore" pointless and confusing future readers. The pre-Phase-40 explicit empty dict was an artifact of an older ConfigWriter that did emit empty dicts; the new behavior is intentional.
**How to avoid:** Document the intent in `Swift2.2-baseline.md` ("`excludeFieldsByItemType` is empty by design - per-ItemType exclusions can be added via the admin UI, but Swift 2.2 baseline ships without any") rather than restore.

### Pitfall 5: Breaking the SerializerSettingsNodeProviderModeTreeTests with the rename
**What goes wrong:** Tests currently assert the *Id* of the Item Types node, not the *Name*. Renaming the display name is invisible to the test suite. **But** if a future test author adds a `Name` assertion, it'd need to be updated to "Item Type Excludes". Worth a verification grep.
**Warning signs:** New test in the AdminUI suite asserting `Assert.Contains("Item Types", ...)` - that test is checking the rename target.
**How to avoid:** Search the test tree for the literal string "Item Types" before merging.

## Code Examples

### D-05 fix: merge saved exclusions into discovered options

```csharp
// XmlTypeEditScreen.cs - replace CreateElementSelector body
private SelectMultiDual CreateElementSelector()
{
    var editor = new SelectMultiDual
    {
        Label = "Exclude Elements",
        Explanation = "Select XML elements to exclude from serialization for this type.",
        SortOrder = OrderBy.Default
    };

    if (string.IsNullOrWhiteSpace(Model?.TypeName))
        return editor;

    var allElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    try
    {
        var discovery = new XmlTypeDiscovery(new DwSqlExecutor());
        foreach (var e in discovery.DiscoverElementsForType(Model.TypeName))
            allElements.Add(e);
    }
    catch (Exception ex)
    {
        editor.Explanation = $"Could not discover elements from live database: {ex.Message}";
    }

    // D-05: always include the currently-saved exclusions in Options so the user sees them
    // even when the live DB scan returns nothing for this type.
    var selected = (Model.ExcludedElements ?? string.Empty)
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(v => v.Trim())
        .Where(v => v.Length > 0)
        .ToArray();

    foreach (var s in selected)
        allElements.Add(s);

    if (allElements.Count == 0)
    {
        editor.Explanation = "No XML data found in database for this type and no saved exclusions yet.";
        return editor;
    }

    editor.Options = allElements
        .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
        .Select(e => new ListOption { Value = e, Label = e })
        .ToList();

    if (selected.Length > 0)
        editor.Value = selected;

    return editor;
}
```

Same shape applied to `ItemTypeEditScreen.CreateFieldSelector` (D-06).

### D-13 fix: change Mode model property to string

```csharp
// PredicateEditModel.cs line 11-17 - replace
/// <summary>
/// Phase 40 D-01 / Phase 41 D-13: the predicate's own deployment mode as a string for DW Select
/// binding. Persists into ProviderPredicateDefinition.Mode (enum) via Enum.Parse on save.
/// String-typed because DW Select dropdowns bind by string Value (project memory feedback_dw_patterns.md).
/// </summary>
[ConfigurableProperty("Mode", hint: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields).")]
public string Mode { get; set; } = nameof(DeploymentMode.Deploy);
```

```csharp
// PredicateEditScreen.cs lines 83-91 - replace
nameof(PredicateEditModel.Mode) => new Select
{
    SortOrder = OrderBy.Default,
    Options = new List<ListOption>
    {
        new() { Value = nameof(DeploymentMode.Deploy), Label = "Deploy" },
        new() { Value = nameof(DeploymentMode.Seed), Label = "Seed" }
    }
},
```

```csharp
// PredicateByIndexQuery.cs line 32 - replace
Mode = pred.Mode.ToString(),  // enum -> string for editor binding
```

```csharp
// SavePredicateCommand.cs lines 161 and 183 - replace
Mode = Enum.Parse<DeploymentMode>(Model.Mode, ignoreCase: true),
```

### D-08 fix: enlarge Sample XML editor

```csharp
// XmlTypeEditScreen.cs line 60-66 - replace
new Dynamicweb.CoreUI.Editors.Inputs.Textarea
{
    Label = "Sample XML from database",
    Explanation = "This is a sample of the raw XML found in the database for this type. The element or parameter names shown in the exclusion list above correspond to the structure you see here.",
    Value = sample,
    Readonly = true,
    Rows = 30  // D-08: large enough to fill the reference tab without scrolling for typical samples
}
```

If `Rows` proves visually insufficient (the reference tab has more vertical space), the next knob is `Height = "60vh"` or similar string CSS value. Both `Rows` and `Height` setters exist on `Textarea` per CoreUI 10.23.9 DLL inspection.

### D-01 rename

```csharp
// SerializerSettingsNodeProvider.cs line 75
Name = "Item Type Excludes",
```

```csharp
// ItemTypeListScreen.cs line 13
protected override string GetScreenName() => "Item Type Excludes";
```

```csharp
// ItemTypeEditScreen.cs line 131
protected override string GetScreenName() =>
    !string.IsNullOrWhiteSpace(Model?.SystemName) ? $"Item Type Excludes - {Model.SystemName}" : "Item Type Excludes";
```

### D-12 tooltip-on-label

The closest CoreUI primitive to "tooltip on the field label" is `[ConfigurableProperty(label, hint: "...")]`, where `hint` typically renders as a small help indicator next to the label (vs `explanation` which renders as below-input help text). Using `hint:` matches the CONTEXT D-12 intent.

```csharp
[ConfigurableProperty("Mode", hint: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields).")]
public string Mode { get; set; } = nameof(DeploymentMode.Deploy);
```

If the visual treatment of `hint:` is not what the user expects (e.g., it renders as inline placeholder text rather than a hover tooltip), the fallback is to set `Hint` directly on the Select editor instance. Phase 41 plan can swap between the two during the live-host smoke test without churn (both knobs already wired into the editor).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-mode tree subnodes (Phase 37-01.1) | Single flat tree under Serialize (Phase 40 D-06) | 2026-04-29 (Plan 40-03) | Tree label rename in this phase touches the new shape |
| Section-level config Deploy/Seed objects | Flat `predicates: [...]` with per-predicate `mode` field (Phase 40 D-01) | 2026-04-29 (Plan 40-01) | Mode is now a per-predicate field on the model surface |
| Mode-scoped `excludeFieldsByItemType` / `excludeXmlElementsByType` dicts | Top-level mode-agnostic dicts (Phase 40 D-04) | 2026-04-29 | The dict structure being audited in D-03 |

**Deprecated/outdated:**
- `Phase37Plan37-01.1` per-mode item-type / xml-type screens - removed in Plan 40-03; Phase 41 does not touch any per-mode UI surface.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `[ConfigurableProperty(... hint: ...)]` renders as a label-adjacent help tooltip in DW 10.23.9 admin UI | D-12 fix | If `hint:` renders as inline placeholder text or below-input copy, the visual outcome doesn't match D-12's "tooltip on the select's label". Live-host smoke test must verify; fallback is to set `Hint` directly on the Select editor instance. |
| A2 | The actual screen error from D-13 is the enum-vs-string binding failure, not the parens | D-13 fix | If the screen error is something else (a serialization round-trip bug, or `WithReloadOnChange` interaction, or a model-validation collision), the proposed fix won't address it. Mitigation: capture actual error message before locking the fix path. The fix is still reasonable defense-in-depth (matches established codebase pattern), but the underlying cause may need additional treatment. |
| A3 | `eCom_CartV2` does not appear as a `ParagraphModuleSystemName` or `PageUrlDataProvider` value in the live Swift 2.2 DB - hence `DiscoverElementsForType` returns 0 | D-05 root cause | If `DiscoverElementsForType` actually returns elements but those elements don't match the saved exclusion-list (case mismatch, name-attribute discovery vs element-name discovery branch divergence), the fix shape still works (it merges saved into discovered). But the bug description shifts. Mitigation: live-host repro should run the discovery once and report the count. |
| A4 | `Textarea.Rows = 30` produces a visually-acceptable editor size on the reference tab | D-08 fix | If Rows=30 still doesn't fill the tab, increase it or use `Height` CSS value. Either is a one-line change. Low risk. |
| A5 | The `excludeFieldsByItemType: {}` empty-dict in the pre-Phase-40 baseline carried no semantic information beyond "we have this section, populated nothing" | D-03 audit | If a downstream tool / docs reader specifically inspects whether the key is present-vs-absent, removing it is a breaking change for them. There's no such consumer in this codebase. Risk approaches zero. |

## Open Questions

1. **D-12 Hint vs Explanation rendering semantics in DW 10.23.9**
   - What we know: Both `Hint` and `Explanation` setters exist on `EditorBase` derivatives; project's existing screens use `Explanation` as the long below-input copy. `Hint` has been used exactly once in the codebase (`SelectorBuilder.CreatePageSelector(... hint: ...)`).
   - What's unclear: Whether `hint` renders as a hover-only tooltip vs an always-visible inline indicator. DW public docs are sparse.
   - Recommendation: ship with `[ConfigurableProperty("Mode", hint: "...")]`; if live-host smoke shows unwanted always-visible inline copy, swap to `Hint` on the Select instance and verify there.

2. **D-13 actual error message text from live host**
   - What we know: CONTEXT marks the parens hypothesis as the prevailing guess; project memory points at enum-vs-string binding.
   - What's unclear: The actual error string the user saw (would help confirm the binding-vs-parens question definitively).
   - Recommendation: include "capture screen error message + stack" as the first verification step in the D-13 plan; if the error is anything other than a value-match-on-render failure, escalate back via orchestrator return.

3. **D-08 visual fit with `Rows = 30`**
   - What we know: Rows is a settable property on `Textarea`; existing screens never set it (default ~3-5 rows).
   - What's unclear: Whether the reference tab on `XmlTypeEditScreen` has a fixed-height container that ignores Rows, and whether `Height` (CSS string) would behave differently.
   - Recommendation: ship with `Rows = 30`; the live-host smoke test catches any "still tiny" outcome; one-line follow-up if needed.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | Build + tests | Yes | 8.0.x (verified by working `dotnet build` in 40-VERIFICATION.md) | - |
| Dynamicweb 10.23.9 NuGet | Source compile | Yes | 10.23.9 (csproj-pinned) | - |
| Local Swift 2.2 DW host (port 54035) | D-05/D-13 live repro + live-host verify | Project memory states YES (`reference_dw_hosts.md`) - operator-controlled | Swift 2.2 | Skip live verify; rely on unit-test-shaped repro for D-05 (mock `XmlTypeDiscovery` to return empty + populated `Model.ExcludedElements`); D-13 still needs a live repro to capture the actual error |
| `XmlTypeDiscovery.DiscoverElementsForType` infra (live SQL) | D-05 root cause confirmation | Yes via local DW host | - | Mockable via `ISqlExecutor` |

**Missing dependencies with no fallback:** none.

**Missing dependencies with fallback:** none - this phase is plain C# admin-UI work; the only "external" dependency is the local DW host for repro/verify and that's already provisioned.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.x with .NET 8 (verified via `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj`) |
| Config file | none - xUnit zero-config; see `xunit.runner.json` if it exists |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --filter "FullyQualifiedName~AdminUI"` |
| Full suite command | `dotnet build DynamicWeb.Serializer.sln && dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |

### Phase Requirements -> Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-01 | Tree node display name reads "Item Type Excludes" | unit | `dotnet test --filter "FullyQualifiedName~SerializerSettingsNodeProviderModeTreeTests"` | Update existing class - add `Assert.Contains(nodes, n => n.Name == "Item Type Excludes")` to `GetSubNodes_UnderSerializeNode_Returns_Predicates_ItemTypes_XmlTypes_LogViewer` |
| D-01 | List screen title reads "Item Type Excludes" | unit | manual code review (no test infrastructure for `GetScreenName()` in this codebase today) | Manual-only - no precedent for testing screen-name strings |
| D-01 | Edit screen title pattern reads "Item Type Excludes - {SystemName}" | unit | manual code review | Manual-only - same |
| D-03 | `excludeFieldsByItemType` documentation reflects intentional emptiness | docs review | manual review of `Swift2.2-baseline.md` | Manual + grep |
| D-05 | Dual-list shows saved exclusions when discovery returns 0 | unit | New test in `XmlTypeEditScreen` test file (does not exist today - Wave 0 gap) | NO - Wave 0 gap |
| D-05 | Dual-list shows saved exclusions when discovery returns subset | unit | Same | NO - Wave 0 gap |
| D-05 | Dual-list shows union when both populated | unit | Same | NO - Wave 0 gap |
| D-06 | Same shape verified for `ItemTypeEditScreen.CreateFieldSelector` | unit | New test in `ItemTypeEditScreen` test file (does not exist today - Wave 0 gap) | NO - Wave 0 gap |
| D-08/D-09/D-10 | Sample XML editor sized to fill reference tab; read-only | manual UI repro | live-host smoke at `https://localhost:54035/Admin/` | Manual-only |
| D-11 | Mode select option labels read "Deploy" / "Seed" | unit | New test or extend `PredicateCommandTests` to assert label strings via reflection on the editor | Update `PredicateCommandTests` or add test alongside |
| D-12 | `[ConfigurableProperty]` on Mode contains the explanatory copy | unit | Reflection-based assertion that `typeof(PredicateEditModel).GetProperty("Mode").GetCustomAttribute<ConfigurablePropertyAttribute>().Hint` (or Explanation) contains "Deploy =" | New unit test |
| D-13 | `PredicateEditModel.Mode` is `string`, not `DeploymentMode` | unit | Reflection assertion: `Assert.Equal(typeof(string), typeof(PredicateEditModel).GetProperty("Mode").PropertyType)` | New unit test |
| D-13 | Round-trip: SavePredicateCommand stores as enum, PredicateByIndexQuery retrieves as string | unit | Extend `PredicateCommandTests` - save a model with `Mode = "Seed"`, reload via query, assert `Mode == "Seed"` | Extend existing - tests already exist |
| D-13 | Live-host: opening a saved Deploy predicate does not error | manual UI repro | live-host smoke | Manual-only |

### Sampling Rate
- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --filter "FullyQualifiedName~AdminUI"`
- **Per wave merge:** `dotnet build DynamicWeb.Serializer.sln && dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd-verify-work` + manual live-host smoke covering D-05, D-08, D-13 (the three issues with UI-rendering surface that unit tests cannot fully cover)

### Wave 0 Gaps
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeEditScreenTests.cs` - new file - covers D-05 dual-list state load (3 tests: discovery-empty + saved-non-empty, discovery-subset, discovery-union)
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeEditScreenTests.cs` - new file - covers D-06 same shape (3 tests parallel to above)
- [ ] One new test in `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` for D-13 string-Mode round-trip (extends existing class; class already exists)
- [ ] Reflection-based attribute test for D-12 explanatory copy (one fact, can live in `PredicateCommandTests.cs` or a new `PredicateEditModelTests.cs`)
- [ ] No framework install required - xUnit + DW NuGet stack already in place per Phase 40

**Note on UI-screen testability:** existing test infrastructure does not include direct rendering tests for `EditScreenBase.GetEditor()` returns. Phase 41 tests for D-05/D-06 must reflect on the model returned from the screen factory or use the live `BuildEditScreen` method through reflection. Precedent: `SerializerSettingsNodeProviderModeTreeTests` instantiates the provider and calls public methods directly. The same approach works for `XmlTypeEditScreen` if the test mocks `Model` and inspects the `EditorBase` returned from `GetEditor("ExcludedElements")` - though `XmlTypeDiscovery` would need to be injectable (currently constructed inline at line 105). The plan should consider whether to extract `XmlTypeDiscovery` as a Screen-level injectable field for testability, or test via a `ScanXmlTypesCommand` integration path with a `FakeSqlExecutor`.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | DW admin UI handles authn upstream of this code |
| V3 Session Management | no | DW handles session |
| V4 Access Control | no | DW handles authz; admin UI surface is gated by DW's existing permission model |
| V5 Input Validation | yes | The `Mode` string property accepts user input that flows into `Enum.Parse<DeploymentMode>(...)`; if a user submits "DROP TABLE" via the form, `Enum.Parse` throws `ArgumentException`. ConfigLoader already enforces the same validation on the JSON pathway (case-insensitive `Enum.TryParse`); SavePredicateCommand should match. Also relevant for the dual-list: user-supplied exclusion element names flow into `XmlTypeDiscovery` regex `^[A-Za-z0-9_., ]+$` (already in place at `XmlTypeDiscovery.cs:69`). |
| V6 Cryptography | no | None |

### Known Threat Patterns for admin-UI/.NET stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Mode injection (post-rename to string) | Tampering | `Enum.Parse<DeploymentMode>(Model.Mode, ignoreCase: true)` throws on invalid values; SavePredicateCommand's catch block returns `CommandResult.ResultType.Error` with the message - acceptable. **Add:** explicit validation step in SavePredicateCommand that returns `CommandResult.ResultType.Invalid` with a clear message if the string doesn't parse to a known DeploymentMode value, mirroring ConfigLoader's behavior (its message: "predicates[<i>] (name='<Name>') has invalid mode '<Value>' (expected 'Deploy' or 'Seed', case-insensitive)."). |
| XSS via tooltip text rendered as innerHTML | Information Disclosure | The Phase 41 tooltip text is hardcoded as a constant string; no user input flows in. CoreUI's renderer is responsible for HTML-encoding attribute values. Project does not hand-render any HTML. **No additional mitigation needed.** |
| Stored XSS via element names in dual-list | Information Disclosure | Saved exclusions are written through `SaveXmlTypeCommand` which trims whitespace and lengths but does not HTML-encode. The CoreUI renderer for `SelectMultiDual` handles encoding. No additional mitigation needed in Phase 41 surface. |

`security_enforcement` config key: not present in `.planning/config.json`, treat as enabled. ASVS table above covers the realistic threats; no new attack surface introduced.

## Sources

### Primary (HIGH confidence)
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` - read directly for D-03 / D-13 verification (case sensitivity in dict comparer, Enum.TryParse pathway)
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` - read directly for D-03 verification (`WhenWritingNull` empty-dict omission)
- `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` - current state of baseline; 21 elements verified for `eCom_CartV2`
- `git show c5d9a8c~:src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json` - pre-Phase-40 baseline; `excludeFieldsByItemType: {}` confirmed
- `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs` - D-05 root cause (lines 102-127)
- `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` - D-13 root cause site (lines 83-91)
- `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` - D-13 model property type (line 17)
- `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` - D-01 anchor (line 75)
- `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeListScreen.cs` - D-01 anchor (line 13)
- `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs` - D-01 anchor (line 131); D-06 same-shape bug confirmation (lines 82-128)
- `src/DynamicWeb.Serializer/AdminUI/Infrastructure/XmlTypeDiscovery.cs` - confirmed live-DB scan returns OrdinalIgnoreCase HashSet
- DLL strings dump from `Dynamicweb.CoreUI.dll` 10.23.9 - confirmed `Rows`, `Height`, `Hint`, `Explanation`, `Label`, `CodeEditor`, `Textarea`, `LabelWithStatus`, `MultiLineTextEditorDefinition` exist
- DW Project memory `feedback_dw_patterns.md` (37 days old, project-specific): "DW Select dropdown values are strings. If the model property is an int, the framework can't match the selected value back" - extends to enums per D-13 hypothesis
- DW Project memory `feedback_no_backcompat.md` (8 days old): no migration shims; just change things
- Phase 40 plan summaries (40-01-SUMMARY.md, 40-03-SUMMARY.md, 40-04-SUMMARY.md, 40-VERIFICATION.md) - confirmed Phase 40 contracts and what shifted in admin UI

### Secondary (MEDIUM confidence)
- `Dynamicweb.CoreUI` 10.23.9 XML doc file - sparse XML doc coverage; confirmed CodeEditor exists with FilePath requirement
- Existing precedents: `ItemTypeEditScreen.cs:109` Label-with-parens, `LogViewerScreen.cs:97` Textarea pattern, `SerializerSettingsEditScreen.cs:96-122` LogLevel/ConflictStrategy as string-typed model properties

### Tertiary (LOW confidence)
- WebSearch on "DynamicWeb 10 CoreUI Hint Tooltip" - returned generic CoreUI (CSS framework, unrelated) results; no Dynamicweb-specific docs for hint-vs-explanation rendering. A1 / Open Question 1 reflects this gap.

## Metadata

**Confidence breakdown:**
- D-01 rename surface (Standard Stack + Architecture): HIGH - exact 3-string surface confirmed by grep + read of the only files referencing "Item Types" string
- D-03 baseline audit: HIGH - direct git diff vs `c5d9a8c~` shows the answer
- D-05 root cause: HIGH - read the screen code; the early-return short-circuits saved exclusions, confirmed against the actual JSON content for `eCom_CartV2`
- D-06 same-shape check: HIGH - `ItemTypeEditScreen.CreateFieldSelector` has the same short-circuit pattern; lookup query (`ItemTypeBySystemNameQuery`) is fine, screen has the bug
- D-08 / D-09 editor choice: MEDIUM - `Textarea.Rows` confirmed via DLL strings; visual outcome only validates on live host. CodeEditor unfit due to FilePath requirement.
- D-11 label change: HIGH - 2 strings, trivial
- D-12 tooltip pattern: MEDIUM - `[ConfigurableProperty(... hint: ...)]` is the closest CoreUI primitive; visual semantics confirmed only by live-host smoke. No `Tooltip` type exists in CoreUI.
- D-13 root cause: HIGH - the project-memory rule on Select+enum/int binding is well-established and the type mismatch is observable in the source. The fix matches existing working precedents (`LogLevel`, `ConflictStrategy`).
- Pitfalls: HIGH - all five pitfalls map to real source-code anchors

**Research date:** 2026-05-01
**Valid until:** 2026-05-31 (admin-UI surface is stable; CoreUI 10.23.9 is the version pinned by the project; no upstream churn anticipated this month)

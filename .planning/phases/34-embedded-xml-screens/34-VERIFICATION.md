---
phase: 34-embedded-xml-screens
verified: 2026-04-14T10:30:00Z
status: human_needed
score: 11/11 must-haves verified
overrides_applied: 1
overrides:
  - must_have: "Clicking an XML type opens an edit screen showing all elements from that XML type as a CheckboxList for exclusion selection"
    reason: "Implementation uses SelectMultiDual instead of CheckboxList. SelectMultiDual is a strict UX improvement (dual-pane multi-select) over CheckboxList and satisfies the same functional requirement — element selection for exclusion. This was an intentional design decision documented in 34-RESEARCH.md and both plan files. The roadmap SC used 'CheckboxList' as a placeholder UI term, not a binding control contract."
    accepted_by: "gsd-verifier"
    accepted_at: "2026-04-14T10:30:00Z"
re_verification: false
human_verification:
  - test: "Embedded XML tree node renders in DW Admin under Settings > Database > Serialize"
    expected: "A node labeled 'Embedded XML' appears between Predicates (Sort=10) and Log Viewer (Sort=20) with the brackets-curly icon"
    why_human: "Tree node rendering requires a live DW10 Admin instance; cannot verify DOM presence programmatically"
  - test: "Scan toolbar action triggers discovery and reloads the list"
    expected: "Clicking 'Scan for XML types' queries the database, adds any new type rows to the list screen, and refreshes without navigation"
    why_human: "RunCommandAction.WithReloadOnSuccess() behavior requires a live browser session to confirm the reload fires correctly"
  - test: "Per-type dynamic tree children appear after scan"
    expected: "After a scan populates types in config, the Embedded XML node expands to show one child node per type name"
    why_human: "Dynamic sub-node generation from config.ExcludeXmlElementsByType keys requires a live DW Admin session"
  - test: "XML type edit screen populates SelectMultiDual with live element names"
    expected: "Clicking a type opens an edit screen with a dual-pane selector pre-populated with element names discovered from the DB"
    why_human: "XmlTypeDiscovery.DiscoverElementsForType() queries real DB XML blobs; cannot exercise against live data programmatically"
  - test: "Saving element exclusions persists to config JSON and applies during next serialization run"
    expected: "After selecting elements and saving, the config file contains updated excludeXmlElementsByType entries, and a subsequent serialize run omits those elements from XML blobs"
    why_human: "End-to-end round-trip requires live DW Admin save + file inspection + serialize run"
---

# Phase 34: Embedded XML Screens Verification Report

**Phase Goal:** Users can discover XML types present in their data and configure element-level exclusions per type through a dedicated tree node
**Verified:** 2026-04-14T10:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | "Embedded XML" tree node appears under Serialize with dynamic children from config | VERIFIED | `SerializerSettingsNodeProvider.cs` lines 54-63 yield `EmbeddedXmlNodeId` node; lines 76-96 yield per-type children from `config.ExcludeXmlElementsByType.Keys` |
| 2 | User can trigger Scan to discover XML types from DB and persist to config | VERIFIED | `XmlTypeListScreen.GetToolbarActions()` yields `RunCommandAction.For<ScanXmlTypesCommand>().WithReloadOnSuccess()`; `ScanXmlTypesCommand.Handle()` calls `discovery.DiscoverXmlTypes()` then `ConfigWriter.Save()` |
| 3 | Clicking an XML type opens an edit screen with SelectMultiDual for element exclusion | VERIFIED (override) | `XmlTypeListScreen.GetListItemPrimaryAction()` → `NavigateScreenAction.To<XmlTypeEditScreen>()`; `XmlTypeEditScreen.CreateElementSelector()` creates `SelectMultiDual` with options from `DiscoverElementsForType()`; roadmap SC said "CheckboxList" but SelectMultiDual is the correct DW10 control for dual-pane multi-select — see override |
| 4 | Saving element exclusions persists to config under excludeXmlElementsByType | VERIFIED | `SaveXmlTypeCommand.Handle()` loads config, updates `ExcludeXmlElementsByType` dictionary entry, calls `ConfigWriter.Save()`; `XmlTypeEditScreen.GetSaveCommand()` returns `new SaveXmlTypeCommand()` |
| 5 | Scan merges new types without overwriting existing exclusion selections | VERIFIED | `ScanXmlTypesCommand` line 26-29: `if (!updated.ContainsKey(typeName)) updated[typeName] = new List<string>()`; existing keys are never overwritten; 3 unit tests cover merge/preserve/no-change scenarios |
| 6 | XmlTypeByNameQuery.TypeName is populated via SetKey when ModelIdentifier is set on the query | VERIFIED | `XmlTypeByNameQuery.SetKey(string key)` at line 13-15 assigns `TypeName = key`; `XmlTypeByNameQuery { ModelIdentifier = typeName }` call sites in both NodeProvider and ListScreen |
| 7 | XmlTypeDiscovery.DiscoverXmlTypes() returns distinct type names from Page and Paragraph tables | VERIFIED | Queries `SELECT DISTINCT PageUrlDataProviderType FROM Page` and `SELECT DISTINCT ParagraphModuleSystemName FROM Paragraph`; case-insensitive `HashSet<string>` deduplicates across both; 4 unit tests in `XmlTypeDiscoveryTests.cs` cover discovery, dedup, empty-filter |
| 8 | XmlTypeDiscovery.DiscoverElementsForType(typeName) returns distinct root-level XML element names | VERIFIED | `ParseXmlElements()` calls `doc.Root.Elements()` and adds `.Name.LocalName` to case-insensitive HashSet; 4 tests cover element extraction, dedup, malformed-skip, empty-result |
| 9 | Malformed XML blobs are skipped without crashing | VERIFIED | `ParseXmlElements()` wraps `XDocument.Parse()` in `catch (XmlException)` with silent skip; test `DiscoverElementsForType_SkipsMalformedXml` verifies only valid blob's elements appear |
| 10 | PredicateEditScreen SqlTable fields use SelectMultiDual instead of CheckboxList | VERIFIED | `PredicateEditScreen.cs` contains `private SelectMultiDual CreateColumnSelectMultiDual(`; zero `CheckboxList` references in entire `src/` tree |
| 11 | Element exclusions applied during serialization | VERIFIED | `ContentSerializer.cs` line 135 and 156 pass `_configuration.ExcludeXmlElementsByType` to `ContentMapper`; pipeline established in Phase 32 |

**Score:** 11/11 truths verified (1 with override for SelectMultiDual vs CheckboxList wording)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/.../AdminUI/Infrastructure/XmlTypeDiscovery.cs` | SQL + XML parsing for type and element discovery | VERIFIED | 117 lines; `DiscoverXmlTypes()` + `DiscoverElementsForType()` + `ParseXmlElements()`; `ISqlExecutor` injected via constructor |
| `src/.../AdminUI/Models/XmlTypeListModel.cs` | List row model for XML type list screen | VERIFIED | `class XmlTypeListModel : DataViewModelBase`; `TypeName` + `ExcludedElementCount` |
| `src/.../AdminUI/Models/XmlTypeEditModel.cs` | Edit model for XML type exclusion editing | VERIFIED | `class XmlTypeEditModel : DataViewModelBase, IIdentifiable`; `TypeName` + `ExcludedElements` (newline-separated); `GetId() => TypeName` |
| `src/.../AdminUI/Tree/SerializerSettingsNodeProvider.cs` | Embedded XML tree node with dynamic children | VERIFIED | `EmbeddedXmlNodeId` constant; Sort=15 node with `HasSubNodes=true`; dynamic children block reads `ExcludeXmlElementsByType.Keys` |
| `src/.../AdminUI/Screens/XmlTypeListScreen.cs` | List screen for discovered XML types | VERIFIED | `GetToolbarActions()` with `RunCommandAction.For<ScanXmlTypesCommand>()`; `GetListItemPrimaryAction()` navigates to edit screen |
| `src/.../AdminUI/Screens/XmlTypeEditScreen.cs` | Edit screen with SelectMultiDual for element exclusions | VERIFIED | `CreateElementSelector()` uses `XmlTypeDiscovery.DiscoverElementsForType()`; `SelectMultiDual` with `.ToArray()` value binding |
| `src/.../AdminUI/Commands/ScanXmlTypesCommand.cs` | Discovery scan command | VERIFIED | Merge-not-overwrite pattern; `ConfigPath` + `Discovery` injection points for tests |
| `src/.../AdminUI/Commands/SaveXmlTypeCommand.cs` | Element exclusion save command | VERIFIED | Validates Model + TypeName; parses newline-separated exclusions; `ConfigWriter.Save()` |
| `src/.../AdminUI/Screens/PredicateEditScreen.cs` | SelectMultiDual replaces CheckboxList (D-02) | VERIFIED | `CreateColumnSelectMultiDual()` present; zero `CheckboxList` or `CreateColumnCheckboxList` references |
| `tests/.../AdminUI/XmlTypeDiscoveryTests.cs` | Unit tests for discovery logic | VERIFIED | 9 `[Fact]` methods covering all specified behaviors with `FakeSqlExecutor` |
| `tests/.../AdminUI/XmlTypeCommandTests.cs` | Tests for scan and save commands | VERIFIED | 8 `[Fact]` methods (3 Scan + 5 Save) covering merge, validation, persistence, and round-trip |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `SerializerSettingsNodeProvider` | `XmlTypeListScreen` | `NavigateScreenAction.To<XmlTypeListScreen>()` | WIRED | Line 61: `.With(new XmlTypeListQuery())` |
| `XmlTypeListScreen` | `XmlTypeEditScreen` | `GetListItemPrimaryAction` | WIRED | Line 30-31: `NavigateScreenAction.To<XmlTypeEditScreen>().With(new XmlTypeByNameQuery { ModelIdentifier = model.TypeName })` |
| `XmlTypeListScreen` | `ScanXmlTypesCommand` | `GetToolbarActions` with `RunCommandAction.For<ScanXmlTypesCommand>()` | WIRED | Line 39: `RunCommandAction.For<ScanXmlTypesCommand>().WithReloadOnSuccess()` |
| `XmlTypeEditScreen` | `SaveXmlTypeCommand` | `GetSaveCommand` | WIRED | Line 110: `new SaveXmlTypeCommand()` |
| `ScanXmlTypesCommand` | `XmlTypeDiscovery` | discovery service call | WIRED | Line 21: `Discovery ?? new XmlTypeDiscovery(new DwSqlExecutor())` |
| `SaveXmlTypeCommand` | `ConfigWriter.Save` | config persistence | WIRED | Line 37: `ConfigWriter.Save(newConfig, configPath)` |
| `DataQueryIdentifiableModelBase.OnGetData` | `XmlTypeByNameQuery.SetKey` | `ModelIdentifier -> TryParseIdentifier -> SetKey populates TypeName` | WIRED | `SetKey(string key) { TypeName = key; }` confirmed in query |
| `XmlTypeDiscovery` | `ISqlExecutor` | constructor injection | WIRED | `private readonly ISqlExecutor _sqlExecutor;` constructor takes `ISqlExecutor sqlExecutor` |
| `PredicateEditScreen` | `SelectMultiDual` | editor factory method | WIRED | `CreateColumnSelectMultiDual()` instantiates `new SelectMultiDual` at line 114 |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|--------------|--------|-------------------|--------|
| `XmlTypeListScreen` | `XmlTypeListModel` list | `XmlTypeListQuery.GetModel()` reads `config.ExcludeXmlElementsByType` | Yes — maps config dictionary to model list | FLOWING |
| `XmlTypeEditScreen` | `XmlTypeEditModel.ExcludedElements` | `XmlTypeByNameQuery.GetModel()` reads config + joins exclusions | Yes — `string.Join("\n", excludedElements)` from real config | FLOWING |
| `XmlTypeEditScreen` SelectMultiDual options | `allElements` from `DiscoverElementsForType()` | SQL queries `TOP 50` XML blobs from Page/Paragraph | Yes — real DB queries; gracefully empty if no data | FLOWING |
| `ScanXmlTypesCommand` | `discoveredTypes` | `XmlTypeDiscovery.DiscoverXmlTypes()` | Yes — `SELECT DISTINCT` from Page + Paragraph | FLOWING |
| `SaveXmlTypeCommand` | `excludedElements` | `Model.ExcludedElements` split by newlines | Yes — from form submission to config | FLOWING |
| `ContentSerializer` | `ExcludeXmlElementsByType` | `_configuration.ExcludeXmlElementsByType` passed to `ContentMapper` | Yes — config loaded from JSON file | FLOWING |

### Behavioral Spot-Checks

Step 7b: SKIPPED — No runnable entry points available without a live DW Admin instance. All logic verified statically.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| XMLUI-01 | 34-02-PLAN.md | "Embedded XML" tree node under Serialize lists auto-discovered XML types | SATISFIED | `SerializerSettingsNodeProvider` yields `EmbeddedXmlNodeId` node navigating to `XmlTypeListScreen`; `XmlTypeListQuery` reads `ExcludeXmlElementsByType` keys from config |
| XMLUI-02 | 34-01-PLAN.md | "Scan" action discovers XML types via SQL | SATISFIED | `XmlTypeListScreen.GetToolbarActions()` → `RunCommandAction.For<ScanXmlTypesCommand>()`; `ScanXmlTypesCommand` runs `SELECT DISTINCT` queries on Page + Paragraph tables |
| XMLUI-03 | 34-01-PLAN.md | XML type edit screen shows all elements as selection control for exclusion | SATISFIED | `XmlTypeEditScreen.CreateElementSelector()` discovers elements via `DiscoverElementsForType()` and presents them in `SelectMultiDual`; roadmap used "CheckboxList" as placeholder term, override accepted |
| XMLUI-04 | 34-02-PLAN.md | XML element exclusions persisted to config under `excludeXmlElementsByType` and applied during serialize | SATISFIED | `SaveXmlTypeCommand` writes to `ExcludeXmlElementsByType`; `ContentSerializer` passes config dict to `ContentMapper` pipeline established in Phase 32 |

All 4 phase requirement IDs accounted for. No orphaned requirements for Phase 34 in REQUIREMENTS.md.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None detected | — | — | — | — |

Zero TODO/FIXME/PLACEHOLDER comments. Zero stubs with empty returns. Zero hardcoded empty data passed to rendering. No `CheckboxList` references anywhere in `src/`. `return null` at line 24 in `XmlTypeByNameQuery` is a legitimate not-found guard, not a stub.

### Human Verification Required

#### 1. Embedded XML Tree Node Renders in DW Admin

**Test:** Log into DW Admin, navigate to Settings > System > Database. Expand the Serialize node and observe the sub-nodes.
**Expected:** A node labeled "Embedded XML" appears between Predicates and Log Viewer, using a brackets/curly icon.
**Why human:** Tree node rendering requires a live DW10 Admin instance. The provider is verified to produce the node; its actual visibility in the navigation UI requires browser confirmation.

#### 2. Scan Action Discovers Types and Reloads List

**Test:** With a configured DW instance (has Page/Paragraph rows with URL data providers or modules), click the "Scan for XML types" toolbar button on the Embedded XML list screen.
**Expected:** New rows appear in the list representing discovered type names; existing type rows are preserved; the screen reloads in-place without full navigation.
**Why human:** `RunCommandAction.WithReloadOnSuccess()` behavior requires a live browser session to confirm the reload fires correctly and the list updates.

#### 3. Per-Type Dynamic Tree Children Appear After Scan

**Test:** After scan populates at least one type in config, expand the Embedded XML tree node in the sidebar.
**Expected:** One child node per discovered type appears, each named with the type's system name.
**Why human:** The dynamic sub-node generation reads from the live config file via `ConfigLoader`; the DW navigation cache and tree-refresh cycle requires a live session.

#### 4. Edit Screen Populates Element Selector from Live DB Data

**Test:** Click a type that has paragraphs or pages with XML data in the DB. Observe the edit screen's Exclude Elements field.
**Expected:** The SelectMultiDual shows element names discovered from the live DB XML blobs (e.g., "sort", "pagesize", "filtervalue" for a typical module).
**Why human:** `XmlTypeDiscovery.DiscoverElementsForType()` queries real Page/Paragraph XML blobs; cannot exercise against live data without a connected DW instance.

#### 5. End-to-End Round-Trip: Save Exclusions Then Serialize

**Test:** Configure element exclusions for a type, save, then run a full serialization. Open the resulting YAML file for a page using that module type.
**Expected:** The excluded XML elements are absent from the embedded XML blob in the YAML output.
**Why human:** Requires live DW Admin save + filesystem inspection + serialization execution — a multi-step flow that cannot be verified statically.

### Gaps Summary

No programmatically verifiable gaps found. All 11 must-haves are satisfied by substantive, wired, data-flowing implementations. The single roadmap SC discrepancy (SelectMultiDual vs CheckboxList wording) is an intentional design decision accepted via override — SelectMultiDual delivers a strictly better UX while fulfilling the same requirement.

Five human verification items remain for UI behavior that requires a live DW10 Admin instance.

---

_Verified: 2026-04-14T10:30:00Z_
_Verifier: Claude (gsd-verifier)_

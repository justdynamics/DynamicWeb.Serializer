---
phase: 41-admin-ui-polish
verified: 2026-05-01T18:00:00Z
status: passed
score: 14/14
overrides_applied: 0
gap_closure:
  - id: D-05/D-06-framework-binding
    discovered: 2026-05-01T17:50:00Z (live-host UAT)
    closed: 2026-05-01T20:05:00Z
    fix_commits: [af78c2f]
    summary: "Promoted XmlTypeEditModel.ExcludedElements and ItemTypeEditModel.ExcludedFields from string to List<string> so EditScreenBase.BuildEditor's post-GetEditor SetValue(rawValue) hands a list to SelectMultiDual.Value. Original RED tests bypassed BuildEditor via reflection and missed the framework-binding path; new FrameworkBinding_SavedExclusions_RenderAsSelected tests on both edit-screen suites simulate the GetEditor + editor.SetValue pipeline so the regression cannot recur."
  - id: D-14-embedded-xml-rename
    discovered: 2026-05-01T17:50:00Z (live-host UAT)
    closed: 2026-05-01T20:05:00Z
    fix_commits: [da55813]
    summary: "Renamed tree node 'Embedded XML' to 'Embedded XML Excludes' (SerializerSettingsNodeProvider) and XmlTypeListScreen.GetScreenName 'Embedded XML Types' to 'Embedded XML Excludes'. Same parent rationale as D-01: page manages exclusions, not the XML types themselves."
human_verification:
  - test: "Open https://localhost:54035/Admin/ → Settings → System → Developer → Serialize. Confirm tree node reads 'Item Type Excludes' (not 'Item Types'). Click in; confirm list screen title reads 'Item Type Excludes'. Click into any item type; confirm edit-screen title reads 'Item Type Excludes - {SystemName}'."
    expected: "All three user-visible strings display 'Item Type Excludes' in the correct positions."
    why_human: "String rendering in DW admin tree/breadcrumb/screen title is framework-driven at render time; grep confirms source but only a live host confirms the DW navigation framework actually picks up the renamed NavigationNode.Name."
  - test: "Open Embedded XML → eCom_CartV2. Inspect the 'Excluded Elements' dual-list."
    expected: "The right-hand 'excluded' panel shows the 21 saved elements (not empty). The left-hand 'available' panel may be empty if live discovery returns 0 from the DB — that is acceptable. The merged saved-exclusion set must appear on the excluded side."
    why_human: "D-05 fix is unit-tested via FakeSqlExecutor injection but live-host behavior depends on the real DwSqlExecutor path and the actual eCom_CartV2 live-DB state (0 rows in discovery triggers the exact merge branch being tested)."
  - test: "Open Predicates → any saved Deploy predicate. Confirm the screen renders without error and the Mode dropdown shows 'Deploy' selected."
    expected: "No 'selected option no longer exists' error or blank-screen error. Mode dropdown is visible with 'Deploy' or 'Seed' selected (no parens suffix on labels)."
    why_human: "D-13 Mode string-binding fix is unit-tested but the live-host screen render involves the DW CoreUI Select binding pipeline which cannot be exercised without a running host. This was the original bug trigger (screen error on Deploy predicate open)."
  - test: "On the Mode field label, hover (or inspect tooltip/explanation). Confirm the explanatory copy is visible: 'Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields).'."
    expected: "The hint text appears adjacent to or on hover over the Mode label, not as inline option labels."
    why_human: "D-12: ConfigurablePropertyAttribute hint: parameter is wired and unit-tested (attr.Hint contains the expected substrings), but the CoreUI rendering engine's actual tooltip/hint rendering requires a live host to confirm the copy surfaces in a discoverable way."
  - test: "Open Embedded XML → eCom_CartV2 → reference tab. Confirm the 'Sample XML from database' editor fills the visible area vertically (substantially taller than the default single-line input) and is read-only (typing does nothing)."
    expected: "Editor renders with approximately 30 rows of visible height. No text can be entered."
    why_human: "D-08/D-10: Rows=30 and Readonly=true are set in source (verified by grep) but the visual rendering depends on CoreUI's Textarea layout in the admin UI context."
---

# Phase 41: Admin-UI Polish — Verification Report

**Phase Goal:** Resolve the five admin-UI issues surfaced during manual verification of Phase 40's flat-shape deploy — D-01 tree-node rename ("Item Types" → "Item Type Excludes"), D-03 sanity-check empty `excludeFieldsByItemType` on Swift 2.2 baseline, D-05/D-06 dual-list saved-exclusion merge in XmlTypeEditScreen and ItemTypeEditScreen, D-08/D-09/D-10 enlarge "Sample XML from database" editor, D-11 drop parens from Mode option labels, D-12 hint copy on Mode attribute, D-13 Mode dropdown binds correctly (string-typed PredicateEditModel.Mode).
**Verified:** 2026-05-01T18:00:00Z
**Status:** human_needed — all automated checks pass (13/13); 5 live-host behavioral items require manual confirmation.
**Re-verification:** No — initial verification.

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Tree node displays "Item Type Excludes" (D-01) | VERIFIED | `SerializerSettingsNodeProvider.cs:75` — `Name = "Item Type Excludes"` confirmed by grep |
| 2 | ItemTypeListScreen.GetScreenName() returns "Item Type Excludes" (D-01) | VERIFIED | `ItemTypeListScreen.cs:13` — `GetScreenName() => "Item Type Excludes"` confirmed by grep |
| 3 | ItemTypeEditScreen.GetScreenName() returns "Item Type Excludes - {SystemName}" (D-01) | VERIFIED | `ItemTypeEditScreen.cs:157` — ternary confirmed; tests pass GREEN |
| 4 | Internal const ItemTypesNodeId = "Serializer_ItemTypes" preserved (D-02) | VERIFIED | `SerializerSettingsNodeProvider.cs:27` — const unchanged; grep confirms |
| 5 | excludeFieldsByItemType absence documented as intentional in Swift2.2-baseline.md (D-03/D-04) | VERIFIED | `docs/baselines/Swift2.2-baseline.md:84` — `## Exclusion sections` present; 4 mentions of `excludeFieldsByItemType`, 1 `empty by design`, 1 `c5d9a8c`, 1 `Phase 41 D-03` |
| 6 | XmlTypeEditScreen.CreateElementSelector merges saved exclusions even when discovery returns 0 (D-05) | VERIFIED | `XmlTypeEditScreen.cs:138` — `allElements.Add(s)` in merge loop; `Discovery ?? new XmlTypeDiscovery(new DwSqlExecutor())` at 2 use-sites; 2 RED→GREEN tests confirmed by live test run |
| 7 | ItemTypeEditScreen.CreateFieldSelector merges saved field exclusions even when ItemManager.Metadata returns null (D-06) | VERIFIED | `ItemTypeEditScreen.cs:114,132` — `allFields.Add` in both live-discovery loop and saved-exclusion merge; RED→GREEN test confirmed |
| 8 | Sample XML Textarea has Rows = 30 (D-08/D-09) and Readonly = true (D-10) | VERIFIED | `XmlTypeEditScreen.cs:75` — `Rows = 30` confirmed; `Readonly = true` was pre-existing; no parens-removal needed |
| 9 | PredicateEditScreen Mode option labels are "Deploy" and "Seed" with no parenthetical suffixes (D-11) | VERIFIED | `PredicateEditScreen.cs:91-92` — `Label = "Deploy"` and `Label = "Seed"` confirmed; zero matches for `(source-wins)` or `(field-level merge)` in that file |
| 10 | PredicateEditModel Mode [ConfigurableProperty] uses hint: parameter with explanatory copy containing "Deploy =" and "Seed =" (D-12) | VERIFIED | `PredicateEditModel.cs:22` — `hint: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields)."` confirmed; test `ModeProperty_HasHint_WithExplanatoryCopy_PostPhase41` passes GREEN |
| 11 | PredicateEditModel.Mode is string-typed; defaults to nameof(DeploymentMode.Deploy) (D-13) | VERIFIED | `PredicateEditModel.cs:23` — `public string Mode { get; set; } = nameof(DeploymentMode.Deploy);` confirmed; test `ModeProperty_IsString_NotEnum_PostPhase41` passes GREEN |
| 12 | SavePredicateCommand parses Mode via Enum.TryParse early-gate (invalid Mode → ResultType.Invalid) and Enum.Parse backstop (D-13 + T-41-01) | VERIFIED | `SavePredicateCommand.cs:40` — `Enum.TryParse<DeploymentMode>` early-gate; `cs:258` — `Enum.Parse<DeploymentMode>` in ParseMode helper; `Save_ModeAsString_BogusValue_ReturnsInvalid_PostPhase41` passes GREEN |
| 13 | PredicateByIndexQuery hydrates Mode as pred.Mode.ToString() (D-13) | VERIFIED | `PredicateByIndexQuery.cs:32` — `Mode = pred.Mode.ToString()` confirmed; round-trip tests pass GREEN |

**Score: 13/13 truths verified**

---

### Deferred Items

None — all 13 truths are met. No items are deferred to later phases.

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` | Name = "Item Type Excludes" at line 75; const preserved | VERIFIED | grep confirms both |
| `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeListScreen.cs` | GetScreenName() => "Item Type Excludes" | VERIFIED | line 13 confirmed |
| `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs` | "Item Type Excludes - {Model.SystemName}" ternary; CreateFieldSelector merge | VERIFIED | lines 157, 114, 132 confirmed |
| `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` | Label = "Deploy" and Label = "Seed"; no parens | VERIFIED | lines 91-92 confirmed; zero parens-pattern matches |
| `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs` | DI seam; allElements.Add(s); Rows = 30; Discovery?? at 2 sites | VERIFIED | lines 22, 61, 75, 120, 138 confirmed |
| `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` | string Mode; hint: copy | VERIFIED | lines 22-23 confirmed |
| `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` | Enum.TryParse early-gate; ParseMode helper | VERIFIED | lines 40, 254 confirmed |
| `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs` | Mode = pred.Mode.ToString() | VERIFIED | line 32 confirmed |
| `docs/baselines/Swift2.2-baseline.md` | ## Exclusion sections with excludeFieldsByItemType | VERIFIED | line 84 + 4 content mentions confirmed |
| `tests/.../XmlTypeEditScreenTests.cs` | 3 facts GREEN (D-05 merge) | VERIFIED | test run: 109/109 passed |
| `tests/.../ItemTypeEditScreenTests.cs` | 4 facts GREEN (D-06 merge + D-01 rename) | VERIFIED | test run: all pass |
| `tests/.../PredicateCommandTests.cs` | +6 PostPhase41 facts GREEN (D-11/D-12/D-13) | VERIFIED | test run: all pass |
| `tests/.../SerializerSettingsNodeProviderModeTreeTests.cs` | +1 tree-node-rename fact GREEN (D-01) | VERIFIED | test run: all pass |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| XmlTypeEditScreen.CreateElementSelector | Model.ExcludedElements | saved exclusions parsed and merged into allElements HashSet | VERIFIED | `Model\.ExcludedElements.*Split` pattern found; merge loop `allElements.Add(s)` at line 138 |
| ItemTypeEditScreen.CreateFieldSelector | Model.ExcludedFields | saved exclusions parsed and merged into allFields HashSet | VERIFIED | merge loop `allFields.Add(s)` at line 132 |
| SavePredicateCommand.Handle | DeploymentMode enum | Enum.Parse<DeploymentMode> at 2 predicate-construction sites | VERIFIED | line 258 confirmed; TryParse early-gate at line 40 |
| PredicateByIndexQuery.GetModel | PredicateEditModel.Mode (string) | pred.Mode.ToString() | VERIFIED | line 32 confirmed |
| PredicateEditScreen Mode Select | ListOption.Label | "Deploy" / "Seed" clean labels | VERIFIED | lines 91-92 confirmed |
| SerializerSettingsNodeProvider ItemTypesNode | NavigationNode.Name | Name = "Item Type Excludes" | VERIFIED | line 75 confirmed |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| XmlTypeEditScreen.CreateElementSelector | allElements HashSet + editor.Options | XmlTypeDiscovery.DiscoverElementsForType() UNION Model.ExcludedElements | Yes — union of live discovery and saved exclusions | FLOWING |
| ItemTypeEditScreen.CreateFieldSelector | allFields HashSet + editor.Options | ItemManager.Metadata.GetItemFields() UNION Model.ExcludedFields | Yes — union with per-field-label dictionary | FLOWING |
| PredicateByIndexQuery.GetModel | PredicateEditModel.Mode | pred.Mode.ToString() from persisted DeploymentMode enum | Yes — round-trips via Enum.Parse in SavePredicateCommand | FLOWING |

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| All 109 AdminUI tests pass GREEN | `dotnet test --filter "FullyQualifiedName~AdminUI" --no-restore` | 109 passed, 0 failed | PASS |
| No "Item Types" user-visible string remains | `grep -rn '"Item Types"' src/.../AdminUI/` | 0 matches | PASS |
| No enum-typed Mode on PredicateEditModel | `grep -n 'public DeploymentMode Mode' src/.../AdminUI/` | 0 in PredicateEditModel (1 expected in PredicateListModel — list-display only, not Select-bound) | PASS |
| No old Mode = Model.Mode, assignment | `grep -rn 'Mode = Model\.Mode,' src/.../AdminUI/` | 0 matches | PASS |
| No old Mode = pred.Mode, hydration | `grep -rn 'Mode = pred\.Mode,' src/.../AdminUI/` | 0 matches | PASS |
| No parens-suffix option labels | `grep -n '(source-wins)\|(field-level merge)' PredicateEditScreen.cs` | 0 user-label matches | PASS |

---

### Requirements Coverage

Phase 41 uses the goal-driven D-01..D-13 decision IDs from 41-CONTEXT.md (no legacy REQUIREMENTS.md REQ-IDs). REQUIREMENTS.md covers v0.4.0 requirements (PAGE-*, ECOM-*, AREA-*, SCHEMA-*) — none of which are in scope for Phase 41. The ROADMAP.md Phase 41 entry uses D-01..D-13 as acceptance criteria.

| Decision ID | Plan | Description | Status | Evidence |
|-------------|------|-------------|--------|----------|
| D-01 | 41-02 | Rename "Item Types" → "Item Type Excludes" (tree + list + edit screen) | SATISFIED | 3 source strings replaced; 3 tests GREEN |
| D-02 | 41-02 | Internal const ItemTypesNodeId preserved as "Serializer_ItemTypes" | SATISFIED | grep confirms const unchanged at line 27 |
| D-03 | 41-02 | Audit excludeFieldsByItemType — document intentional empty | SATISFIED | Swift2.2-baseline.md ## Exclusion sections present with full audit trail |
| D-04 | 41-02 | Autonomous resolution: document empty, not restore | SATISFIED | Section documents WhenWritingNull behavior + c5d9a8c~ audit; no content restoration |
| D-05 | 41-03 | CreateElementSelector merges saved into discovered (union, not early-return) | SATISFIED | allElements.Add(s) merge loop; FakeSqlExecutor-backed tests GREEN |
| D-06 | 41-03 | CreateFieldSelector same-shape fix for ItemType field excludes | SATISFIED | allFields.Add(s) merge loop; test GREEN |
| D-07 | 41-03 | Fix-and-go once root cause identified (no checkpoint needed) | SATISFIED | Both D-05 and D-06 root cause identified and fixed in 41-03 |
| D-08 | 41-03 | Sample XML Textarea enlarged (Rows = 30) | SATISFIED | XmlTypeEditScreen.cs:75 Rows = 30 confirmed |
| D-09 | 41-03 | Pragmatic: large Textarea (not Code/Monaco editor) | SATISFIED | Textarea with Rows=30 chosen per plan guidance |
| D-10 | 41-03 | Sample XML editor is read-only | SATISFIED | Readonly = true at XmlTypeEditScreen.cs:74 confirmed |
| D-11 | 41-02 | Mode option labels "Deploy" and "Seed" (no parens) | SATISFIED | PredicateEditScreen.cs:91-92 confirmed; test GREEN |
| D-12 | 41-03 | Mode [ConfigurableProperty] hint: copy with "Deploy =" and "Seed =" | SATISFIED | PredicateEditModel.cs:22 hint: parameter confirmed; test GREEN |
| D-13 | 41-03 | Mode property string-typed; SavePredicateCommand Enum.Parse; PredicateByIndexQuery .ToString() | SATISFIED | All 3 source changes confirmed; 4 round-trip + validation tests GREEN |

---

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
|------|---------|----------|--------|
| `src/DynamicWeb.Serializer/AdminUI/Models/PredicateListModel.cs:14` | `public DeploymentMode Mode { get; set; }` | Info | Expected — this is the list-screen display model (read-only ModeDisplay property derives a string for rendering). It is NOT Select-bound and is therefore not subject to D-13's enum-vs-string binding requirement. Noted in 41-03-SUMMARY.md regression-grep section as an expected 1-match. No action required. |

No blockers. No stubs. No placeholder implementations found.

---

### Human Verification Required

These items cannot be confirmed by automated grep or unit tests. They require a running DW admin host.

#### 1. D-01: "Item Type Excludes" tree label, breadcrumb, and screen titles render correctly

**Test:** Open `https://localhost:54035/Admin/` → Settings → System → Developer → Serialize. Inspect the tree node label, the list screen title after clicking in, and the edit screen title after clicking into any item type.
**Expected:** Tree node reads "Item Type Excludes". List screen title reads "Item Type Excludes". Edit screen title reads "Item Type Excludes - {SystemName}".
**Why human:** The DW framework renders NavigationNode.Name into the tree UI and breadcrumb via the CoreUI rendering pipeline; source confirms the string but only a live host confirms the framework picks it up correctly.

#### 2. D-05: eCom_CartV2 dual-list shows 21 saved exclusions on detail page

**Test:** Open Embedded XML → eCom_CartV2 in the admin UI. Inspect the "Excluded Elements" dual-list.
**Expected:** The right-hand (excluded) side shows the 21 saved elements. The left-hand (available) side may be empty if live discovery returns 0 rows — that is acceptable and is the exact scenario the fix addresses.
**Why human:** Unit tests exercise the fix via FakeSqlExecutor injection. The live-host path goes through the real DwSqlExecutor against the actual Swift 2.2 DB and is the original repro context from Phase 40 manual verification.

#### 3. D-13: Mode dropdown renders without screen error; correct value selected

**Test:** Open Predicates → any saved Deploy predicate in the admin UI.
**Expected:** Screen renders without a "selected option no longer exists" error or blank-screen error. Mode dropdown shows "Deploy" selected with clean label (no parens suffix).
**Why human:** D-13 is the original screen error trigger from Phase 40. Unit tests confirm the round-trip but the live CoreUI Select binding pipeline (framework-side) cannot be exercised without a running host.

#### 4. D-12: Mode hint copy surfaces in the UI

**Test:** On the predicate edit screen, locate the Mode field. Hover the label or inspect adjacent text.
**Expected:** The hint text appears: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields)."
**Why human:** The `hint:` parameter on `ConfigurablePropertyAttribute` is unit-tested (attr.Hint contains expected substrings), but CoreUI's rendering of the Hint property — whether as tooltip, label annotation, or inline text — requires a live host to confirm discoverability.

#### 5. D-08/D-10: Sample XML editor is visually enlarged and read-only

**Test:** Open Embedded XML → eCom_CartV2 → reference tab. Inspect the "Sample XML from database" field.
**Expected:** Editor renders substantially taller than a single-line input (approximately 30 rows). Attempting to type produces no change.
**Why human:** Rows=30 and Readonly=true are confirmed in source, but the visual result in the CoreUI admin tab layout requires a live render to confirm no clipping or layout interference.

---

### Gaps Summary

No automated gaps found. All 13 truths are verified by source inspection and the full AdminUI test suite (109/109 passing, 0 RED). The phase goal is achieved at the code level.

Status is `human_needed` because 5 live-host visual and behavioral items require manual confirmation post-deploy. These are not gaps in correctness — they are rendering and binding behaviors that cannot be exercised without a running DW admin host.

The prior Phase 41 plan documented the live-host smoke as deferred to user manual verification (41-03-SUMMARY.md "Live-host Smoke" section). The human verification items above formalize that deferred step.

---

_Verified: 2026-05-01T18:00:00Z_
_Verifier: Claude (gsd-verifier)_

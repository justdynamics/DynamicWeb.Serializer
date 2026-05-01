# Phase 41: Admin-UI polish + cross-page consistency — Context

**Gathered:** 2026-05-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Five admin-UI / dev-experience fixes surfaced during manual verification of Phase 40's
flat-shape Deploy/Seed deploy on the live Swift 2.2 host (https://localhost:54035/Admin/).
All scoped to the admin UI plus one config-content sanity check — no runtime config-shape
changes, no ConfigLoader / ConfigWriter / Provider pipeline rewrites. Phase 40's flat
predicate model stands.

**In scope:**

1. Rename "Item Types" UI label to "Item Type Excludes" everywhere a user sees it
2. Sanity-check the empty `excludeFieldsByItemType` on the Phase 40 swift2.2-combined.json
   baseline (and likely-thin `excludeXmlElementsByType` too) — restore if lost during
   40-04's rewrite, document if intentional
3. Fix the eCom_CartV2 list-vs-detail dual-list mismatch (list shows 21 excluded
   elements, detail page dual-list shows 0 elements on the excluded side)
4. Enlarge the "Sample XML from database" editor on `XmlTypeEditScreen` to fill the
   reference tab content area
5. Fix the Mode dropdown screen error on `PredicateEditScreen` — drop the parenthetical
   `(source-wins)` / `(field-level merge)` suffixes from the option labels and surface
   that explanatory text as a tooltip on the select label

**Out of scope:**
- Any change to `SerializerConfiguration`, `ConfigLoader`, `ConfigWriter`, or the
  `ProviderPredicateDefinition.Mode` model — Phase 40's contracts stand
- Any new admin-UI capabilities (e.g., bulk-import predicates, history/audit views) —
  those would be their own phases
- The `ItemTypePerModeTests` / `XmlTypePerModeTests` deletion that Plan 03 already did
- The 9 pre-existing `DependencyResolverException` integration-test failures — pure
  environmental, unrelated to Phase 41's surface

</domain>

<decisions>
## Implementation Decisions

### Item Types → Item Type Excludes rename (D-01)
- **D-01**: Rename scope is **tree + list screen + breadcrumb**. Plan touches:
  - `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` (line 75:
    `Name = "Item Types"`)
  - `ItemTypeListScreen` title / heading
  - `ItemTypeEditScreen` title / heading (when viewing a specific ItemType's exclusion
    set, "Item Type Excludes — {systemName}" or similar — exact copy is researcher's
    call)
  - Any breadcrumb-path provider that emits the segment label
  - The label `Item Types` is misleading because the page does NOT manage Item Types
    (those are owned by the DW10 ItemManager pipeline) — it manages **per-ItemType
    field exclusions** for serialization. The new label states that intent directly.
- **D-02**: Internal C# identifier `ItemTypesNodeId` (the const) does NOT need to be
  renamed — it's an internal id, not user-visible. Leave it as `Serializer_ItemTypes`
  to avoid churn in `SerializerSettingsNodeProviderModeTreeTests`.

### Empty Phase 40 baseline excludes — investigate first (D-03)
- **D-03**: Run a content audit of the post-Phase-40 `swift2.2-combined.json`
  `excludeFieldsByItemType` and `excludeXmlElementsByType` against the **pre-Phase-40**
  baseline that was deleted in commit `c5d9a8c` (`chore(baseline): remove obsolete
  swift2.2-baseline.json`). Surface that file via `git show
  c5d9a8c~:src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json` (or earlier
  commits in the file's history) and diff its exclusion content against the new flat
  shape. If content was dropped during the 40-04 rewrite, restore it; if the new shape
  is intentionally empty, the planner documents why in
  `docs/baselines/Swift2.2-baseline.md` and confirms with the user before closing.
- **D-04**: The decision between "restore" and "document empty" lives in CONTEXT and the
  executor follows what the audit shows. NOT a checkpoint — autonomous resolution as
  long as the audit produces a clear answer.

### eCom_CartV2 list-vs-detail dual-list mismatch (D-05)
- **D-05**: Trust the **list count** (21 excluded) and fix the **detail-page dual-list
  state load**. Rationale: the list reads `config.ExcludeXmlElementsByType[TypeName]`
  directly via `XmlTypeListQuery` (presumably) and shows the dict's actual content.
  The detail page reads via `XmlTypeByNameQuery.cs:31` which calls
  `config.ExcludeXmlElementsByType.TryGetValue(TypeName, out var excludedElements)` —
  if `TypeName` arrives in a different normalization than the list-side key, the
  TryGetValue misses and the dual-list renders with empty "excluded" side. Likely
  causes: case sensitivity, dotted-FQN vs short-name, or URL-decoding of the route
  parameter. Researcher reproduces + identifies, executor fixes.
- **D-06**: Check whether the **same lookup pattern exists for ItemType field excludes**
  (`ItemTypeBySystemNameQuery` reads from `config.ExcludeFieldsByItemType` — same
  shape, same risk of key-normalization drift). If the same bug is present, fix both;
  if not, document why ItemType escaped (likely because list and detail use the same
  normalization helper).
- **D-07**: Does NOT need a checkpoint — fix-and-go once root cause is identified. If
  the root cause turns out to be more architectural than a normalization fix
  (e.g., requires the dual-list editor to take a different model shape), researcher
  flags and orchestrator routes back here.

### Sample XML editor — fill the tab via CSS (D-08)
- **D-08**: The `Label = "Sample XML from database"` editor at
  `XmlTypeEditScreen.cs:62` should fill the reference-tab content area by default
  rather than rely on a fixed `WithRows(N)`. Researcher's job: identify the right
  CoreUI knob — likely a CSS class on the `Textarea` editor, or switching to the
  CoreUI `Code` editor if that has a fill-parent height mode out of the box.
- **D-09**: If switching to a Code/Monaco-style editor lands cleanly *and* gives
  syntax highlighting + line numbers as a side benefit, that's preferred. If it
  fights the fill-parent layout or breaks the rest of the screen, fall back to a
  large fixed-rows `Textarea` (e.g., `WithRows(40)`) — pragmatic over pretty.
- **D-10**: This editor is **read-only** in intent (it's a sample for reference). If
  the chosen editor has a read-only mode, set it. If not, no escalation needed —
  the field is not bound to anything that gets persisted.

### Mode dropdown — drop suffix + tooltip (D-11)
- **D-11**: At `PredicateEditScreen.cs:88-89`, change option labels:
  - `"Deploy (source-wins)"` → `"Deploy"`
  - `"Seed (field-level merge)"` → `"Seed"`
- **D-12**: Surface the explanatory text as a **tooltip on the select's label** (the
  field title rendered next to the dropdown). Tooltip text:
  `"Deploy = source-wins (YAML overwrites destination). Seed = destination-wins
   field-level merge (only fills empty destination fields)."`
- **D-13**: Researcher confirms the actual screen-error root cause matches the
  hypothesis (parens in option labels confusing the Select component or breaking
  value/label normalization). If the root cause turns out to be different (e.g., a
  binding bug between `nameof(DeploymentMode.Deploy)` and the persisted enum value,
  or a `WithReloadOnChange` interaction), the fix follows the actual cause —
  but the label cleanup + tooltip stay as the desired UX regardless.

### Claude's Discretion
- Exact copy text for renamed UI labels and the tooltip wording — researcher / planner
  picks final wording within the constraints above
- Choice between `Code` editor and large `Textarea` for the Sample XML field — pick
  the one that drops in cleaner; tradeoff explained in plan
- Whether to bundle the ItemType bySystemName lookup audit (D-06) as a single plan
  with the eCom_CartV2 fix or split into two plans — planner's call
- Whether the rename + the dropdown fix go in the same plan (both touch admin-UI
  screens, both are mechanical) or split — planner's call

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 40 outputs (the contracts this phase polishes)
- `.planning/phases/40-per-predicate-deploy-seed-split-replace-section-level-config/40-01-SUMMARY.md` — final flat-shape config model + ConfigLoader/Writer behavior
- `.planning/phases/40-per-predicate-deploy-seed-split-replace-section-level-config/40-03-SUMMARY.md` — single-tree admin UI rewrite + per-predicate Mode editor introduction
- `.planning/phases/40-per-predicate-deploy-seed-split-replace-section-level-config/40-04-SUMMARY.md` — swift2.2-combined.json baseline rewrite (the file under audit in D-03)
- `.planning/phases/40-per-predicate-deploy-seed-split-replace-section-level-config/40-VERIFICATION.md` — Phase 40 disposition + the post-merge follow-up commits

### Pre-Phase-40 baseline (the audit comparison source for D-03)
- `swift2.2-baseline.json` at commit `c5d9a8c~` — pre-Phase-40 baseline file deleted
  during Phase 40. Recoverable via `git show
  c5d9a8c~:src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json`. Contains
  the `excludeFieldsByItemType` set that may have been lost during 40-04's flat-shape
  rewrite.
- `docs/baselines/Swift2.2-baseline.md` — Swift 2.2 baseline reasoning doc

### Project decisions
- `.planning/PROJECT.md` — core principle and no-backcompat policy still apply
- `CLAUDE.md` — project conventions including the DW admin-UI patterns memory entries

### DW admin-UI conventions
- Memory: `feedback_dw_patterns.md` — DW admin UI patterns (MapPath,
  WithReloadOnChange in dialogs, Select value types, ShadowEdit overlay). Researcher
  must read this before touching `PredicateEditScreen` or `XmlTypeEditScreen`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ConfigLoaderValidatorFixtureBase` (tests/DynamicWeb.Serializer.Tests/TestHelpers/) —
  permissive identifier validator for unit tests that load configs with SqlTable
  predicates. Already in use; any new admin-UI test that calls
  `ConfigLoader.Load(parameterless)` should extend it (recent post-merge fix
  pattern from Phase 40 Wave 2).
- `XmlTypeDiscovery.GetSampleXml(typeName)` — already provides the sample XML payload
  the editor displays. Sizing fix is purely render-side; no service-layer change.
- CoreUI `Select`, `Textarea`, `Tooltip`-class APIs are the established pattern across
  every existing admin screen — research these before introducing new editor types.

### Established Patterns
- Tree provider name lookup: `SerializerSettingsNodeProvider.GetSubNodes` switches on
  `parentNodePath.Last`. Adding a label rename is local to one file.
- Detail-page state load: `*ByNameQuery.Get()` returns a model populated from
  `config.Exclude*` dicts. The dual-list editor binds to `Model.ExcludedElements`
  (or the equivalent property name). Mismatch in `Model.ExcludedElements` populated
  vs displayed is the most likely root-cause site for D-05.
- Admin tooltips: existing pattern probably uses a `WithTooltip("...")` extension on
  the editor builder. Research codebase to confirm before assuming.

### Integration Points
- `PredicateEditScreen.cs:83-89` — Mode `Select` builder. The Mode label tooltip
  attaches here.
- `XmlTypeEditScreen.cs:62` — Sample XML editor builder. Sizing fix attaches here.
- `XmlTypeByNameQuery.cs:31` (`config.ExcludeXmlElementsByType.TryGetValue(...)`) — the
  most likely root-cause site for the dual-list discrepancy.
- `SerializerSettingsNodeProvider.cs:75` — the tree label rename anchor.

</code_context>

<specifics>
## Specific Ideas

- The `eCom_CartV2` mismatch: list shows 21 excluded, detail page shows 0 on the
  "excluded" side of the dual-list. Repro target is the running Swift 2.2 host with
  the new Phase 40 DLLs already deployed.
- The Mode-dropdown screen error: opening a Deploy predicate after Phase 40's deploy
  triggers an error on the screen. Researcher needs to capture the actual error
  message + stack to confirm root cause matches hypothesis (parens) before locking
  the fix path.
- The "Sample XML" editor is on the **reference tab** of `XmlTypeEditScreen` — not
  the main edit tab. Mention this distinction so researcher inspects the right tab
  layout context.

</specifics>

<deferred>
## Deferred Ideas

- "Help text below the select" alternative for the Mode dropdown was rejected in
  favor of the tooltip — if the tooltip turns out to be discoverable enough, this
  stays deferred forever. If user feedback says tooltips are missed, revisit in a
  future polish phase.
- Switching to a full Code/Monaco-style editor for **all** XML/JSON-rendering admin
  fields (not just the Sample XML one) — would be a broader UX phase if pursued.
- Auto-formatting the displayed sample XML on render — currently the field shows
  the raw payload; pretty-printing could be a small future polish but is out of
  scope here.
- The 9 environmental `DependencyResolverException` integration-test failures —
  needs a separate test-infra phase to bootstrap the DW host context for those
  tests; explicitly NOT this phase's problem.

</deferred>

---

*Phase: 41-admin-ui-polish*
*Context gathered: 2026-05-01*

# Roadmap: DynamicWeb.Serializer

## Milestones

- [x] **v1.0 MVP** - Phases 1-5 (shipped 2026-03-20) - [Archive](milestones/v1.0-ROADMAP.md)
- [x] **v1.1 Robustness** - Phase 6 (shipped 2026-03-20) - [Archive](milestones/v1.1-ROADMAP.md)
- [x] **v1.2 Admin UI** - Phases 7-10 (shipped 2026-03-22)
- [x] **v1.3 Permissions** - Phases 11-12 (shipped 2026-03-23)
- [x] **v2.0 DynamicWeb.Serializer** - Phases 13-18 (shipped 2026-03-24)
- [x] **v0.3.1 Internal Link Resolution** - Phases 19-22 (shipped 2026-04-03)
- [x] **v0.4.0 Full Page Fidelity** - Phases 23-25 (shipped 2026-04-07)
- [x] **v0.5.0 Granular Serialization Control** - Phases 26-31 (shipped 2026-04-09) - [Archive](milestones/v0.5.0-ROADMAP.md)
- [ ] **v0.6.0 UI Configuration Improvements** - Phases 32-37 (in progress)

## Phases

<details>
<summary>v1.0 MVP (Phases 1-5) - SHIPPED 2026-03-20</summary>

- [x] Phase 1: Foundation (2/2 plans) - completed 2026-03-19
- [x] Phase 2: Configuration (1/1 plans) - completed 2026-03-19
- [x] Phase 3: Serialization (3/3 plans) - completed 2026-03-19
- [x] Phase 4: Deserialization (2/2 plans) - completed 2026-03-19
- [x] Phase 5: Integration (2/2 plans) - completed 2026-03-19

</details>

<details>
<summary>v1.1 Robustness (Phase 6) - SHIPPED 2026-03-20</summary>

- [x] Phase 6: Sync Robustness (2/2 plans) - completed 2026-03-20

</details>

<details>
<summary>v1.2 Admin UI (Phases 7-10) - SHIPPED 2026-03-22</summary>

- [x] Phase 7: Config Infrastructure + Settings Tree Node (2/2 plans) - completed
- [x] Phase 8: Settings Screen (1/1 plans) - completed
- [x] Phase 9: Predicate Management (2/2 plans) - completed
- [x] Phase 10: Context Menu Actions (3/3 plans) - completed 2026-03-22

</details>

<details>
<summary>v1.3 Permissions (Phases 11-12) - SHIPPED 2026-03-23</summary>

- [x] Phase 11: Permission Serialization (1/1 plans) - completed 2026-03-22
- [x] Phase 12: Permission Deserialization + Docs (2/2 plans) - completed 2026-03-23

</details>

<details>
<summary>v2.0 DynamicWeb.Serializer (Phases 13-18) - SHIPPED 2026-03-24</summary>

- [x] Phase 13: Provider Foundation + SqlTableProvider Proof (3/3 plans) - completed 2026-03-23
- [x] Phase 14: Content Migration + Orchestrator (2/2 plans) - completed 2026-03-24
- [x] Phase 15: Ecommerce Tables at Scale (2/2 plans) - completed 2026-03-24
- [x] Phase 16: Admin UX + Rename (5/5 plans) - completed 2026-03-24
- [x] Phase 17: Project Rename - Absorbed into Phase 16
- [x] Phase 18: Predicate Config Multi-Provider (2/2 plans) - completed 2026-03-24

</details>

<details>
<summary>v0.3.1 Internal Link Resolution (Phases 19-22) - SHIPPED 2026-04-03</summary>

- [x] Phase 19: Source ID Serialization (1/1 plans) - completed 2026-04-03
- [x] Phase 20: Link Resolution Core (2/2 plans) - completed 2026-04-03
- [x] Phase 21: Paragraph Anchor Resolution (1/1 plans) - completed 2026-04-03
- [x] Phase 22: Version Housekeeping (1/1 plans) - completed 2026-04-03

</details>

<details>
<summary>v0.4.0 Full Page Fidelity (Phases 23-25) - SHIPPED 2026-04-07</summary>

- [x] **Phase 23: Full Page Properties + Navigation Settings** (2/2 plans) - completed 2026-04-03
- [x] **Phase 24: Area ItemType Fields** (1/1 plans) - completed 2026-04-03
- [x] **Phase 25: Ecommerce Schema Sync** (1/1 plans) - completed 2026-04-03

</details>

<details>
<summary>v0.5.0 Granular Serialization Control (Phases 26-31) - SHIPPED 2026-04-09</summary>

- [x] **Phase 26: XML Pretty-Print for Content** (1/1 plans) - completed 2026-04-07
- [x] **Phase 27: XML Pretty-Print for SqlTable** (1/1 plans) - completed 2026-04-07
- [x] **Phase 28: Field-Level Filtering Core** (1/1 plans) - completed 2026-04-07
- [x] **Phase 29: SqlTable Field Filtering** (1/1 plans) - completed 2026-04-07
- [x] **Phase 30: Area Property Consolidation** (1/1 plans) - completed 2026-04-07
- [x] **Phase 31: Predicate UI Enhancement** (1/1 plans) - completed 2026-04-07

</details>

### v0.6.0 UI Configuration Improvements (In Progress)

**Milestone Goal:** Replace all free-text configuration with structured, discoverable UI controls across item types, embedded XML definitions, predicates, area settings, and SQL tables.

- [x] **Phase 32: Config Schema Extension** - Add dictionary-based exclusion config alongside existing flat arrays (completed 2026-04-09)
- [x] **Phase 33: SqlTable Column Pickers** - Replace free-text excludeFields/xmlColumns with schema-driven CheckboxLists (completed 2026-04-11)
- [ ] **Phase 34: Embedded XML Screens** - New tree node with auto-discovery and element-level exclusion
- [ ] **Phase 35: Item Type Screens** - New tree node with per-item-type field exclusion CheckboxList
- [ ] **Phase 36: Area Screens** - New tree node with per-area column exclusion CheckboxList
- [ ] **Phase 37: Predicate UI Polish** - Page picker for exclusions, read-only summaries with cross-links

## Phase Details

### Phase 32: Config Schema Extension
**Goal**: Config JSON supports typed exclusion dictionaries so UI screens can persist per-type and per-item-type settings
**Depends on**: Nothing (foundation for all v0.6.0 work)
**Requirements**: CFG-01, CFG-02
**Success Criteria** (what must be TRUE):
  1. Config JSON accepts `excludeFieldsByItemType` as a dictionary mapping item type names to field lists, and `excludeXmlElementsByType` mapping XML type names to element lists
  2. Existing v0.5.0 configs with flat `excludeFields` and `excludeXmlElements` arrays load and function identically (no breaking change)
  3. Both flat arrays and typed dictionaries are applied during serialize/deserialize (additive merge)
**Plans**: 2 plans
Plans:
- [x] 32-01-PLAN.md -- Config model extension + backward compat tests
- [x] 32-02-PLAN.md -- ExclusionMerger helper + pipeline integration

### Phase 33: SqlTable Column Pickers
**Goal**: SqlTable predicate editing uses auto-populated column selectors instead of free-text entry
**Depends on**: Phase 32
**Requirements**: PRED-04, PRED-05
**Success Criteria** (what must be TRUE):
  1. SqlTable predicate edit screen shows excludeFields as a CheckboxList populated from the table's actual SQL column schema
  2. SqlTable predicate edit screen shows xmlColumns as a CheckboxList populated from the table's actual SQL column schema
  3. Selections persist to config JSON and are applied during serialize/deserialize
**Plans**: 1 plan
Plans:
- [x] 33-01-PLAN.md -- CheckboxList editors + round-trip tests
**UI hint**: yes

### Phase 34: Embedded XML Screens
**Goal**: Users can discover XML types present in their data and configure element-level exclusions per type through a dedicated tree node
**Depends on**: Phase 32
**Requirements**: XMLUI-01, XMLUI-02, XMLUI-03, XMLUI-04
**Success Criteria** (what must be TRUE):
  1. "Embedded XML" tree node appears under Serialize and lists XML types that have been discovered
  2. User can trigger a "Scan" action that queries the database for distinct moduleSystemName and urlDataProviderTypeName values and populates the list
  3. Clicking an XML type opens an edit screen showing all elements from that type as a CheckboxList for exclusion selection
  4. Element exclusions are saved to config under `excludeXmlElementsByType` and applied during serialization
**Plans**: 2 plans
Plans:
- [ ] 32-01-PLAN.md -- Config model extension + backward compat tests
- [ ] 32-02-PLAN.md -- ExclusionMerger helper + pipeline integration
**UI hint**: yes

### Phase 35: Item Type Screens
**Goal**: Users can browse all item types in the system and configure per-item-type field exclusions through a dedicated tree node
**Depends on**: Phase 32
**Requirements**: ITEM-01, ITEM-02, ITEM-03
**Success Criteria** (what must be TRUE):
  1. "Item Types" tree node appears under Serialize and lists all item types discovered in the system
  2. Clicking an item type opens an edit screen showing all fields for that type as a CheckboxList where the user selects fields to exclude
  3. Field exclusions are saved to config under `excludeFieldsByItemType` and applied during serialize/deserialize
**Plans**: 2 plans
Plans:
- [ ] 32-01-PLAN.md -- Config model extension + backward compat tests
- [ ] 32-02-PLAN.md -- ExclusionMerger helper + pipeline integration
**UI hint**: yes

### Phase 36: Area Screens
**Goal**: Users can browse all areas and configure per-area column exclusions through a dedicated tree node
**Depends on**: Phase 32
**Requirements**: AREA-06, AREA-07, AREA-08
**Success Criteria** (what must be TRUE):
  1. "Areas" tree node appears under Serialize and lists all areas in the system
  2. Clicking an area opens an edit screen showing all area columns as a CheckboxList where the user selects columns to exclude
  3. Column exclusions are saved to config and applied during serialize/deserialize
**Plans**: 2 plans
Plans:
- [ ] 32-01-PLAN.md -- Config model extension + backward compat tests
- [ ] 32-02-PLAN.md -- ExclusionMerger helper + pipeline integration
**UI hint**: yes

### Phase 37: Predicate UI Polish
**Goal**: Content predicate editing uses structured controls and links to related config screens instead of raw text fields
**Depends on**: Phase 34, Phase 35
**Requirements**: PRED-01, PRED-02, PRED-03
**Success Criteria** (what must be TRUE):
  1. Content predicate page exclusions use a multi-select page picker control instead of a free-text textarea
  2. Predicate filtering section displays item field exclusions as a read-only summary with a clickable link navigating to the Item Types screen
  3. Predicate filtering section displays XML element exclusions as a read-only summary with a clickable link navigating to the Embedded XML screen
**Plans**: 2 plans
Plans:
- [ ] 32-01-PLAN.md -- Config model extension + backward compat tests
- [ ] 32-02-PLAN.md -- ExclusionMerger helper + pipeline integration
**UI hint**: yes

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1-5 | v1.0 | 10/10 | Complete | 2026-03-19 |
| 6 | v1.1 | 2/2 | Complete | 2026-03-20 |
| 7-10 | v1.2 | 8/8 | Complete | 2026-03-22 |
| 11-12 | v1.3 | 3/3 | Complete | 2026-03-23 |
| 13-18 | v2.0 | 14/14 | Complete | 2026-03-24 |
| 19-22 | v0.3.1 | 5/5 | Complete | 2026-04-03 |
| 23-25 | v0.4.0 | 4/4 | Complete | 2026-04-03 |
| 26-31 | v0.5.0 | 6/6 | Complete | 2026-04-09 |
| 32. Config Schema Extension | v0.6.0 | 2/2 | Complete    | 2026-04-09 |
| 33. SqlTable Column Pickers | v0.6.0 | 1/1 | Complete    | 2026-04-11 |
| 34. Embedded XML Screens | v0.6.0 | 0/0 | Not started | - |
| 35. Item Type Screens | v0.6.0 | 0/0 | Not started | - |
| 36. Area Screens | v0.6.0 | 0/0 | Not started | - |
| 37. Predicate UI Polish | v0.6.0 | 0/0 | Not started | - |

# Roadmap: DynamicWeb.Serializer

## Milestones

- [x] **v1.0 MVP** - Phases 1-5 (shipped 2026-03-20) - [Archive](milestones/v1.0-ROADMAP.md)
- [x] **v1.1 Robustness** - Phase 6 (shipped 2026-03-20) - [Archive](milestones/v1.1-ROADMAP.md)
- [x] **v1.2 Admin UI** - Phases 7-10 (shipped 2026-03-22)
- [x] **v1.3 Permissions** - Phases 11-12 (shipped 2026-03-23)
- [x] **v2.0 DynamicWeb.Serializer** - Phases 13-18 (shipped 2026-03-24)
- [x] **v0.3.1 Internal Link Resolution** - Phases 19-22 (shipped 2026-04-03)
- [x] **v0.4.0 Full Page Fidelity** - Phases 23-25 (shipped 2026-04-07)
- [ ] **v0.5.0 Granular Serialization Control** - Phases 26-31 (in progress)

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

### v0.5.0 Granular Serialization Control (In Progress)

**Milestone Goal:** Pretty-print embedded XML, consolidate areas into ContentProvider, and add field-level include/exclude filtering across all provider types with updated predicate UI.

- [x] **Phase 26: XML Pretty-Print for Content** - XmlFormatter utility + content pipeline XML pretty-printing with round-trip compaction (completed 2026-04-07)
- [ ] **Phase 27: XML Pretty-Print for SqlTable** - SQL table pipeline XML pretty-printing via config-driven xmlColumns
- [x] **Phase 28: Field-Level Filtering Core** - Predicate excludeFields/excludeXmlElements for content with deserialize skip guard (completed 2026-04-07)
- [ ] **Phase 29: SqlTable Field Filtering** - excludeFields support for SqlTable predicates
- [ ] **Phase 30: Area Property Consolidation** - Full area properties in ContentProvider with field-level blacklist
- [ ] **Phase 31: Predicate UI Enhancement** - Admin UI for excludeFields, xmlColumns, and excludeXmlElements configuration

## Phase Details

### Phase 26: XML Pretty-Print for Content
**Goal**: Embedded XML blobs in content YAML (moduleSettings, urlDataProviderParameters) become readable indented multi-line XML, and round-trip back to compact single-line on deserialize
**Depends on**: Phase 25 (current codebase)
**Requirements**: XML-01, XML-03
**Success Criteria** (what must be TRUE):
  1. Content YAML files containing moduleSettings or urlDataProviderParameters show indented multi-line XML using YAML literal block scalars instead of single-line escaped strings
  2. Deserializing pretty-printed content YAML compacts the XML back to single-line before writing to the database, producing byte-identical DB values to the original
  3. Content that contains no embedded XML is unaffected by the formatter (no regressions)
**Plans**: 1 plan
Plans:
- [x] 26-01-PLAN.md — XmlFormatter utility + content pipeline integration

### Phase 27: XML Pretty-Print for SqlTable
**Goal**: SQL table YAML files with XML columns show readable indented XML, controlled by a per-predicate xmlColumns config list
**Depends on**: Phase 26 (XmlFormatter utility)
**Requirements**: XML-02
**Success Criteria** (what must be TRUE):
  1. SqlTable predicates accept an xmlColumns list in config specifying which columns contain XML
  2. SQL table YAML files show indented multi-line XML for configured xmlColumns, using YAML literal block scalars
  3. Deserializing SQL table YAML with pretty-printed XML compacts it back to single-line before writing to DB (round-trip correctness via XML-03 compaction logic from Phase 26)
**Plans**: 1 plan
Plans:
- [ ] 27-01-PLAN.md — xmlColumns config + SqlTable pipeline XML pretty-print/compact

### Phase 28: Field-Level Filtering Core
**Goal**: Content predicates can exclude specific fields from serialization, strip specific XML elements from blobs, and excluded fields are safely skipped during deserialization (no null-out destruction)
**Depends on**: Phase 26 (XmlFormatter for XML element filtering)
**Requirements**: FILT-01, FILT-03, FILT-04
**Success Criteria** (what must be TRUE):
  1. A content predicate with excludeFields: [PageNavigationTag, AreaDomain] omits those fields from serialized YAML output
  2. Deserializing YAML that was serialized with excludeFields does NOT null out the excluded fields on the target DB (skip guard prevents source-wins destruction)
  3. A content predicate with excludeXmlElements: [sort, pagesize] strips those element names from embedded XML blobs before writing YAML
  4. Fields not in the exclude list continue to serialize and deserialize normally (no regression)
**Plans**: 1 plan
Plans:
- [x] 28-01-PLAN.md — excludeFields/excludeXmlElements config + serialize filtering + deserialize skip guard + XmlFormatter.RemoveElements

### Phase 29: SqlTable Field Filtering
**Goal**: SqlTable predicates can exclude specific columns from serialization with the same skip-guard protection on deserialize
**Depends on**: Phase 28 (filtering infrastructure)
**Requirements**: FILT-02
**Success Criteria** (what must be TRUE):
  1. A SqlTable predicate with excludeFields: [LastModified, MachineName] omits those columns from serialized YAML output
  2. Deserializing SQL table YAML with excluded fields does NOT null out or delete those column values on the target DB
**Plans**: 1 plan
Plans:
- [ ] 27-01-PLAN.md — xmlColumns config + SqlTable pipeline XML pretty-print/compact

### Phase 30: Area Property Consolidation
**Goal**: ContentProvider serializes and deserializes all 60+ Area columns (Domain, Layout, Culture, EcomSettings, SSL, CDN, etc.) in area.yml, with field-level blacklist for environment-specific values
**Depends on**: Phase 28 (excludeFields mechanism)
**Requirements**: AREA-03, AREA-04, AREA-05
**Success Criteria** (what must be TRUE):
  1. area.yml contains all 60+ Area columns (Domain, Layout, Culture, EcomSettings, SSL, CDN, etc.) alongside existing ItemType fields
  2. Deserializing area.yml restores all Area properties to the database, creating the area row if it does not exist on target
  3. A content predicate with excludeFields: [AreaDomain, AreaNoindex] omits those area-specific columns from area.yml and does not null them on deserialize
  4. Existing area ItemType field serialization (from Phase 24) continues working alongside the new area properties
**Plans**: 1 plan
Plans:
- [ ] 27-01-PLAN.md — xmlColumns config + SqlTable pipeline XML pretty-print/compact

### Phase 31: Predicate UI Enhancement
**Goal**: Admin UI predicate edit screens expose all new v0.5.0 config fields (excludeFields, xmlColumns, excludeXmlElements) for visual configuration
**Depends on**: Phase 28, Phase 29, Phase 30 (all config fields finalized)
**Requirements**: UI-01, UI-02, UI-03
**Success Criteria** (what must be TRUE):
  1. Predicate edit screen for content predicates shows an excludeFields input where users can add/remove field names to exclude
  2. Predicate edit screen for SqlTable predicates shows an xmlColumns input where users can specify which columns contain XML
  3. Predicate edit screen shows an excludeXmlElements input where users can add/remove XML element names to strip
  4. Changes made in the UI are persisted to the config file and take effect on next serialize/deserialize
**Plans**: 1 plan
Plans:
- [ ] 27-01-PLAN.md — xmlColumns config + SqlTable pipeline XML pretty-print/compact
**UI hint**: yes

## Progress

**Execution Order:** Phases 26 -> 27 -> 28 -> 29, 30 (parallel after 28) -> 31

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Foundation | v1.0 | 2/2 | Complete | 2026-03-19 |
| 2. Configuration | v1.0 | 1/1 | Complete | 2026-03-19 |
| 3. Serialization | v1.0 | 3/3 | Complete | 2026-03-19 |
| 4. Deserialization | v1.0 | 2/2 | Complete | 2026-03-19 |
| 5. Integration | v1.0 | 2/2 | Complete | 2026-03-19 |
| 6. Sync Robustness | v1.1 | 2/2 | Complete | 2026-03-20 |
| 7. Config Infrastructure | v1.2 | 2/2 | Complete | 2026-03-22 |
| 8. Settings Screen | v1.2 | 1/1 | Complete | 2026-03-22 |
| 9. Predicate Management | v1.2 | 2/2 | Complete | 2026-03-22 |
| 10. Context Menu Actions | v1.2 | 3/3 | Complete | 2026-03-22 |
| 11. Permission Serialization | v1.3 | 1/1 | Complete | 2026-03-22 |
| 12. Permission Deserialization + Docs | v1.3 | 2/2 | Complete | 2026-03-23 |
| 13. Provider Foundation + SqlTableProvider Proof | v2.0 | 3/3 | Complete | 2026-03-23 |
| 14. Content Migration + Orchestrator | v2.0 | 2/2 | Complete | 2026-03-24 |
| 15. Ecommerce Tables at Scale | v2.0 | 2/2 | Complete | 2026-03-24 |
| 16. Admin UX + Rename | v2.0 | 5/5 | Complete | 2026-03-24 |
| 17. Project Rename | v2.0 | N/A | Absorbed into P16 | - |
| 18. Predicate Config Multi-Provider | v2.0 | 2/2 | Complete | 2026-03-24 |
| 19. Source ID Serialization | v0.3.1 | 1/1 | Complete | 2026-04-03 |
| 20. Link Resolution Core | v0.3.1 | 2/2 | Complete | 2026-04-03 |
| 21. Paragraph Anchor Resolution | v0.3.1 | 1/1 | Complete | 2026-04-03 |
| 22. Version Housekeeping | v0.3.1 | 1/1 | Complete | 2026-04-03 |
| 23. Full Page Properties + Navigation Settings | v0.4.0 | 2/2 | Complete | 2026-04-03 |
| 24. Area ItemType Fields | v0.4.0 | 1/1 | Complete | 2026-04-03 |
| 25. Ecommerce Schema Sync | v0.4.0 | 1/1 | Complete | 2026-04-03 |
| 26. XML Pretty-Print for Content | v0.5.0 | 1/1 | Complete   | 2026-04-07 |
| 27. XML Pretty-Print for SqlTable | v0.5.0 | 0/? | Not started | - |
| 28. Field-Level Filtering Core | v0.5.0 | 1/1 | Complete   | 2026-04-07 |
| 29. SqlTable Field Filtering | v0.5.0 | 0/? | Not started | - |
| 30. Area Property Consolidation | v0.5.0 | 0/? | Not started | - |
| 31. Predicate UI Enhancement | v0.5.0 | 0/? | Not started | - |

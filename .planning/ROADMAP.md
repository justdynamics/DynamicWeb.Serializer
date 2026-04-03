# Roadmap: DynamicWeb.Serializer

## Milestones

- [x] **v1.0 MVP** - Phases 1-5 (shipped 2026-03-20) - [Archive](milestones/v1.0-ROADMAP.md)
- [x] **v1.1 Robustness** - Phase 6 (shipped 2026-03-20) - [Archive](milestones/v1.1-ROADMAP.md)
- [x] **v1.2 Admin UI** - Phases 7-10 (shipped 2026-03-22)
- [x] **v1.3 Permissions** - Phases 11-12 (shipped 2026-03-23)
- [x] **v2.0 DynamicWeb.Serializer** - Phases 13-18 (shipped 2026-03-24)
- [ ] **v0.3.1 Internal Link Resolution** - Phases 19-22 (in progress)

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

### v0.3.1 Internal Link Resolution (In Progress)

**Milestone Goal:** Resolve internal page ID references in content fields during deserialization so cross-environment links work correctly.

- [ ] **Phase 19: Source ID Serialization** - Serialize source numeric IDs into YAML so deserialization can build the ID mapping
- [ ] **Phase 20: Link Resolution Core** - Detect and rewrite `Default.aspx?ID=NNN` patterns in ItemType fields during deserialization
- [ ] **Phase 21: Paragraph Anchor Resolution** - Resolve paragraph ID fragments in `Default.aspx?ID=NNN#PPP` anchor links
- [ ] **Phase 22: Version Housekeeping** - Re-tag Git history from v1.0/v2.0 to 0.x pre-release versioning

## Phase Details

### Phase 19: Source ID Serialization
**Goal**: Serialized YAML includes the source environment's numeric page and paragraph IDs alongside GUIDs, enabling ID mapping construction at deserialization time
**Depends on**: Phase 18 (current codebase)
**Requirements**: SER-01, SER-02
**Success Criteria** (what must be TRUE):
  1. SerializedPage YAML files contain a SourcePageId field with the numeric page ID from the source environment
  2. SerializedParagraph YAML files contain a SourceParagraphId field with the numeric paragraph ID from the source environment
  3. Existing deserialization still works correctly with the new fields present (backward compatible)
  4. Re-serializing an already-synced environment produces YAML with that environment's own numeric IDs (not stale source IDs)
**Plans**: 1 plan
Plans:
- [ ] 19-01-PLAN.md -- Add SourcePageId/SourceParagraphId to DTOs, ContentMapper, and unit tests

### Phase 20: Link Resolution Core
**Goal**: Internal page links (`Default.aspx?ID=NNN`) embedded in ItemType field values are automatically rewritten to the correct target page IDs during deserialization
**Depends on**: Phase 19 (source IDs available in YAML)
**Requirements**: LINK-01, LINK-02, LINK-03, LINK-04
**Success Criteria** (what must be TRUE):
  1. After deserialization, `Default.aspx?ID=NNN` references in link fields point to the correct target page (verified by checking the target DB value matches the page with the same GUID)
  2. Rich text HTML fields containing `<a href="Default.aspx?ID=NNN">` have their embedded page IDs correctly rewritten
  3. Button field values containing `Default.aspx?ID=NNN` patterns have their page IDs correctly rewritten
  4. Links referencing pages that do not exist in the target environment are left unchanged and a warning is logged identifying the unresolvable source page ID
  5. ID replacement is boundary-aware: rewriting ID=1 does not corrupt ID=12 or ID=100
**Plans**: TBD

### Phase 21: Paragraph Anchor Resolution
**Goal**: Paragraph anchor links (`Default.aspx?ID=NNN#PPP`) have both their page ID and paragraph ID fragments resolved to target environment values
**Depends on**: Phase 20 (link resolver infrastructure), Phase 19 (SourceParagraphId in YAML)
**Requirements**: LINK-05
**Success Criteria** (what must be TRUE):
  1. After deserialization, `Default.aspx?ID=NNN#PPP` references have both the page ID and paragraph ID rewritten to target values
  2. Anchor links where the paragraph does not exist in target but the page does are resolved for the page ID portion and a warning is logged for the paragraph fragment
**Plans**: TBD

### Phase 22: Version Housekeeping
**Goal**: Git history uses consistent 0.x pre-release versioning instead of the legacy v1.0/v2.0 scheme
**Depends on**: Nothing (independent of other phases)
**Requirements**: VER-01
**Success Criteria** (what must be TRUE):
  1. Git tags follow a 0.x.y pattern (e.g., 0.1.0, 0.2.0, 0.3.0) replacing the previous v1.0/v2.0 tags
  2. Running `git tag` shows only 0.x tags (no leftover v1.x or v2.x tags)
**Plans**: TBD

## Progress

**Execution Order:** Phases 19 -> 20 -> 21; Phase 22 can run anytime (independent)

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
| 19. Source ID Serialization | v0.3.1 | 0/1 | In progress | - |
| 20. Link Resolution Core | v0.3.1 | 0/0 | Not started | - |
| 21. Paragraph Anchor Resolution | v0.3.1 | 0/0 | Not started | - |
| 22. Version Housekeeping | v0.3.1 | 0/0 | Not started | - |

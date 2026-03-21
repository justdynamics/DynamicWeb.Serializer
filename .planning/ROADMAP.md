# Roadmap: Dynamicweb.ContentSync

## Milestones

- [x] **v1.0 MVP** - Phases 1-5 (shipped 2026-03-20) - [Archive](milestones/v1.0-ROADMAP.md)
- [x] **v1.1 Robustness** - Phase 6 (shipped 2026-03-20) - [Archive](milestones/v1.1-ROADMAP.md)
- [ ] **v1.2 Admin UI** - Phases 7-10 (in progress)

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

### v1.2 Admin UI (In Progress)

**Milestone Goal:** Make ContentSync configurable from the DynamicWeb admin UI with query-based predicate management and ad-hoc serialize/deserialize via context menus.

- [ ] **Phase 7: Config Infrastructure + Settings Tree Node** - Concurrency-safe config read/write and nav node registration in DW admin tree
- [ ] **Phase 8: Settings Screen** - Admin UI for editing OutputDirectory, dry-run, logging level, and conflict strategy
- [ ] **Phase 9: Predicate Management** - CRUD list/edit screens for predicate configuration
- [ ] **Phase 10: Context Menu Actions** - Serialize-to-zip and deserialize-from-zip on content tree page nodes

## Phase Details

### Phase 7: Config Infrastructure + Settings Tree Node
**Goal**: Config file operations are concurrency-safe and the Sync node is visible in the DW admin navigation tree
**Depends on**: Phase 6 (v1.1 codebase)
**Requirements**: CFG-01, CFG-02, CFG-03, UI-01
**Success Criteria** (what must be TRUE):
  1. Two concurrent config writes (e.g., UI save during a scheduled task run) do not corrupt the config file
  2. A manual edit to ContentSync.config.json is reflected on the next admin UI screen load without restart
  3. Saving an invalid config value (e.g., empty OutputDirectory) shows a clear error message rather than silently writing bad config
  4. A "Sync" node appears under Settings > Content in the DW admin navigation tree and clicking it loads a screen (not a 404)
**Plans**: 2 plans

Plans:
- [x] 07-01-PLAN.md — Config infrastructure: ConfigWriter, ConfigPathResolver, and tests
- [x] 07-02-PLAN.md — Admin UI tree node registration and skeleton edit screen

### Phase 8: Settings Screen
**Goal**: Users can view and edit all ContentSync configuration options from the DW admin UI
**Depends on**: Phase 7
**Requirements**: UI-02, UI-03, UI-04, UI-05, UI-06
**Success Criteria** (what must be TRUE):
  1. User can view and change the OutputDirectory path from the settings screen
  2. User can toggle dry-run mode on/off from the settings screen
  3. User can select a logging level and a conflict strategy from dropdown controls on the settings screen
  4. Clicking Save persists all changes to ContentSync.config.json, and reloading the screen shows the saved values
**Plans**: 1 plan

Plans:
- [x] 08-01-PLAN.md — Config model expansion (DryRun, ConflictStrategy) + full settings screen with dropdowns and validation

### Phase 9: Predicate Management
**Goal**: Users can manage content sync predicates (add, view, edit, delete) from the DW admin UI
**Depends on**: Phase 8
**Requirements**: PRED-01, PRED-02, PRED-03, PRED-04, PRED-05, PRED-06
**Success Criteria** (what must be TRUE):
  1. A "Predicates" sub-node appears under the Sync node in admin navigation and opens a list screen
  2. User can view all configured predicates showing their name, path, and include/exclude status
  3. User can add a new predicate, edit an existing predicate, and delete a predicate from the list screen
  4. All predicate changes (add, edit, delete) persist to ContentSync.config.json and survive a screen reload
  5. Predicates added via the admin UI are respected by the next scheduled task serialization run
**Plans**: TBD

Plans:
- [ ] 09-01: TBD

### Phase 10: Context Menu Actions
**Goal**: Users can serialize page subtrees to downloadable zips and deserialize uploaded zips into the content tree via right-click context menu
**Depends on**: Phase 7 (uses config infrastructure; architecturally independent of Phases 8-9)
**Requirements**: ACT-01, ACT-02, ACT-03, ACT-04, ACT-05, ACT-06, ACT-07, ACT-08
**Success Criteria** (what must be TRUE):
  1. Right-clicking a page in the content tree shows both "Serialize" and "Deserialize" context menu actions
  2. Clicking Serialize produces a zip of the page subtree as YAML and triggers a browser download
  3. The serialize zip is also saved to a configurable location on disk
  4. Clicking Deserialize prompts for a zip upload, lets the user choose overwrite-node or import-as-subtree, and applies the content to the tree
  5. Context menu actions reuse the existing ContentSerializer/ContentDeserializer without duplicating serialization logic
**Plans**: TBD

Plans:
- [ ] 10-01: TBD
- [ ] 10-02: TBD

## Progress

**Execution Order:** Phases 7 -> 8 -> 9 -> 10

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Foundation | v1.0 | 2/2 | Complete | 2026-03-19 |
| 2. Configuration | v1.0 | 1/1 | Complete | 2026-03-19 |
| 3. Serialization | v1.0 | 3/3 | Complete | 2026-03-19 |
| 4. Deserialization | v1.0 | 2/2 | Complete | 2026-03-19 |
| 5. Integration | v1.0 | 2/2 | Complete | 2026-03-19 |
| 6. Sync Robustness | v1.1 | 2/2 | Complete | 2026-03-20 |
| 7. Config Infrastructure + Settings Tree Node | v1.2 | 0/2 | Not started | - |
| 8. Settings Screen | v1.2 | 0/1 | Not started | - |
| 9. Predicate Management | v1.2 | 0/TBD | Not started | - |
| 10. Context Menu Actions | v1.2 | 0/TBD | Not started | - |

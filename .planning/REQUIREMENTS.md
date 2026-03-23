# Requirements: Dynamicweb.ContentSync

**Defined:** 2026-03-21
**Core Value:** Developers can reliably move content between DynamicWeb environments through source control, with serialized YAML files as the single source of truth.

## v1.2 Requirements

Requirements for admin UI milestone. Each maps to roadmap phases.

### Config Infrastructure

- [x] **CFG-01**: Config file read/write is concurrency-safe (file locking prevents corruption from simultaneous UI and scheduled task access)
- [x] **CFG-02**: Admin UI reflects manual config file edits on next screen load (bidirectional sync)
- [x] **CFG-03**: Config file validation produces clear error messages for invalid values

### Admin UI Settings

- [x] **UI-01**: Sync node appears at Settings > Content > Sync in DW admin navigation tree
- [x] **UI-02**: User can view and edit OutputDirectory from the settings screen
- [x] **UI-03**: User can toggle dry-run mode from the settings screen
- [x] **UI-04**: User can configure logging level from the settings screen
- [x] **UI-05**: User can set conflict strategy from the settings screen
- [x] **UI-06**: Settings changes persist to ContentSync.config.json on save

### Predicate Management

- [x] **PRED-01**: Query sub-node appears under the Sync node in admin navigation
- [x] **PRED-02**: User can view a list of configured predicates (name, path, include/exclude)
- [x] **PRED-03**: User can add a new predicate with name, path, and include/exclude toggle
- [x] **PRED-04**: User can edit an existing predicate
- [x] **PRED-05**: User can delete a predicate
- [x] **PRED-06**: Predicate changes persist to ContentSync.config.json

### Context Menu Actions

- [ ] **ACT-01**: Serialize action appears in page context menu in the content tree
- [x] **ACT-02**: Serialize creates a zip of the page subtree at a temporary location (separate from main serialization tree)
- [x] **ACT-03**: Serialize zip is available for browser download
- [x] **ACT-04**: Serialize zip is also saved to a configurable location on disk
- [ ] **ACT-05**: Deserialize action appears in page context menu in the content tree
- [x] **ACT-06**: Deserialize prompts user to upload a zip file
- [x] **ACT-07**: Deserialize lets user choose: overwrite clicked node as parent, or import zip as subtree
- [x] **ACT-08**: Context menu actions reuse existing ContentSerializer/ContentDeserializer logic (no code duplication)

## Future Requirements

### Publishing

- **PUB-01**: App published to NuGet registry with dynamicweb-app-store tag
- **PUB-02**: Tested across multiple content trees beyond Customer Center

## v1.3 Requirements

Requirements for permissions milestone. Each maps to roadmap phases.

### Permission Serialization

- [x] **PERM-01**: Explicit page permissions are serialized to YAML (roles + user groups with permission levels)
- [x] **PERM-02**: Permission owner is stored by name for roles and by group name for user groups (not numeric IDs)
- [x] **PERM-03**: Pages without explicit permissions serialize no permission data (inheritance preserved by tree structure)

### Permission Deserialization

- [x] **PERM-04**: Role-based permissions (Anonymous, AuthenticatedFrontend, etc.) are restored on deserialize using role name
- [x] **PERM-05**: User group permissions are resolved by group name on the target environment
- [x] **PERM-06**: If a referenced user group does not exist on the target, Anonymous is set to None (deny) as a safety fallback
- [x] **PERM-07**: Deserialization logs all permission actions (applied, skipped, safety fallback triggered)

### Documentation

- [x] **PERM-08**: README documents permission handling behavior including the safety fallback for missing groups

## Out of Scope

| Feature | Reason |
|---------|--------|
| DW Lucene query UI reuse for predicates | Impedance mismatch — predicates are path-based, not index queries |
| Real-time change detection | v2 feature — Notifications API deferred |
| Media/file serialization | Content structure only |
| Incremental/partial sync | Full sync only |
| Database-backed config storage | Config file is source of truth by design |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CFG-01 | Phase 7 | Complete |
| CFG-02 | Phase 7 | Complete |
| CFG-03 | Phase 7 | Complete |
| UI-01 | Phase 7 | Complete |
| UI-02 | Phase 8 | Complete |
| UI-03 | Phase 8 | Complete |
| UI-04 | Phase 8 | Complete |
| UI-05 | Phase 8 | Complete |
| UI-06 | Phase 8 | Complete |
| PRED-01 | Phase 9 | Complete |
| PRED-02 | Phase 9 | Complete |
| PRED-03 | Phase 9 | Complete |
| PRED-04 | Phase 9 | Complete |
| PRED-05 | Phase 9 | Complete |
| PRED-06 | Phase 9 | Complete |
| ACT-01 | Phase 10 | Pending |
| ACT-02 | Phase 10 | Complete |
| ACT-03 | Phase 10 | Complete |
| ACT-04 | Phase 10 | Complete |
| ACT-05 | Phase 10 | Pending |
| ACT-06 | Phase 10 | Complete |
| ACT-07 | Phase 10 | Complete |
| ACT-08 | Phase 10 | Complete |
| PERM-01 | Phase 11 | Complete |
| PERM-02 | Phase 11 | Complete |
| PERM-03 | Phase 11 | Complete |
| PERM-04 | Phase 12 | Complete |
| PERM-05 | Phase 12 | Complete |
| PERM-06 | Phase 12 | Complete |
| PERM-07 | Phase 12 | Complete |
| PERM-08 | Phase 12 | Complete |

**Coverage:**
- v1.2 requirements: 23 total (all complete)
- v1.3 requirements: 8 total
- Mapped to phases: 8
- Unmapped: 0

---
*Requirements defined: 2026-03-21*
*Last updated: 2026-03-22 after v1.3 roadmap creation*

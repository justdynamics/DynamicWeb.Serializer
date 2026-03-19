# Requirements: Dynamicweb.ContentSync

**Defined:** 2026-03-19
**Core Value:** Developers can reliably move content between DynamicWeb environments through source control, with serialized YAML files as the single source of truth.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Serialization

- [x] **SER-01**: Serialize full content tree (Area > Page > Grid > Row > Paragraph) to YAML files
- [ ] **SER-02**: Mirror-tree file layout — folder structure reflects content hierarchy with .yml per item
- [ ] **SER-03**: Source-wins conflict resolution — serialized files always overwrite target DB on deserialize
- [ ] **SER-04**: Deterministic serialization order to prevent git noise from non-deterministic DB queries

### Deserialization

- [ ] **DES-01**: Deserialize YAML files back into DynamicWeb database
- [ ] **DES-02**: GUID-based identity — match on PageUniqueId, insert with new numeric ID if no match
- [ ] **DES-03**: Dependency-ordered writes — parent pages exist before children are inserted
- [ ] **DES-04**: Dry-run mode — report what would change without applying

### Configuration

- [ ] **CFG-01**: Standalone config file defining sync scope (not DW admin UI)
- [ ] **CFG-02**: Predicate rules — include/exclude content trees by path or page ID

### Operations

- [ ] **OPS-01**: Scheduled task for full serialization (DB to disk)
- [ ] **OPS-02**: Scheduled task for full deserialization (disk to DB)
- [ ] **OPS-03**: Structured logging — log new, updated, skipped items and errors

### Infrastructure

- [ ] **INF-01**: DynamicWeb AppStore app structure (.NET 8.0+, NuGet package)
- [ ] **INF-02**: YAML round-trip fidelity — handle tildes, CRLFs, HTML content without corruption
- [ ] **INF-03**: Windows long-path handling for deep content hierarchies

## v2 Requirements

### Configuration

- **CFG-03**: Multiple named configuration sets for independent content trees
- **CFG-04**: Field-level exclusions to filter noisy system fields

### Sync Operations

- **OPS-04**: Orphan handling — delete items in DB scope that aren't in YAML files
- **OPS-05**: Real-time change detection via DynamicWeb Notifications API
- **OPS-06**: Admin UI for configuration and sync status

## Out of Scope

| Feature | Reason |
|---------|--------|
| Media/file serialization | Binary files in git cause bloat; separate deployment concern |
| Merge/three-way conflict resolution | Exponentially complex; source-wins is sufficient for v1 |
| Bi-directional sync | Creates data loss with source-wins in both directions |
| Incremental/delta sync | Full sync is safe and predictable for v1 |
| NuGet publishing | Final stages only, not part of development scope |
| Rollback / version history | Git is the version history; DB backup is the rollback mechanism |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| SER-01 | Phase 1 | Complete |
| SER-02 | Phase 1 | Pending |
| SER-04 | Phase 1 | Pending |
| CFG-01 | Phase 2 | Pending |
| CFG-02 | Phase 2 | Pending |
| SER-03 | Phase 3 | Pending |
| INF-02 | Phase 3 | Pending |
| INF-03 | Phase 3 | Pending |
| DES-01 | Phase 4 | Pending |
| DES-02 | Phase 4 | Pending |
| DES-03 | Phase 4 | Pending |
| DES-04 | Phase 4 | Pending |
| OPS-01 | Phase 5 | Pending |
| OPS-02 | Phase 5 | Pending |
| OPS-03 | Phase 5 | Pending |
| INF-01 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 16 total
- Mapped to phases: 16
- Unmapped: 0

---
*Requirements defined: 2026-03-19*
*Last updated: 2026-03-19 after roadmap creation — all requirements mapped*

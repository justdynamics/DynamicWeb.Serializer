# Roadmap: Dynamicweb.ContentSync

## Milestones

- ✅ **v1.0 MVP** — Phases 1-5 (shipped 2026-03-20) — [Archive](milestones/v1.0-ROADMAP.md)
- 🚧 **v1.1 Robustness** — Phase 6 (in progress)

## Phases

<details>
<summary>✅ v1.0 MVP (Phases 1-5) — SHIPPED 2026-03-20</summary>

- [x] Phase 1: Foundation (2/2 plans) — completed 2026-03-19
- [x] Phase 2: Configuration (1/1 plans) — completed 2026-03-19
- [x] Phase 3: Serialization (3/3 plans) — completed 2026-03-19
- [x] Phase 4: Deserialization (2/2 plans) — completed 2026-03-19
- [x] Phase 5: Integration (2/2 plans) — completed 2026-03-19

</details>

### 🚧 v1.1 Robustness

- [ ] **Phase 6: Sync Robustness** - Multi-column paragraph attribution, dry-run PropertyFields diff, config validation, Area GUID documentation

## Phase Details

### Phase 6: Sync Robustness
**Goal**: Close all tech debt gaps from v1.0 audit — multi-column paragraphs round-trip correctly, dry-run shows complete field diffs, config validates early, and operator documentation is accurate
**Depends on**: Phase 5
**Requirements**: SER-01, DES-04, CFG-01
**Plans:** 2 plans
**Gap Closure**: Closes gaps from v1.0-MILESTONE-AUDIT.md
**Success Criteria** (what must be TRUE):
  1. Paragraphs in columns 2+ survive a serialize → deserialize round-trip with correct GridRowColumn attribution
  2. Dry-run mode reports PropertyFields changes (Icon, SubmenuType) in diff output alongside existing Fields diff
  3. ConfigLoader.Load() validates that OutputDirectory exists (or logs a clear warning) before deserialization begins
  4. SerializedArea.AreaId behavior is documented in code comments (informational only, not used for identity resolution)

Plans:
- [ ] 06-01-PLAN.md — Multi-column paragraph round-trip (SER-01)
- [ ] 06-02-PLAN.md — Dry-run PropertyFields diff, OutputDirectory validation, AreaId docs (DES-04, CFG-01)

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Foundation | v1.0 | 2/2 | Complete | 2026-03-19 |
| 2. Configuration | v1.0 | 1/1 | Complete | 2026-03-19 |
| 3. Serialization | v1.0 | 3/3 | Complete | 2026-03-19 |
| 4. Deserialization | v1.0 | 2/2 | Complete | 2026-03-19 |
| 5. Integration | v1.0 | 2/2 | Complete | 2026-03-19 |
| 6. Sync Robustness | v1.1 | 0/2 | Not started | - |

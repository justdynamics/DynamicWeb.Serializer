# Phase 43: Manifest-driven deserialize + per-entry reporting + command surface — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-08
**Phase:** 43-manifest-driven-deserialize-per-entry-reporting-command-surface
**Areas discussed:** Wave decomposition, `EntryStatus.Skipped` semantics, `DeserializeFromZipCommand` transitional handling, `ToPredicate(Entry)` test-helper shim lifecycle

---

## Wave decomposition — how to slice 10 requirements into plans

| Option | Description | Selected |
|--------|-------------|----------|
| 4-5 incremental plans (Recommended) | Wave 1 `EntryStatus`/`EntryOutcome` types (additive). Wave 2 orchestrator pivot. Wave 3 `ISerializationProvider.Deserialize(ManifestEntry)` + per-provider bodies. Wave 4 command surface + Layer A tests. Mirrors Phase 42's 4-wave shape; smallest diff per commit; rollback-friendly. | |
| 2-3 plans (medium grain) | Plan 1 types + orchestrator together. Plan 2 provider + command together. Plan 3 Layer A tests. Bigger commits; harder to bisect. | |
| Big-bang single plan | One atomic switchover. Highest risk, smallest paperwork burden. | ✓ |

**User's choice:** Big-bang single plan.
**Notes:** Trades commit-bisect granularity for paperwork minimisation. CONTEXT.md flags to the planner that even with one PLAN.md, internal tasks MUST commit atomically (~6-10 tasks) so bisect remains usable.

---

## `EntryStatus.Skipped` semantics — what counts as Skipped vs Succeeded vs Failed

| Option | Description | Selected |
|--------|-------------|----------|
| Tight: only providerFilter exclusion (Recommended) | Skipped = orchestrator never dispatched the entry. Files-don't-exist => Failed. Dry-run that would change => Succeeded. Seed-merge with all fields already set => Succeeded with Counts.Skipped:N. | ✓ |
| Medium: filter exclusion + missing files | Adds files-don't-exist => Skipped. | |
| Loose: any zero-write outcome | Filter + missing files + Seed no-changes + dry-run all => Skipped. Loses signal — Failed-because-files-missing gets hidden. | |

**User's choice:** Tight (only providerFilter exclusion).
**Notes:** Aligns REPORT-01's "today's silent-skip class becomes observable" framing — silent skip = orchestrator-level skip. Per-row skip counts inside a successful entry route to `Counts.Skipped`, not to the entry-level `Status`.

---

## `DeserializeFromZipCommand` transitional handling — how to break the `ConfigLoader.Load` call

| Option | Description | Selected |
|--------|-------------|----------|
| Minimal diff (Recommended) | Replace `ConfigLoader.Load` with a config-free path helper for `EnsureDirectories(systemDir)` only. Leave the direct `ContentDeserializer.Deserialize()` call alone for Phase 44. | ✓ |
| Pull convergence forward | Route zip through `SerializerOrchestrator.DeserializeAll` now (eats Phase 44 CONVERGE-02 scope). | |
| Mark `[Obsolete]`, remove ConfigLoader.Load only | Tag command for Phase 44 deletion; strip the config call. | |

**User's choice:** Minimal diff.
**Notes:** Preserves the planned 43/44 scope split. Phase 44 owns full zip-import convergence (CONVERGE-02). Phase 43 just removes the `ConfigLoader.Load` reference per DESER-04.

---

## `ToPredicate(Entry)` test-helper shim lifecycle — when does it land, when does it leave

| Option | Description | Selected |
|--------|-------------|----------|
| Lands early, removed at end of 43 (Recommended) | Shim ships with the orchestrator pivot wave so existing predicate-fixture integration tests keep passing. Layer A unit tests use entry fixtures directly. Shim deleted at end of 43. Phase 44 Layer B port migrates tests directly to entry fixtures (no shim safety net). | ✓ |
| Lands early, carried into Phase 44 | Same start; shim survives to ease Layer B port. Removed at end of Phase 44. | |

**User's choice:** Lands early, removed at end of 43.
**Notes:** Two-step migration. Internal helper, gated behind `InternalsVisibleTo` to the test assembly. Don't ship in production assembly.

---

## Claude's Discretion

- **Strict-mode WARNING surface (DESER-05):** planner picks shape — log line at start of every deserialize run is sufficient; admin-UI banner is overkill for a transitional warning.
- **Per-entry log line column layout (REPORT-05 / SC-5):** SC-5 example defines the format; planner picks exact column shape (timestamps + duration in tail, indented continuation for multi-line warnings).
- **Strict-mode resolver call site:** explicit `configValue: null` argument at every call site for grep-friendliness.

## Deferred Ideas

- Phase 38.1 open-with-gap (B.5.2, ITEM-01, Plan 04 script-08 revision) folded into Phase 44's E2E sweep.
- Future requirements DRIFT-01, OVERRIDE-01, DEPENDS-01, TIEBREAK-01, PROVENANCE-01, HAND-EDIT-01, MIGRATE-01, CARVE-EMBEDDED-XML — out of v0.6.0 scope per REQUIREMENTS.md.

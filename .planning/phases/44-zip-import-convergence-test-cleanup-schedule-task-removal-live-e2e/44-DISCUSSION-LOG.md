# Phase 44: Zip-import convergence + test cleanup + Obsolete deletion + REVIEW fold-in - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-11
**Phase:** 44-zip-import-convergence-test-cleanup-schedule-task-removal-live-e2e
**Areas discussed:** Zip-import convergence shape, Layer B test port + Obsolete deletion ordering, Live E2E preconditions + DAP pipeline, Phase 43 REVIEW.md fold-in scope

---

## Zip-import convergence shape

### Q1: How should DeserializeFromZipCommand feed its synthetic ContentEntry into the orchestrator?

| Option | Description | Selected |
|--------|-------------|----------|
| Public DeserializeAll(Manifest, ...) overload | Add public overload; disk-reading DeserializeAll becomes thin wrapper. Both paths share dispatch core. | ✓ |
| Write temp manifest.json on disk in ZipImport/ | Re-use existing disk reader via synthesised manifest file. Tight coupling to ManifestWriter atomic-write semantics. | |
| Expose DeserializeEntries(IReadOnlyList<ManifestEntry>, ...) publicly | Promote internal Layer-A test seam. Cements two public dispatch surfaces. | |

**User's choice:** Public DeserializeAll(Manifest, ...) overload
**Notes:** Matches CONVERGE-02 wording verbatim ("in-memory Manifest ... same orchestrator pipeline"). Single shared dispatch core.

### Q2: Where should the shared BuildContentEntryForArea helper live?

| Option | Description | Selected |
|--------|-------------|----------|
| Static method on ContentProvider | public static; no DI ceremony; lives next to BuildManifestEntry which refactors to call it. | ✓ |
| Static helper in Infrastructure/ContentEntryBuilder.cs | Free-standing; domain-neutral location; drifts away from BuildManifestEntry. | |
| Public instance method on ContentProvider | Forces zip-import to DI-instantiate provider for stateless projection. | |

**User's choice:** Static method on ContentProvider
**Notes:** Single canonical shape definition; BuildManifestEntry refactors to project predicate → (areaId, contentRoot) and call BuildContentEntryForArea internally.

### Q3: How should strict mode resolve for the zip-import path?

| Option | Description | Selected |
|--------|-------------|----------|
| StrictModeResolver consistent with main deserialize | Mirror D-38-11 / D-16; admin-UI OFF, API ON, ?strictMode= override. | ✓ |
| Always OFF (zip-import is one-shot import) | Pin to false; no override. Re-introduces silent-skip class. | |
| Always ON (one-shot import = highest scrutiny) | Pin to true; sharp edge for upgrades. | |

**User's choice:** StrictModeResolver consistent with main deserialize
**Notes:** Adds nullable bool? StrictMode + IsAdminUiInvocation flag. Same canonical plumbing across all three deserialize entry points.

### Q4: After CONVERGE-02, ContentDeserializer has no callers outside ContentProvider. Retirement scope?

| Option | Description | Selected |
|--------|-------------|----------|
| Pivot ContentDeserializer to ContentEntry-typed | Eliminates synthetic predicate at ContentProvider.Deserialize. Single shape end-to-end. | ✓ |
| Make ContentDeserializer internal but leave signature predicate-typed | Smallest diff; half-converged state persists. | |
| Leave fully public + predicate-typed | Status quo for inner machinery. | |

**User's choice:** Pivot ContentDeserializer to ContentEntry-typed
**Notes:** Phase 43 SUMMARY's flagged "Phase 44 candidate". Aligns with no-backcompat policy.

---

## Layer B test port + Obsolete deletion ordering

### Q1: How should the Layer B port + [Obsolete] deletion + reverse-shim deletion sequence in Phase 44?

| Option | Description | Selected |
|--------|-------------|----------|
| Port-then-delete (smooth ratchet, 1 commit per file) | 6 port commits → 1 deletion commit. Tests green at every boundary. | ✓ |
| Delete-then-fix (loud break, fewer commits) | Repeats Phase 43's transient-red pattern; bisect across range is unusable. | |
| All-at-once (single commit, biggest diff) | One commit; bisect granularity collapses. | |

**User's choice:** Port-then-delete (smooth ratchet, 1 commit per file)
**Notes:** 7 atomic commits total. Mirrors Phase 43's 9-commit atomic-task philosophy. Test build green at every commit boundary.

### Q2: SerializerOrchestratorTests.cs has 53 ProviderPredicateDefinition refs (Layer A residual). Disposition?

| Option | Description | Selected |
|--------|-------------|----------|
| Delete the legacy-overload-specific tests | Audit each test; delete if testing [Obsolete] overload behaviour; port if testing surviving orchestrator semantics. | ✓ |
| Port everything possible to entry fixtures | Mechanically convert all; some tests become circular. | |
| Leave Layer A untouched, only Layer B in scope | Self-contradicts CONVERGE-04. | |

**User's choice:** Delete the legacy-overload-specific tests
**Notes:** Audit deliverable in PLAN.md commit 7; estimate ~10-12 deleted, ~3-5 ported.

### Q3: ROADMAP SC-3 says delete "the two [Obsolete] overloads" but code has 3. Which to delete?

| Option | Description | Selected |
|--------|-------------|----------|
| Delete all three Obsolete overloads | Re-word SC-3 to "3 overloads"; zero [Obsolete] attributes post-phase. | ✓ |
| Delete the two predicate-typed DeserializeAll only | Half-converged; predicate-typed SerializeAll persists. | |
| Defer SerializeAll Obsolete deletion to v0.7.0 | One [Obsolete] left; risks slipping. | |

**User's choice:** Delete all three Obsolete overloads
**Notes:** Phase 43 SUMMARY's "two" wording was inaccurate. ROADMAP.md edited during discuss to reflect "three overloads".

### Q4: ROADMAP SC-2 names 7 files but 3 already grep zero. CONVERGE-05 schedule-task also greps zero. Verification approach?

| Option | Description | Selected |
|--------|-------------|----------|
| Plan ports 6 with debt + grep gates full 7-file SC-2 list + grep gates schedule-task absence | Port-tasks for shim users; assertion-only tasks for the rest. | ✓ |
| Audit each named file individually + write 'no work needed' verification log | Heavy ceremony for zero-result greps. | |
| Skip explicit verification — trust grep at phase end | Light-touch; no commit anchors the verification. | |

**User's choice:** Plan ports the 6 with debt + grep gates the full 7-file SC-2 list + grep gates schedule-task absence
**Notes:** CONVERGE-05 ratifies prior cleanup (commit a32703f) rather than doing new removal work.

---

## Live E2E preconditions + DAP pipeline

### Q1: For Swift 2.2 → CleanDB live E2E, how should we handle Phase 38.1 carry-forwards (B.5.2 + ITEM-01)?

| Option | Description | Selected |
|--------|-------------|----------|
| Re-run as-is, ship acknowledgedOrphanPageIds for the gap | Pragmatic close; B.5.2 + ITEM-01 stay as v0.7.0 backlog. | ✓ (superseded) |
| Fix B.5.2 + ITEM-01 first, then E2E | 200-400 LOC; inflates Phase 44 scope. | |
| Run as-is + log gaps + fail SC-4 if needed | Risks mid-phase scope explosion. | |

**User's choice:** Re-run as-is, ship acknowledgedOrphanPageIds for the gap (superseded by Q2 pivot below)

### Q2: For the DAP / pim.carriageservices live deploy (SC-5), what's the tooling shape?

| Option | Description | Selected |
|--------|-------------|----------|
| Manual run logged under .planning/phases/44-.../live-runs/ | No new ps1; documented procedure. | |
| Extend full-clean-roundtrip.ps1 with -Demo dap switch | Bloats Swift pipeline framework. | |
| Parallel dap-clean-roundtrip.ps1 | Duplicates ~30% boilerplate. | |
| **User pivot: forget about carriageservices, and skip e2e fully for now** | All E2E dropped from Phase 44 entirely. | ✓ |

**User's choice:** forget about carriageservices, and skip e2e fully for now
**Notes:** Significant scope pivot — drops entire live E2E concern from Phase 44.

### Q3: Skipping E2E means CONVERGE-06 + SC-4 + SC-5 fall out of Phase 44 scope. Where should CONVERGE-06 land?

| Option | Description | Selected |
|--------|-------------|----------|
| Defer CONVERGE-06 to a follow-up phase (44.1 or 45) | Milestone v0.6.0 stays open until live E2E ships. | |
| Defer CONVERGE-06 to v0.7.0 | Milestone ships without live E2E gate; risks regressions. | |
| Drop CONVERGE-06 entirely | Strike from REQUIREMENTS.md; rely on unit + integration suite. | ✓ |

**User's choice:** Drop CONVERGE-06 entirely
**Notes:** Milestone v0.6.0 ships without live E2E gate. tools/e2e/full-clean-roundtrip.ps1 stays in-repo for on-demand runs. REQUIREMENTS.md + ROADMAP.md edited during discuss to lock the contracted scope.

---

## Phase 43 REVIEW.md fold-in scope

### Q1: Phase 43 REVIEW.md had 4 warnings + 6 info findings. WR-01 auto-fixes via CONVERGE-04. Fold-in scope?

| Option | Description | Selected |
|--------|-------------|----------|
| Fold in all WR + structural IN findings | WR-02..04, IN-01..03, IN-06. Skip IN-04 (defensive nit), IN-05 (dies with shim). | ✓ |
| Fold only WR-01 auto-fix + IN-01 split-brain | Minimum needed for correctness; defers WR-02..04 + other IN-*. | |
| Skip all fold-in — keep Phase 44 scope tight to CONVERGE-01..05 | Cleanest scope boundary; risks forgotten findings. | |

**User's choice:** Fold in all WR + structural IN findings
**Notes:** Added as CONVERGE-07 in REQUIREMENTS.md during discuss.

### Q2: When IN-01 deletes OrchestratorResult.DeserializeResults, AdviceGenerator's per-table advice surface needs to migrate to EntryOutcomes. Migration shape?

| Option | Description | Selected |
|--------|-------------|----------|
| Migrate AdviceGenerator to EntryOutcomes (preserve advice contract) | Change input type to IReadOnlyList<EntryOutcome>; same Created/Updated/Skipped/Failed fields via ProviderCounts. | ✓ |
| Keep AdviceGenerator on a thin DeserializeResults projection | Re-introduces split-brain; self-contradicts IN-01. | |
| Delete AdviceGenerator's per-table advice surface | Larger behavioural change to admin-UI log viewer. | |

**User's choice:** Migrate AdviceGenerator to EntryOutcomes (preserve advice contract)
**Notes:** Public advice-text output stays semantically identical; only input source changes.

---

## Claude's Discretion

- In-memory `Manifest` construction details for zip-import (complete: true sentinel, schemaVersion, empty `ExcludeFieldsByItemType` / `ExcludeXmlElementsByType` envelopes per MANIFEST-05 precedence rule).
- Retention of `internal SerializerOrchestrator.DeserializeEntries(IReadOnlyList<ManifestEntry>, ...)` test seam (planner's call: delete if redundant with new public `DeserializeAll(Manifest, ...)` overload, keep if test ergonomics suffer).
- Whether to expose `acknowledgedOrphanPageIds` on `DeserializeFromZipCommand` or hardcode empty (today's zip-import has no orphan-acknowledgement surface; not changing that here).
- Commit message prefix convention `(44-01):` for atomic commits inside Plan 01.

## Deferred Ideas

- **Live E2E re-validation (was CONVERGE-06)** — dropped from v0.6.0 scope on 2026-05-11; on-demand only.
- **DAP / pim.carriageservices live deploy SC (was SC-5)** — dropped along with CONVERGE-06.
- **B.5.2 PropertyItem GUID sweep + 47 orphan page-IDs across 20 distinct IDs** — Phase 38.1 backlog; v0.7.0 candidate.
- **ITEM-01 ItemEditor field handling** — Phase 38.1 backlog; v0.7.0 candidate.
- **Per-entry advice surface** — D-10 preserves aggregate contract; per-entry advice via EntryOutcome.Errors + EntryOutcome.EntryId is a v0.7.0 candidate.
- **Defensive null-validation in SerializerPathResolver.EnsureDirectories (IN-04)** — re-promote only if a non-test caller surfaces a null path.

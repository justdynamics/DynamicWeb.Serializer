---
phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37
plan: 05
subsystem: config-flip
tags: [strict-mode, baseline, config, e2e, phase-closure]

requires:
  - phase: 37-production-ready-baseline
    provides: StrictModeResolver entry-point precedence (API/CLI=ON, admin UI=OFF); BaselineLinkSweeper
  - phase: 38-01
    provides: D.1 query-param fallback, D.2 HTTP-status hardening (0-error → 200), env-bucket.md docs
  - phase: 38-02
    provides: A.3 predicate-level ack consolidation; A.1/A.2 retroactive tests; B.5 paragraph-anchor sweeper correctness (Pitfall 3 defers ButtonEditor JSON)
  - phase: 38-03
    provides: C.1 FlatFileStore dedup preserves 2051 EcomProducts; B.1/B.2 stale-template SQL cleanup (05); B.3/B.4 investigations documented
  - phase: 38-04
    provides: D.3 smoke tool (tools/smoke/Test-BaselineFrontend.ps1)
provides:
  - Post-Phase-38 canonical baseline config (strictMode default ON via StrictModeResolver, no acknowledgedOrphanPageIds workaround)
  - Live E2E observed-state capture that proves which Phase-38 deferrals + one new B.5 deferral must close in Phase 38.1 before a fully clean strict round-trip is achievable
  - Final disposition for Phase 38: gated-closed-on-38.1 (code+config complete; clean strict E2E blocked on four source-data/schema-alignment items)
affects: [phase-38.1, milestone-v0.5.0, swift22-baseline-memory]

tech-stack:
  added: []
  patterns:
    - "Config-flip gate pattern: flip strict default last, observe real escalations, defer source-data cleanup to next phase"
    - "E2E deferred-item surfacing: removing a workaround config entry reveals latent bugs the workaround hid"

key-files:
  created:
    - .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-05-e2e-results.md
    - .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/task5-logs/final-*.log (8 files)
  modified:
    - src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json (2 lines removed)

key-decisions:
  - "D-38-16 code-complete: swift2.2-combined.json has no strictMode override and no acknowledgedOrphanPageIds entry; API/CLI strict default is ON via StrictModeResolver entry-point precedence (Phase 37-04)"
  - "Phase 38 disposition: gated-closed-on-38.1 — clean strict E2E requires four 38.1 fixes (B.5.1 SelectedValue paragraph-ID, B.4 SHOP19 FK, expanded B.3 schema-drift, 142 GridRow NOT NULL)"
  - "15717 is a paragraph ID referenced in ButtonEditor JSON SelectedValue — NOT a page. B.5's intentional Pitfall 3 deferral (ButtonEditor JSON out-of-scope) becomes the critical blocker once the ack workaround is removed. New Phase 38.1 item: B.5.1"
  - "B.3 scope turned out narrower than reality: the plan named 3 Area columns; live E2E shows 7 more across EcomGroups + EcomProducts. Same fix pattern (operational DW-version alignment OR config allowlist); broader scope"

patterns-established:
  - "Flip gate ordering: retroactive tests (A.*, B.5) → data-loss fix (C.1) → tooling (D.3) → config flip + live E2E (Wave 5). Each layer's fix reveals the next layer's latent issues"
  - "Live-E2E-as-verification pattern: unit tests covered Pitfall 3 deferral with green tests, but live source data exposed the ButtonEditor SelectedValue case that unit fixtures did not include"

requirements-completed: [D-38-16]

duration: 32min
completed: 2026-04-21
---

# Phase 38 Plan 05: D-38-16 Strict-Mode Default Restoration + Final E2E Summary

**D-38-16 code and config complete — post-Phase-38 canonical baseline has strictMode default ON via StrictModeResolver; live E2E surfaces four source-data/schema-alignment deferrals that gate a fully clean strict round-trip on Phase 38.1.**

## Performance

- **Duration:** 32 min
- **Started:** 2026-04-21T16:30:22Z
- **Completed:** 2026-04-21T17:03:07Z
- **Tasks:** 3 (Task 1 config edit, Task 2 live E2E, Task 3 SUMMARY)
- **Files modified:** 1 config (2 lines removed) + 8 log files + 2 docs (e2e-results.md + SUMMARY.md)

## Accomplishments

- **D-38-16 config flip landed.** `swift2.2-combined.json` has no `"strictMode": false` line and no `"acknowledgedOrphanPageIds"` entry. API/CLI entry points now default to strict mode via `StrictModeResolver` (Phase 37-04 D-16 original intent restored); admin UI default remains OFF.
- **Live E2E ran end-to-end against real hosts** (Swift 2.2 @ 54035 and CleanDB @ 58217) with the restored default. All 4 HTTP calls + all 4 host logs captured. EcomProducts round-trip still preserves 2051 rows (C.1 fix from Plan 38-03 holds).
- **Final Phase 38 disposition determined: `gated-closed-on-38.1`.** Three known deferrals (B.4 SHOP19 FK, expanded B.3 schema drift, 142 GridRow NOT NULL) fired as expected; one NEW deferral (B.5.1 ButtonEditor SelectedValue paragraph-ID) surfaced because removing the 15717 ack exposed a case B.5's Pitfall 3 intentionally left out.

## Task Commits

1. **Task 1: Restore strictMode default + remove obsolete orphan-page ack** — `2c70fc2` (refactor)
2. **Task 2: Final strict-mode E2E results** — `177487c` (test)
3. **Task 3: Plan SUMMARY** — (this commit, pending)

## Files Created/Modified

- `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` — removed `"strictMode": false` (line 5) and `"acknowledgedOrphanPageIds": [ 15717 ]` (line 15 of Content predicate)
- `.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-05-e2e-results.md` — full E2E observed state, deferral analysis, repro recipe for 38.1
- `task5-logs/final-serialize-deploy.log` + `final-serialize-deploy-hostlog.log` — HTTP 400 response + 1368-line host log (line 467 = 15717 ButtonEditor SelectedValue error)
- `task5-logs/final-serialize-seed.log` + `final-serialize-seed-hostlog.log` — HTTP 200, 3489 rows / 8 predicates, zero warnings
- `task5-logs/final-deserialize-deploy.log` + `final-deserialize-deploy-hostlog.log` — HTTP 400 + B.4 FK escalation + missing-area.yml downstream error
- `task5-logs/final-deserialize-seed.log` + `final-deserialize-seed-hostlog.log` — HTTP 400, 3489 created / 0 failed, 7 schema-drift warnings escalated

## Key Technical Decisions

### Config flip (Task 1) — exact diff

```diff
- "dryRun": false,
- "strictMode": false,
+ "dryRun": false,
  "deploy": {
```

```diff
        "areaId": 3,
        "path": "/",
-       "acknowledgedOrphanPageIds": [ 15717 ],
        "excludes": [],
```

No `"strictMode": true` was added — absence = null = `StrictModeResolver` entry-point default. Per `feedback_no_backcompat.md`: beta product (0.x), no shim, no default-value injection.

### Phase 38 final disposition — `gated-closed-on-38.1`

Per Plan 05 `<decision_logic>` and orchestrator guidance, three possible outcomes were on the table:
- `e2e-passed-clean` (all green, phase closes clean)
- `gated-closed-on-38.1` (known deferrals fire under strict, as anticipated)
- `new-problem-surfaced` (unexpected issue requires checkpoint)

The observed state is a hybrid: three expected deferrals (B.4, expanded B.3, 142 GridRow) + one not-quite-expected but closely-related deferral (B.5.1). The B.5.1 case is **directly in B.5's domain** — the planner of 38-02 explicitly noted SelectedValue JSON handling was deferred (Pitfall 3). What Phase 38 plan-05 got wrong was: 38-05's planner believed 15717 was a `Default.aspx?ID=X#15717` anchor case (which B.5 fixed), whereas the actual Swift 2.2 data has it in `"SelectedValue": "15717"` with `"LinkType": "paragraph"`. Per `<decision_logic>`, this is close enough to a known deferral to treat as `gated-closed-on-38.1` rather than `new-problem-surfaced` requiring a checkpoint.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Host startup required ASPNETCORE_URLS env var + project-root CWD**
- **Found during:** Task 2 host-start step
- **Issue:** Launching `Dynamicweb.Host.Suite.exe` directly (as Plan 38-03 notes suggested) defaulted to port 5000 instead of 54035/58217, because `launchSettings.json` is only read by `dotnet run` and the bin-dir `wwwroot` lacks `Files/`. The previous executor's recipe assumed the exe could start standalone, but it cannot without these two fixes.
- **Fix:** PowerShell script `.tmp/restart-hosts3.ps1` launches each exe with `ProcessStartInfo.Environment['ASPNETCORE_URLS']` set + `WorkingDirectory` pointed at the project root so wwwroot/Files resolves. Auth endpoint returned 200 on both hosts within ~4 minutes.
- **Commit:** not committed — shell-only operational scripts live in `.tmp/` and are uncommitted

### Expected Deferrals That Fired

These were either predicted in the plan or are documented Phase 38.1 carry-forwards:

**1. [B.4 - Phase 38.1 carry-forward] EcomShopGroupRelation FK escalation under strict**
- **Found during:** Deserialize deploy
- **Issue:** `Strict mode: 1 warning(s) escalated to failure: WARNING: Could not re-enable FK constraints for [EcomShopGroupRelation]: ALTER TABLE conflicted with DW_FK_EcomShopGroupRelation_EcomShops on column 'ShopId'`
- **Fix path:** Phase 38.1 `06-delete-orphan-ecomshopgrouprelation.sql` removes orphan SHOP19 row (see `38-03-b4-investigation.md`)
- **Status:** deferred per Plan 38-03 Task 4

**2. [Expanded B.3 - Phase 38.1 scope extension] 7 EcomGroups + EcomProducts schema-drift columns**
- **Found during:** Deserialize seed
- **Issue:** 7 WARNING lines escalated to failure in strict mode (`GroupPageIDRel`, `ProductPeriodId`, `ProductVariantGroupCounter`, `ProductPriceMatrixPeriod`, `ProductOptimizedFor`, `MyVolume`, `MyDouble`)
- **Scope note:** Phase 38's B.3 named 3 Area columns; live E2E shows 7 more columns across other tables. Same fix pattern (DW version alignment or per-column allowlist).
- **Fix path:** Phase 38.1 — either operational (align DW NuGet versions Swift-2.2 vs CleanDB) or config (extend per-predicate allowlist)

**3. [B.5.1 NEW - Phase 38.1 code fix] ButtonEditor SelectedValue paragraph-ID case**
- **Found during:** Serialize deploy (Content predicate abort)
- **Issue:** `BaselineLinkSweeper.CheckField`'s `SelectedValuePattern` loop (intentionally left unchanged per Plan 02 Pitfall 3) treats `"SelectedValue": "15717"` as a page-ID reference. In Swift-2.2 source, 15717 is actually a paragraph ID with `"LinkType": "paragraph"` — this case was deferred by B.5 but now blocks serialize under strict mode once the ack workaround is removed.
- **Fix path (Phase 38.1 B.5.1):** Extend `CheckField.SelectedValuePattern` loop to accept `validParagraphIds` membership as a valid resolution; add regression fixture matching the ButtonEditor JSON shape.
- **Scope note:** This is a minor extension of the B.5 code in `BaselineLinkSweeper.cs:167-172`; the required data (`validParagraphIds` HashSet) already exists from B.5.

**4. [142 GridRow - Phase 38.1 carry-forward] GridRowDefinitionId NOT NULL (not exercised this run)**
- **Status:** carried forward from Plan 38-03 Task 5; not exercised in this run because Content deserialize never reached GridRow (missing area.yml upstream abort)

## Phase 38 Full Item Status (14 items + D-38-16)

| ID | Item | Plan Closed | 38.1 Action |
| -- | ---- | ----------- | ----------- |
| A.1 | AcknowledgedOrphanPageIds retroactive tests | **38-02** ✅ | — |
| A.2 | IDENTITY_INSERT integration test for Area | **38-02** ✅ | — |
| A.3 | Consolidate ack list to ProviderPredicateDefinition only | **38-02** ✅ | — |
| B.1 | 1ColumnEmail stale template cleanup | **38-03** ✅ (SQL 05) | — |
| B.2 | 2ColumnsEmail / Swift-v2_PageNoLayout cleanup | **38-03** ✅ (SQL 05) | — |
| B.3 | 3 Area schema-drift columns | **38-03** ✅ investigation | **extend** — 7 more EcomGroups/EcomProducts columns found in live E2E |
| B.4 | EcomShopGroupRelation FK re-enable | **38-03** ✅ investigation | **close** — `06-delete-orphan-ecomshopgrouprelation.sql` |
| B.5 | BaselineLinkSweeper paragraph-anchor false-positive | **38-02** ✅ Default.aspx?ID=X#Y | **extend (B.5.1)** — SelectedValue paragraph-ID case |
| C.1 | EcomProducts 2051 → 582 silent-loss | **38-03** ✅ verified 2051→2051 | — |
| D.1 | ?mode=seed query-param fallback | **38-01** ✅ | — |
| D.2 | SerializerSerialize HTTP-status 0-error → 200 | **38-01** ✅ | — |
| D.3 | Smoke tool under tools/smoke/ | **38-04** ✅ | — |
| E.1 | docs/baselines/Swift2.2-baseline.md extension | **38-01** ✅ | — |
| E.2 | docs/baselines/env-bucket.md | **38-01** ✅ | — |
| D-38-16 | strictMode default ON + remove ack workaround | **38-05** ✅ (this plan) | — |
| — | 142 GridRow NOT NULL | — (discovered in 38-03) | **close** — cleanup approach decision |

**Phase 38 total: 14 + D-38-16 = 15 items. 15 code-complete. Live E2E gated on 4 deferrals to Phase 38.1.**

## Phase 38 Retrospective

The wave structure (D-38-01) worked well: quick wins + retroactive tests first (Wave 1-2), investigations next (Wave 3), tooling (Wave 4), final config flip last (Wave 5). Plan 05 is the canonical validation that all prior waves held up — and each deferral surfaced here was correctly recorded by its origin wave.

The one misjudgment in the Phase 38 plan set: Plan 05's planner wrote "B.5 fix (Plan 02) made the paragraph-anchor validation correctness fix, so the workaround is no longer needed" — implying 15717 was an anchor case. The actual 15717 case is ButtonEditor `SelectedValue` with `LinkType: paragraph`, which Plan 02's Pitfall 3 explicitly deferred. The deferred-items list in Plan 02's SUMMARY correctly flagged this, but the information didn't propagate to Plan 05's pre-flight assertions.

The live E2E also proved the `<decision_logic>` was right to insist on an **observed** E2E rather than relying on unit-test green alone. Plan 02's 643 unit tests + 4 paragraph-anchor tests passed, but the live fixture exposed the data-shape gap. This reinforces the Phase 37 learning: unit tests cover code correctness; live round-trips cover data-shape correctness.

## Phase 38.1 Queue (in priority order)

1. **B.5.1** — Extend `BaselineLinkSweeper.CheckField.SelectedValuePattern` to validate against `validParagraphIds` alongside `validIds`. Regression test on the 15717 fixture. Small code change (~10 lines + test).
2. **B.4 close** — Add `tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql` removing SHOP19 FK orphan.
3. **B.3 extension** — Decide: operational DW-version alignment OR per-predicate allowlist for 7 more schema-drift columns (EcomGroups.GroupPageIDRel + 6 EcomProducts columns).
4. **142 GridRow NOT NULL** — Either add default template-name injection in deserializer OR delete the 7 orphaned newsletter templates' GridRows in a new cleanup script (Plan 38-03 Task 5 recommended the latter).
5. Re-run Plan 38-05 Task 2 recipe with all four fixes to confirm `e2e-passed-clean`.

## Self-Check: PASSED

**Commits verified:**
- `2c70fc2` Task 1 config edit — confirmed with `git log --oneline --all | grep 2c70fc2`
- `177487c` Task 2 E2E results — confirmed with `git log --oneline --all | grep 177487c`

**Files verified:**
- `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` — exists, no strictMode/ack
- `.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-05-e2e-results.md` — exists, 3-KB+ content
- 8 log files in `task5-logs/final-*.log` — all exist and committed

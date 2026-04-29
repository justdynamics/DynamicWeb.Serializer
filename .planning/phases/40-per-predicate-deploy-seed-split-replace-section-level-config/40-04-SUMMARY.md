---
phase: 40-per-predicate-deploy-seed-split-replace-section-level-config
plan: 04
subsystem: configuration
tags: [json, baseline, docs, per-predicate-mode, phase-40-flat-shape]

# Dependency graph
requires:
  - phase: 40-per-predicate-deploy-seed-split-replace-section-level-config
    provides: "Phase 40 Plan 01 — flat-shape ConfigLoader + ProviderPredicateDefinition.Mode + legacy-rejection error strings"
provides:
  - "swift2.2-combined.json (canonical baseline) rewritten in Phase 40 flat shape — 17 Deploy + 9 Seed = 26 predicates"
  - "demo-sync.json, ecommerce-predicates-example.json, full-sync-example.json rewritten in flat shape with explicit mode field on every predicate"
  - "Swift22BaselineRoundTripTests asserting baseline parses + mode counts (17/9/26) + EcomShops=Deploy + EcomGroups=Seed"
  - "ExampleConfigsLoadTests asserting all three example JSONs parse + every predicate Mode==Deploy"
  - "docs/baselines/Swift2.2-baseline.md updated with Phase 40 paragraph + Per-predicate mode subsection"
  - "docs/configuration.md updated with flat-shape top-level schema + per-predicate-mode section + revised admin-UI table"
affects:
  - "Phase 40 Plan 05 (Wave 3 verification — final phase gate consumes the rewritten artefacts)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Phase 40 flat baseline shape: top-level outputDirectory/logLevel/dryRun/deployOutputSubfolder/seedOutputSubfolder/excludeXmlElementsByType/predicates[] — no nested deploy/seed/conflictStrategy keys"
    - "Per-predicate mode tagging: every predicate carries 'mode': 'Deploy' as second key after 'name' (or 'Seed' for the 9 seed predicates in the canonical baseline)"
    - "Mechanical sed-style rewrite for the 145-predicate full-sync-example.json — Python re.sub on a uniform 6-space-indented predicate-block pattern; safer than line-by-line manual edit at that volume"

key-files:
  created:
    - "tests/DynamicWeb.Serializer.Tests/Configuration/Swift22BaselineRoundTripTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Configuration/ExampleConfigsLoadTests.cs"
    - ".planning/phases/40-per-predicate-deploy-seed-split-replace-section-level-config/deferred-items.md"
  modified:
    - "src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json (rewritten flat — 297 → 297-ish lines, structural reshape)"
    - "src/DynamicWeb.Serializer/Configuration/demo-sync.json (added mode to 19 predicates, dropped conflictStrategy)"
    - "src/DynamicWeb.Serializer/Configuration/ecommerce-predicates-example.json (added mode to 28 predicates, appended _comment guidance entry)"
    - "src/DynamicWeb.Serializer/Configuration/full-sync-example.json (added mode to 145 predicates, dropped conflictStrategy, preserved trailing keys)"
    - "docs/baselines/Swift2.2-baseline.md (added Phase 40 update paragraph + Per-predicate mode subsection)"
    - "docs/configuration.md (rewrote top-level-schema, per-predicate-mode, global-exclusions, full-config-example, and admin-UI sections for flat shape)"

key-decisions:
  - "Multi-line indented JSON formatting preserved across all four config files (no compact one-liners). Reasoning: existing files used multi-line; ConfigLoader/ConfigWriter pass through identical regardless of formatting; consistency aids diff readability across the four files. Plan 04 Task 1 step 2 explicitly flagged the choice as either-acceptable; multi-line wins on aesthetics."
  - "All three example JSONs (demo-sync, ecommerce-predicates-example, full-sync-example) tagged Deploy globally rather than mixed Deploy/Seed. Reasoning: each example documents a static-reference-data deployment scenario — Seed is the field-level merge case (Phase 39) and is inappropriate as a kitchen-sink default. Users adapt to Seed by editing the mode value on the specific predicate(s) where field-level merge is wanted; the appended _comment guidance entry in ecommerce-predicates-example.json calls this out explicitly."
  - "Tests use the identifierValidator: null overload (per Plan 04 Task 1 checker Warning #7 option (b)). Reasoning: the round-trip test scope is JSON-shape parse + Mode resolution, NOT SqlIdentifierValidator. Extending the test fixture allowlist with the 23 baseline SqlTable identifiers would be disproportionate to value; the live-host smoke check in Task 4 (the human-verify checkpoint) covers identifier-validation end-to-end."
  - "Out-of-scope discoveries (legacy shape references in docs/getting-started.md, docs/strict-mode.md, docs/troubleshooting.md) logged to deferred-items.md per execute-plan.md SCOPE BOUNDARY. NOT auto-fixed because Plan 04's files_modified list does not include them; suggest a Phase 41 sweep."
  - "Task 4 (live host human-verify checkpoint) deferred to orchestrator surface per parallel-execution instructions: 'EMIT a structured checkpoint return rather than blocking — the orchestrator will surface it to the user.' The checkpoint is documented in the 'Checkpoint Disposition' section below; Tasks 1-3 are committed and verified; Task 4 awaits the operator on an integrated CleanDB/Swift-2.2 host that does not exist inside the parallel worktree."

requirements-completed: []

# Metrics
duration: ~9min
completed: 2026-04-29
---

# Phase 40 Plan 04: Baseline + example configs + docs rewritten for flat shape Summary

**swift2.2-combined.json (the canonical Phase 37/38/39 baseline) and the three user-facing example/doc JSONs are now in the Phase 40 flat shape; two reasoning docs describe the new shape with copy-paste-ready examples.**

## Performance

- **Duration:** ~9 min (2026-04-29T11:00:17Z → 2026-04-29T~11:09Z, all autonomous tasks)
- **Tasks:** 3 auto + 1 checkpoint:human-verify (deferred to orchestrator)
- **Files modified:** 6 (4 JSON + 2 markdown)
- **Files created:** 3 (2 test classes + 1 deferred-items log)
- **Files deleted:** 0

## Accomplishments

- **swift2.2-combined.json — canonical baseline rewritten flat.** 17 Deploy predicates + 9 Seed predicates = 26 total. excludeXmlElementsByType hoisted to top level (D-04). outputSubfolder values hoisted to top-level deployOutputSubfolder/seedOutputSubfolder (D-02). conflictStrategy fields dropped (D-02 hardcodes them per mode). Indented multi-line formatting preserved. EcomShops resolves Deploy; EcomGroups resolves Seed (gating sentinels for the assertion test).

- **Three example/doc JSONs — copy-paste safety preserved.** demo-sync.json (19 predicates), ecommerce-predicates-example.json (28 predicates), full-sync-example.json (145 predicates). Every predicate now declares `"mode": "Deploy"`. Top-level conflictStrategy removed from demo-sync and full-sync (ecommerce-predicates-example never had one). Per checker Warning #5: a user copying any of these into a fresh ConfigLoader.Load call now gets a parsed config instead of the Phase-40-rejection error.

- **Two new test classes wired.** Swift22BaselineRoundTripTests (5 facts: count parity, dictionary key count, EcomShops mode, EcomGroups mode, top-level subfolder keys). ExampleConfigsLoadTests (3 facts: one per example JSON; asserts non-empty predicates + Assert.All on Mode==Deploy). Both classes use ConfigLoader.Load(path, identifierValidator: null) — out-of-scope for the SqlIdentifierValidator pipeline, in-scope for JSON-shape parse + per-predicate Mode.

- **Two reasoning docs updated.** Swift2.2-baseline.md gained a Phase 40 paragraph plus a Per-predicate mode subsection (Deploy = source-wins, Seed = field-level merge per Phase 39, both set in admin UI). configuration.md rewrote five sections: top-level config schema, per-predicate mode (replaces "Deploy and Seed mode configs"), global exclusion maps, full config example, admin UI screens table.

- **deferred-items.md logged for Phase 41 cleanup.** Three out-of-scope doc files (getting-started.md, strict-mode.md, troubleshooting.md) carry legacy `deploy: { ... }` references that fall outside Plan 04's files_modified list. Mechanical pattern matches the rewrites already shipped here; suggest a Phase 41 sweep.

## Task Commits

Each task committed atomically:

1. **Task 1: Rewrite swift2.2-combined.json into Phase 40 flat shape** — `61f0129` (feat)
2. **Task 2: Rewrite the three example/documentation JSONs** — `9311c50` (feat)
3. **Task 3: Update Swift2.2-baseline.md and configuration.md** — `78a3d13` (docs)
4. **Task 4: Live host human-verify checkpoint** — DEFERRED to orchestrator (see "Checkpoint Disposition" below)

## Files Created/Modified

### Created (3)

- `tests/DynamicWeb.Serializer.Tests/Configuration/Swift22BaselineRoundTripTests.cs` — 5 facts. Uses identifierValidator: null overload. Does NOT inherit ConfigLoaderValidatorFixtureBase (no static state to manage). Class-level XML doc comment documents the scope decision (Plan 04 Task 1 checker Warning #7 option (b)).
- `tests/DynamicWeb.Serializer.Tests/Configuration/ExampleConfigsLoadTests.cs` — 3 facts (one per example JSON). Same scope decision and inheritance choice as the baseline test class.
- `.planning/phases/40-per-predicate-deploy-seed-split-replace-section-level-config/deferred-items.md` — out-of-scope doc-cleanup discoveries.

### Modified (6)

- `src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json` — flat shape with 26 predicates. 17 Deploy first (Content + 16 SqlTable) then 9 Seed (Content + 8 SqlTable). Top-level keys: outputDirectory, logLevel, dryRun, deployOutputSubfolder, seedOutputSubfolder, excludeXmlElementsByType (16 entries hoisted), predicates.
- `src/DynamicWeb.Serializer/Configuration/demo-sync.json` — 19 predicates, all `"mode": "Deploy"`. Removed top-level conflictStrategy.
- `src/DynamicWeb.Serializer/Configuration/ecommerce-predicates-example.json` — 28 predicates, all `"mode": "Deploy"`. Appended a _comment array entry calling out that mode is required and Seed is the alternative.
- `src/DynamicWeb.Serializer/Configuration/full-sync-example.json` — 145 predicates, all `"mode": "Deploy"`. Removed top-level conflictStrategy. Preserved trailing serializeRoot/uploadDir/downloadDir/logDir top-level keys (lines unchanged).
- `docs/baselines/Swift2.2-baseline.md` — added Phase 40 update paragraph + Per-predicate mode subsection (29 net lines added).
- `docs/configuration.md` — rewrote 5 sections (137 inserts / 91 deletions). TOC updated. Top-level schema example now flat. Per-predicate-mode section replaces Deploy-and-Seed-mode-configs section. Global exclusions example flat. Full config example abbreviated to flat shape. Admin UI table reflects mode-as-field rather than per-mode subnodes.

## Decisions Made

- **Multi-line formatting preserved.** All four JSON files retain indented multi-line structure (no compact one-line predicates). Plan 04 Task 1 step 2 left this as either/acceptable; consistency across files + diff readability won the choice.

- **All three example JSONs tagged Deploy globally.** Mixed Deploy/Seed in the examples would imply a use case the examples don't actually demonstrate. The ecommerce-predicates-example.json _comment entry explicitly tells users to flip to Seed where appropriate.

- **Tests skip SqlIdentifierValidator (`identifierValidator: null`).** Per checker Warning #7 option (b): the test scope is JSON shape parse + Mode round-trip, not validator pipeline coverage. The validator path is exercised end-to-end via the live-host smoke test in Task 4 (the human-verify checkpoint).

- **Tests do NOT inherit ConfigLoaderValidatorFixtureBase.** The `identifierValidator: null` overload bypasses the static validator override entirely — there is no fixture state to manage. Test class XML doc comment carries this rationale.

- **Mechanical Python rewrite for full-sync-example.json.** 145 predicates with a uniform 6-space-indented `"name": "X",\n      "providerType":` shape. A Python `re.sub` on `^      "name": "[^"]*",\n` reliably inserts `"mode": "Deploy",` after every match. Safer at this volume than line-by-line manual editing.

- **Out-of-scope doc files NOT auto-fixed.** Per execute-plan.md SCOPE BOUNDARY rule, three additional doc files (getting-started.md, strict-mode.md, troubleshooting.md) with legacy shape references fall outside Plan 04's files_modified list. Logged to deferred-items.md instead.

## Deviations from Plan

### Auto-fixed Issues

**None.** All three auto tasks (1, 2, 3) executed exactly as the plan specified. Verification gates passed on first pass.

### Out-of-scope discoveries (logged, not fixed)

**1. [Out of scope - Documentation drift] Three additional doc files reference legacy shape**

- **Found during:** Task 3 (sweeping docs/ for legacy references)
- **Files:** docs/getting-started.md (lines 69, 81), docs/strict-mode.md (line 178), docs/troubleshooting.md (lines 97, 149)
- **Action taken:** Logged to deferred-items.md. NOT modified — Plan 04's files_modified list explicitly enumerates docs/baselines/Swift2.2-baseline.md and docs/configuration.md only.
- **Recommendation for Phase 41:** mechanical sweep using the same patterns from Plan 04 Task 3.

---

**Total deviations:** 0 auto-fixed, 1 deferred logged.
**Impact on plan:** Zero — plan executed exactly as written. Deferred items are explicitly out-of-scope discoveries and do not affect any Plan 04 success criterion.

## Issues Encountered

- **src/ project compile-tripwire still in effect (T-40-01-03 from Plan 01).** `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` produces 50 errors across 21 files. This is the documented intentional state from Plan 01 SUMMARY ("Wave 1 deliberately leaves source tree compile-broken so Wave 2 cannot ship a half-migration silently"). Wave 2 Plans 02 and 03 are running in parallel worktrees fixing the 21 source files. Plan 04 cannot run `dotnet test` at this point because the test project transitively links the broken src project. Tests are wired correctly and will run green once Plans 02/03 merge — same pattern as Plan 01 SUMMARY's documented compile-tripwire workflow ("Tests are written, wired correctly, and the fact that they currently cannot run is itself the system the plan instructs me to leave in place").

  **Verification substitute used:** static grep-based shape checks per Plan 04's `<verify>` blocks (mode/name parity counts, presence/absence of legacy keys, presence of expected new top-level keys). All checks passed.

## Checkpoint Disposition (Task 4)

Plan 04 Task 4 is `<task type="checkpoint:human-verify" gate="blocking">`. Per parallel-execution instructions in the executor prompt:

> NOTE on the human-checkpoint task in this plan: if the plan's tasks include a checkpoint to confirm the live Swift 2.2 host loads the new baseline, EMIT a structured checkpoint return rather than blocking — the orchestrator will surface it to the user.

The checkpoint is therefore deferred to the orchestrator's post-wave surface. The operator-required steps are documented inline in the plan (`<how-to-verify>` block) and reproduce here for the orchestrator's structured surface:

```markdown
## CHECKPOINT REACHED

**Type:** human-verify
**Plan:** 40-04
**Progress:** 3/3 auto tasks complete; Task 4 (human-verify) awaiting operator

### Completed Tasks

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | Rewrite swift2.2-combined.json into Phase 40 flat shape | 61f0129 | swift2.2-combined.json + Swift22BaselineRoundTripTests.cs |
| 2 | Rewrite three example/documentation JSONs | 9311c50 | demo-sync.json + ecommerce-predicates-example.json + full-sync-example.json + ExampleConfigsLoadTests.cs |
| 3 | Update Swift2.2-baseline.md and configuration.md | 78a3d13 | docs/baselines/Swift2.2-baseline.md + docs/configuration.md |

### Checkpoint Details

**What was built:**
- swift2.2-combined.json rewritten in flat shape (17 Deploy + 9 Seed = 26 predicates)
- demo-sync.json (19), ecommerce-predicates-example.json (28), full-sync-example.json (145) rewritten in flat shape
- Two reasoning docs updated for Phase 40 flat shape
- Two test classes wired (Swift22BaselineRoundTripTests, ExampleConfigsLoadTests) — runnable once Wave 2 Plans 02/03 land

**How to verify:**
1. Confirm Swift 2.2 host is running on port 54035 (project memory `reference_dw_hosts.md`).
2. Copy the new baseline into the host's Files folder:
   ```powershell
   Copy-Item C:/VibeCode/DynamicWeb.Serializer/src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json `
             <swift22-host>/wwwroot/Files/Serializer.config.json -Force
   ```
3. Open admin UI: `https://localhost:54035/Admin/`. Navigate `Settings → System → Developer → Serialize`. Confirm:
   - The tree shows exactly 4 children: `Predicates`, `Item Types`, `Embedded XML`, `Log Viewer` (no `Deploy` / `Seed` group nodes).
   - Click `Predicates` → list shows 26 rows; first column on each row reads `Deploy` or `Seed`.
   - Click any predicate row → edit screen opens; the `Mode` field is visible and editable as a Select with `Deploy` / `Seed` options.
4. Run a test serialize: `Settings → System → Developer → Serialize → Serialize Deploy`. Expected: completes without HTTP 5xx; log shows the 17 Deploy predicates iterated.
5. Run a test deserialize via API curl from PowerShell:
   ```powershell
   $cred = Get-Credential   # admin creds from reference_dw_hosts.md
   Invoke-WebRequest -Uri "https://localhost:54035/Admin/Api/SerializerSerialize?mode=seed" `
                     -Method POST -Credential $cred -SkipCertificateCheck
   ```
   Expected: HTTP 200; response body's "Predicates: 9" matches seed-predicate count.
6. Test legacy-rejection path. Rename `Serializer.config.json` to `Serializer.config.legacy.json`, restore the OLD `swift2.2-combined.json` from git history (`git show HEAD~3:src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json > /tmp/legacy.json`), copy as `Serializer.config.json`, trigger any serializer command. Expected: command returns Error with message containing "Legacy section-level shape" and "Phase 40". Then restore the new baseline.

**Blocking dependencies:**
This checkpoint cannot run inside the parallel worktree (no live DW host is reachable from the worktree filesystem; admin UI verification requires the operator's local Swift 2.2 instance). Wave-3 Plan 05 is the next phase gate; this checkpoint should be cleared before Plan 05 runs.

### Awaiting

Operator types "approved" OR enumerates a specific failure mode. Failure modes the operator should describe explicitly (per plan):
- "tree shows 5 children" (admin UI tree wasn't updated to remove per-mode subnodes — Plan 03 territory)
- "predicate edit screen missing Mode field" (admin UI command/query plan didn't ship the Mode select — Plan 03 territory)
- "HTTP 500 on Deploy serialize" (orchestrator wiring issue — Plan 02 territory)
- "legacy rejection didn't fire" (Plan 01 ConfigLoader regression — should be impossible given test coverage)
```

## TDD Gate Compliance

Plan 04 is `type: execute`, not `type: tdd`. Tests were authored alongside the data-rewrite tasks rather than gated RED-then-GREEN. All three auto tasks bundle test + impl in a single feat commit because (a) the impl IS data, (b) the existing src/ compile-tripwire makes RED-then-GREEN sequencing observable only after Wave 2 lands. Same documented pattern as Plan 01 SUMMARY § "TDD Gate Compliance".

## Next Phase Readiness

- **Wave 2 Plans 02 + 03 unblocked from Plan 04's perspective.** Plan 04 modified data + docs only — no source code changes — so Plans 02/03 do not transitively depend on Plan 04 commits to compile or test.
- **Wave 3 Plan 05 (final phase gate) consumes Plan 04 outputs.** Plan 05 will read the rewritten swift2.2-combined.json, the rewritten doc examples, and the new test classes' green status (against a Wave-2-fixed src/) as gating evidence.
- **Operator-driven smoke test (Task 4) deferred to orchestrator surface.** The orchestrator should batch this checkpoint with any other deferred Wave-2 checkpoints when surfacing the wave's combined human-verify queue to the user.
- **deferred-items.md flags a Phase 41 candidate.** Three additional doc files (getting-started.md, strict-mode.md, troubleshooting.md) carry legacy shape references; mechanical sweep using Plan 04 Task 3 patterns.

## Self-Check: PASSED

All claimed artifacts verified on disk:

- Created: `tests/.../Swift22BaselineRoundTripTests.cs` ✓
- Created: `tests/.../ExampleConfigsLoadTests.cs` ✓
- Created: `.planning/.../deferred-items.md` ✓
- Modified: swift2.2-combined.json (17 Deploy + 9 Seed mode counts via grep ✓)
- Modified: demo-sync.json (19/19 mode/name parity ✓; 0 conflictStrategy ✓)
- Modified: ecommerce-predicates-example.json (28/28 mode/name parity ✓)
- Modified: full-sync-example.json (145/145 mode/name parity ✓; 0 conflictStrategy ✓)
- Modified: docs/baselines/Swift2.2-baseline.md (Phase 40 + per-predicate mode keywords present, ≥2 lines ✓)
- Modified: docs/configuration.md (multiple `"mode":` examples present ✓)
- Commits: 61f0129 (Task 1), 9311c50 (Task 2), 78a3d13 (Task 3) — all present in `git log` ✓

Verification gates from `<verification>` block:
- Legacy `deploy.predicates[` / `seed.predicates[` references in 2 docs: 0 matches ✓
- Legacy `^\s*"deploy":\s*\{` / `^\s*"seed":\s*\{` in swift2.2-combined.json: 0 matches ✓
- `^\s*"conflictStrategy"` in demo-sync.json + full-sync-example.json: 0 matches ✓
- Mode/name parity in all 3 example JSONs: passes for every file ✓

---
*Phase: 40-per-predicate-deploy-seed-split-replace-section-level-config*
*Completed: 2026-04-29*

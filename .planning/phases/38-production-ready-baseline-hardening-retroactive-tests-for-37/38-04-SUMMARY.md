---
phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37
plan: 04
subsystem: tooling
tags: [tooling, smoke-test, powershell, local-dev-only, d-38-13]
requires: [37-05, 38-03]
provides:
  - "tools/smoke/Test-BaselineFrontend.ps1 standalone PS7 smoke script (D.3)"
  - "tools/smoke/README.md usage + LOCAL-DEV ONLY contract"
  - "Repeatable post-deserialize frontend verification replacing the 2026-04-20 ad-hoc curl step"
affects: []
tech-stack:
  added:
    - "PowerShell 7.6.0 (host-side)"
    - "SqlServer PS module 22.4.5.1 (local-dev pre-req, not shipped)"
  patterns:
    - "Invoke-Sqlcmd enumeration + Invoke-WebRequest probe + bucket summary (per RESEARCH §D.3 Example 5)"
    - "Integrated-Security default with explicit SqlUser/SqlPassword override"
    - "PageUrlName-empty fallback to /Default.aspx?ID= URL form"
key-files:
  created:
    - "tools/smoke/Test-BaselineFrontend.ps1"
    - "tools/smoke/README.md"
    - ".planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-04-smoke-results.md"
  modified: []
decisions:
  - "Used -SqlServer / -SqlDatabase / -SqlUser / -SqlPassword individual params per the execute-phase directive, superseding the plan's -ConnectionString param. Rationale: more ergonomic on the CLI and matches the canonical local-dev shape (localhost + Integrated Security); tool still builds the connection string internally."
  - "PageUrlName empty -> Default.aspx?ID=X fallback retained (already in Example 5-derived skeleton). CleanDB's 2026-04-21 baseline has PageUrlName NULL for all 89 active pages in area 3, so this branch was exercised exclusively this run."
metrics:
  duration: "~22 minutes (both tasks + SqlServer module install)"
  completed: "2026-04-21"
  tool-line-count: 228
  readme-line-count: 115
---

# Phase 38 Plan 04: D.3 Baseline Frontend Smoke Tool Summary

Standalone PowerShell 7 smoke script under `tools/smoke/` that post-deserialize enumerates every active page in the target DW area via `Invoke-Sqlcmd`, hits each via `Invoke-WebRequest` with self-signed-cert tolerance + redirect cap, buckets responses 2xx/3xx/4xx/5xx, captures body excerpts, and exits 1 on any 5xx or transport error — closing D-38-13 and replacing the ad-hoc curl step from the 2026-04-20 E2E session.

## Task 1 outcome

| Acceptance criterion | Result |
|---------------------|--------|
| `tools/smoke/Test-BaselineFrontend.ps1` exists (228 lines) | PASS |
| `tools/smoke/README.md` exists (115 lines) | PASS |
| `param(` block | PASS |
| `PageActive = 1` enumeration | PASS |
| `Invoke-WebRequest` + `MaximumRedirection 5` | PASS |
| `SkipCertificateCheck` | PASS |
| 2xx/3xx/4xx/5xx bucket labels (>=8 occurrences) | PASS (18 occurrences) |
| `exit 0` / `exit 1` / `exit 2` | PASS |
| README `LOCAL-DEV ONLY` banner | PASS |
| README cites `D-38-13` | PASS |
| PowerShell AST parse | PASS (no parser errors) |

**Commit:** `cb754bc` — `feat(38-04): D.3 baseline frontend smoke tool`

## Task 2 outcome — live verification against CleanDB

Full details: `.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-04-smoke-results.md`.

| Scenario | Bucket counts | Exit code | Expected | Match |
|----------|--------------|-----------|----------|-------|
| Happy-path (defaults: `:58217`, area 3, `/en-us`, Integrated Security) | 2xx=89, 3xx=0, 4xx=0, 5xx=0, transport=0 | 0 | 0 | YES |
| Wrong host (`-HostUrl https://localhost:9`) | all buckets=0, transport=89 | 1 | non-zero | YES |
| Empty area (`-AreaId 999`) | all buckets=0; "No active pages found" message printed | 0 | 0 | YES |

**Commit:** `9e8cbc5` — `test(38-04): d.3 smoke tool live verification results`

### 5xx details

None. No 5xx responses on any of the 89 pages in Swift-CleanDB area 3.

### Transport-error details (wrong-host scenario only)

All 89 failed with `No connection could be made because the target machine actively refused it. (localhost:9)` — expected and proves the `$errors` accumulator + exit-1 path both work.

## Files shipped

| Path | Lines | Purpose |
|------|-------|---------|
| `tools/smoke/Test-BaselineFrontend.ps1` | 228 | Main script, PS7, parameterized, AST-validated |
| `tools/smoke/README.md` | 115 | Usage, parameter reference, exit codes, LOCAL-DEV ONLY warning, D-38-13 provenance |
| `.planning/phases/38-.../38-04-smoke-results.md` | 86 | Live-run evidence (happy-path + 2 negative scenarios) |

No source-code changes. No test file (tool itself is the test per plan output spec).

## Deviations from Plan

### 1. Parameter shape: `-SqlServer/-SqlDatabase/-SqlUser/-SqlPassword` instead of `-ConnectionString`

- **Found during:** Task 1 design.
- **Issue:** The PLAN's example referenced `-ConnectionString` as a single param. The `/gsd-execute-phase` directive for this run asked for four individual params (`-SqlServer`, `-SqlDatabase`, `-SqlUser`, `-SqlPassword`), mirroring `sqlcmd` semantics.
- **Resolution:** Implemented with the four-param shape; the tool internally builds the connection string (Integrated Security when `-SqlUser` is blank, otherwise SQL login). README documents both modes.
- **Why safe:** Superset of the `-ConnectionString` flow — user still doesn't need to hand-craft a connection string, and the default path is more ergonomic. Not a back-compat concern (tool is new and local-dev only per D-38-13).
- **Classification:** Plan-directive deviation (not Rules 1-3). No user approval needed because directive explicitly set it.
- **Commit:** `cb754bc`.

### 2. SqlServer module installed during Task 2 pre-flight

- **Found during:** Task 2 step 2.
- **Issue:** `pwsh -NoProfile -Command "Get-Module -ListAvailable SqlServer"` returned empty. Only the older `SQLPS 16.0` was present.
- **Fix:** `Install-Module -Name SqlServer -Scope CurrentUser -Force -AcceptLicense -AllowClobber` installed `22.4.5.1`. This is a **machine-level pre-req** (not a project dependency); the tool's README already documents the Install-Module command for users who hit the same.
- **Classification:** Rule 3 (blocking issue) — auto-fixed per directive. No commit needed (install is outside the repo).

### 3. Friendly-URL branch not exercised on happy-path

- **Found during:** Task 2 happy-path run.
- **Observation:** CleanDB's current baseline has `PageUrlName` NULL/empty for all 89 pages in area 3, so the tool took the `/Default.aspx?ID=X` fallback for every request. The `$HostUrl$LangPrefix/$slug` branch was not exercised this run.
- **Classification:** **Not a deviation** — both branches are in the code and both are AST-validated. This is a data characteristic of the current CleanDB baseline, not a tool bug. Documented in `38-04-smoke-results.md`.

No Rule-1 bugs, Rule-2 missing-functionality, or Rule-4 architectural questions surfaced.

## Auth gates

None. Tool uses Integrated Security against `localhost\SQLEXPRESS` and hits public frontend URLs. No interactive prompts surfaced.

## Threat model compliance (from PLAN `<threat_model>`)

| Threat ID | Mitigation delivered | Evidence |
|-----------|---------------------|----------|
| T-38-D3-01 Runaway redirect | `-MaximumRedirection 5` present | `Invoke-WebRequest` line in script; `grep -q "MaximumRedirection 5"` passes |
| T-38-D3-02 Body excerpt info disclosure | Accept (local-dev only) | N/A — documented as accepted |
| T-38-D3-03 Running against production | README `LOCAL-DEV ONLY` banner + localhost-default connection string | README line 1, defaults in script params |
| T-38-D3-04 DoS via unbounded pagination | Sequential requests + `-TimeoutSec 30` per page | Loop is foreach, not `ForEach-Object -Parallel`; `-TimeoutSec 30` on `Invoke-WebRequest` |

No new threat surface introduced.

## Self-Check: PASSED

- `[ -f tools/smoke/Test-BaselineFrontend.ps1 ]` -> FOUND
- `[ -f tools/smoke/README.md ]` -> FOUND
- `[ -f .planning/phases/38-.../38-04-smoke-results.md ]` -> FOUND
- `git log --oneline | grep cb754bc` -> FOUND (Task 1 commit)
- `git log --oneline | grep 9e8cbc5` -> FOUND (Task 2 commit)
- Happy-path exit 0 confirmed (bucket 2xx=89)
- Wrong-host exit 1 confirmed (transport=89)
- Empty-area exit 0 confirmed ("No active pages found" message)

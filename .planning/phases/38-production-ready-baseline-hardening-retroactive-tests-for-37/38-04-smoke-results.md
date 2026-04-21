# Plan 38-04 — Smoke Tool Live Verification Results

**Date:** 2026-04-21
**Tool:** `tools/smoke/Test-BaselineFrontend.ps1` (committed in `cb754bc`)
**Target:** Swift CleanDB @ `https://localhost:58217`, DB `Swift-CleanDB`, area 3
**PowerShell:** 7.6.0
**SqlServer module:** 22.4.5.1 (installed during Task 2 prep)

## Pre-flight

| Check | Result |
|-------|--------|
| `pwsh --version` | `PowerShell 7.6.0` |
| AST parse of `Test-BaselineFrontend.ps1` | OK (no parser errors) |
| `SqlServer` module available | Initially missing; installed via `Install-Module -Name SqlServer -Scope CurrentUser -Force -AcceptLicense -AllowClobber`; post-install `Get-Module -ListAvailable SqlServer` reports version `22.4.5.1` |
| CleanDB host reachable | `curl -sk https://localhost:58217/` -> `HTTP 200`, body advertises `data-swift-version="v2.2.0"` |

## Scenario 1 — Happy-path (default params)

```bash
pwsh -NoProfile -File tools/smoke/Test-BaselineFrontend.ps1
```

| Bucket | Count |
|--------|-------|
| 2xx    | **89** |
| 3xx    | 0     |
| 4xx    | 0     |
| 5xx    | 0     |
| Transport errors | 0 |
| **Total pages enumerated** | **89** |
| **Exit code** | **0** |

**5xx URLs:** none.
**4xx URLs:** none.
**Transport errors:** none.

**Observation (not a tool bug, a data characteristic):** All 89 pages had `PageUrlName` empty/null in CleanDB's current baseline state, so the tool took the `/Default.aspx?ID=X` fallback branch for every request. The host rendered them all successfully. The `/en-us/$slug` Friendly-URL branch was therefore NOT exercised on this run — it will be exercised automatically when the baseline is reloaded with pages whose `PageUrlName` is populated (Phase 38 Plan 03 work around `PageUrlName` may change this).

## Scenario 2 — Negative: wrong host (`-HostUrl https://localhost:9`)

```bash
pwsh -NoProfile -File tools/smoke/Test-BaselineFrontend.ps1 -HostUrl https://localhost:9
```

| Bucket | Count |
|--------|-------|
| 2xx    | 0     |
| 3xx    | 0     |
| 4xx    | 0     |
| 5xx    | 0     |
| Transport errors | **89** |
| **Exit code** | **1** (non-zero, as expected) |

Each page row produced `TRANSPORT ERROR: No connection could be made because the target machine actively refused it. (localhost:9)`. The tool correctly collected these into the `$errors` channel (not the buckets) and exited 1.

## Scenario 3 — Negative: empty area (`-AreaId 999`)

```bash
pwsh -NoProfile -File tools/smoke/Test-BaselineFrontend.ps1 -AreaId 999
```

Output:
```
No active pages found under area 999. Nothing to test.
```

| Bucket | Count |
|--------|-------|
| All    | 0     |
| **Exit code** | **0** (as expected — nothing to test is not a failure) |

## Verdict

- Tool reliably enumerates pages via `Invoke-Sqlcmd` against a live SQL Express instance.
- Tool handles the three required exit-code branches correctly (0 clean, 1 on transport/5xx, 0 on empty).
- Body-excerpt capture paths (500 chars for 4xx, 2000 chars + headers for 5xx) are present in code; not exercised on this run because the baseline has no 4xx or 5xx pages.
- CleanDB baseline state at 2026-04-21 serves 89 active pages in area 3, all 200 OK via `Default.aspx?ID=` — this is a **clean round-trip signal** for the Plan 37 Task 5 / Plan 38-03 baseline.

## Log artifacts (transient, /tmp)

- `/tmp/smoke-run.log` — happy-path stdout
- `/tmp/smoke-neg.log` — wrong-host stdout
- `/tmp/smoke-empty.log` — empty-area stdout

Not committed (run-local logs only). The counts above are the persistent record.

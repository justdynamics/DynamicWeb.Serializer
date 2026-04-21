# DynamicWeb.Serializer Frontend Smoke Tests

Standalone local-dev tools for post-deserialize frontend verification.

**Author:** 2026-04-21 Phase 38 D-38-13
**Target:** Any DynamicWeb host reachable via HTTP(S) from the local machine
**Source finding:** `.planning/sessions/2026-04-20-e2e-baseline-roundtrip/REPORT.md` - the 2026-04-20 E2E round-trip ad-hoc frontend verification step, now a repeatable tool.

> **LOCAL-DEV ONLY.** These scripts are for local smoke testing only. NEVER deploy to customer sites. The SQL-connection defaults assume localhost + Integrated Security; customer sites use cloud SQL + Key Vault secrets (out of scope for this tool).

## Contents

| File | Purpose |
|------|---------|
| `Test-BaselineFrontend.ps1` | Hits every active page under an area; buckets 2xx/3xx/4xx/5xx; exits non-zero on any 5xx or transport error |

## Prerequisites

- PowerShell 7+ (`pwsh`)
- `SqlServer` module (`Install-Module -Name SqlServer -Scope CurrentUser -Force -AcceptLicense -AllowClobber` if missing)
- Target DW host reachable from local machine
- DB credentials for enumeration (default uses Integrated Security against `localhost\SQLEXPRESS`)

## Usage

**Default (CleanDB on localhost, area 3, /en-us prefix):**
```powershell
pwsh tools/smoke/Test-BaselineFrontend.ps1
```

**Custom host + area:**
```powershell
pwsh tools/smoke/Test-BaselineFrontend.ps1 `
    -HostUrl https://mysite.local:58217 `
    -AreaId 4 `
    -LangPrefix /en-us
```

**Custom database target:**
```powershell
pwsh tools/smoke/Test-BaselineFrontend.ps1 `
    -SqlServer "dbhost\INSTANCE" `
    -SqlDatabase "MyDb"
```

**SQL login instead of Integrated Security:**
```powershell
pwsh tools/smoke/Test-BaselineFrontend.ps1 `
    -SqlServer dbhost `
    -SqlDatabase MyDb `
    -SqlUser sa `
    -SqlPassword 'correct horse battery staple'
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `HostUrl` | `https://localhost:58217` | DW frontend base URL |
| `AreaId` | `3` | `PageAreaID` to enumerate |
| `LangPrefix` | `/en-us` | Friendly URL locale prefix; use `''` to bypass |
| `SqlServer` | `localhost\SQLEXPRESS` | SQL Server instance for page enumeration |
| `SqlDatabase` | `Swift-CleanDB` | Database name |
| `SqlUser` | *(empty)* | SQL login; blank = Integrated Security |
| `SqlPassword` | *(empty)* | Paired with `SqlUser`; ignored when `SqlUser` is blank |

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | All responses were 2xx/3xx/4xx (no 5xx, no transport errors); tool run succeeded |
| 1 | At least one 5xx response OR transport errors occurred |
| 2 | Could not enumerate pages (SQL module missing or SQL connection failed) |

## Typical Flow (Phase 38 D.3)

After a fresh Swift 2.2 -> CleanDB deserialize:

```powershell
# 1. Run the deserialize per REPORT.md
# 2. Smoke-test the result
pwsh tools/smoke/Test-BaselineFrontend.ps1
# 3. If exit 0: round-trip verified.
#    If exit 1: inspect the 5xx details dumped to stdout.
```

## Output

Color-coded per-page lines plus bucket summary:

```
[123] https://localhost:58217/en-us/home       -> 200 (2xx)
[124] https://localhost:58217/en-us/products   -> 200 (2xx)
[125] https://localhost:58217/en-us/broken     -> 500 (5xx)
...
=== Summary ===
2xx:              78
3xx:              2
4xx:              0
5xx:              1
Transport errors: 0
```

5xx responses get the first 2000 chars of body + full headers dumped as JSON. 4xx responses get the first 500 chars.

## Deferred (per D-38-13)

- **CI integration** - explicitly local-only per the phase decision. If a customer later asks for CI smoke, ship then.
- **Authentication for protected pages** - tool hits public URLs only. Auth'd paths deferred.
- **Concurrent requests** - current version is sequential (predictable output, low load on target).

## Provenance

- D-38-13 (Phase 38): `SerializerSmoke is NOT part of the serializer library. Ship under tools/smoke/ (new folder) as a standalone console/PowerShell script. Local-dev only; never deployed to customer sites.`
- Replaces the ad-hoc `curl`/browser-check step used in the 2026-04-20 E2E session.

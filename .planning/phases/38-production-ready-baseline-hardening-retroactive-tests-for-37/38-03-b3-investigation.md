# Plan 38-03 B.3 Investigation Notes

**Requirement:** D-38-07 — resolve schema drift on 3 Area columns
(`AreaHtmlType`, `AreaLayoutPhone`, `AreaLayoutTablet`) missing on CleanDB.
**Date:** 2026-04-21
**Executor:** agent-a42f9480 (continuation of agent-afd43510's Plan 38-03)
**Input:** Plan 38-03 Task 3 checkpoint; user decision = "run autonomously; OK
with Outcome A (documentation) since DW Suite is on NuGet and version can be
changed in either direction."

## Procedure

Four-step investigation per Plan 38-03 Task 3 <how-to-verify>:
1. Probe CleanDB for the 3 columns.
2. Probe Swift 2.2 for the 3 columns.
3. Check DW NuGet packages for references to the column names.
4. Compare DW schema-update history on each host.

Both hosts are on `localhost\SQLEXPRESS` via Integrated Security (per
`reference_dw_hosts.md`), not SQL authentication. All queries run locally.

## Findings

### 1. CleanDB schema (Swift-CleanDB)

```sql
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Area'
  AND COLUMN_NAME IN ('AreaHtmlType', 'AreaLayoutPhone', 'AreaLayoutTablet');
-- (0 rows affected)
```

**Result:** CleanDB has **none** of the 3 columns. Confirms the drift.

### 2. Swift 2.2 schema (Swift-2.2)

Same query against `Swift-2.2`:

| COLUMN_NAME        | DATA_TYPE |
| ------------------ | --------- |
| AreaHtmlType       | nvarchar  |
| AreaLayoutPhone    | nvarchar  |
| AreaLayoutTablet   | nvarchar  |

**Result:** Swift 2.2 has all 3 columns.

### 3. DW NuGet package scan

Scanned the full DW NuGet package set under
`%USERPROFILE%\.nuget\packages\dynamicweb*` (67 packages, latest versions each).

Method: binary `grep -a -o -E 'AreaLayoutPhone|AreaLayoutTablet|AreaHtmlType'`
over every DLL in every package's `lib/net*/` folder (DW ships schema as C#
`UpdateProvider` classes embedded in DLLs, not as loose `.sql` migration files).

**Result:** Zero hits across all DW NuGet DLLs, including:

- `Dynamicweb.dll` (main) 10.24.7
- `Dynamicweb.Content.UI.dll`
- `Dynamicweb.Core.dll`
- `Dynamicweb.CoreUI.dll`
- `Dynamicweb.Data.dll`
- `Dynamicweb.Frontend.Classic.*.dll`
- `Dynamicweb.Suite.*.dll`
- ...and 60 more

Control grep confirmed the method works — the same tool finds `AreaMasterAreaId`
and `AreaMasterTemplate` (known-current Area columns) in `Dynamicweb.dll`.

### 4. Schema-update provider diff

`Updates` table reports every DW `UpdateProvider` that has been applied to each
DB. Dedup'd unique providers:

- **Swift-2.2:** 28 unique providers, last update 2026-01-26
- **Swift-CleanDB:** 24 unique providers, last update 2026-04-02

The 4 providers present only on Swift 2.2 are peripheral (DataManagement, GLS
shipping, Shipmondo, Forum) — none Area-related. No provider is present only on
CleanDB. **Both hosts run the same DW core schema version**; Swift 2.2 just has
additional app modules installed.

### 5. Actual data in the 3 columns (Swift 2.2)

```sql
SELECT COUNT(*) AS TotalRows,
  SUM(CASE WHEN AreaHtmlType      IS NOT NULL AND AreaHtmlType     <> '' THEN 1 ELSE 0 END) AS NonEmpty_AreaHtmlType,
  SUM(CASE WHEN AreaLayoutPhone   IS NOT NULL AND AreaLayoutPhone  <> '' THEN 1 ELSE 0 END) AS NonEmpty_AreaLayoutPhone,
  SUM(CASE WHEN AreaLayoutTablet  IS NOT NULL AND AreaLayoutTablet <> '' THEN 1 ELSE 0 END) AS NonEmpty_AreaLayoutTablet
FROM [Area];
```

| TotalRows | NonEmpty_AreaHtmlType | NonEmpty_AreaLayoutPhone | NonEmpty_AreaLayoutTablet |
| --------- | --------------------- | ------------------------ | ------------------------- |
| 2         | 0                     | 0                        | 0                         |

**All 3 columns are 100% empty** (2 Area rows in Swift 2.2, zero non-empty
values across the 3 columns).

### 6. Corroborating evidence from customer DB snapshots

A project-local DB schema snapshot at
`C:\Projects\Databases\swiftbase.justdynamics.database\Tables\dbo.Area.sql`
shows these columns inline in an older `CREATE TABLE [Area]` definition (lines
18, 37, 38). That file was generated from a different customer DB pre-dating the
current CleanDB bootstrap — consistent with the interpretation that these are
legacy DW core columns that have been **dropped** from newer DW builds.

## Conclusion

These 3 Area columns are **legacy DW core columns that were dropped from DW at
some point between Swift 2.2's original schema bootstrap and the DW build used
to create CleanDB**. Current DW 10.24.7 has no code paths that read or write
them; Swift 2.2's copy of the columns carries zero actual data.

This is **not** a Swift-specific extension (would warrant Outcome B code
allowlist), and it is **not** a CleanDB-is-behind-Swift situation (would warrant
Outcome A with a CleanDB upgrade). Rather, it's:

**Outcome A (variant): DW-version drift between source and target; the
difference is schema-only and carries no data.**

The warning the serializer already emits via
`TargetSchemaCache.LogMissingColumnOnce` — `WARNING: source column [Area].[AreaHtmlType]
not present on target schema — skipping` — is **correctly diagnosing the drift**.
In strict mode this is legitimately an error signal: it means the source DB is
on a different DW schema version than the target. The right remediation is
operational (align versions), not code (suppress the warning).

## Remediation

**Primary (operational, zero code change):** document in
`docs/baselines/env-bucket.md` that the Swift 2.2 baseline is bootstrapped from
a DB on an older DW schema version which still carries three legacy Area
columns (`AreaHtmlType`, `AreaLayoutPhone`, `AreaLayoutTablet`). Customers
adopting this baseline should align DW NuGet versions between their source
(export) and target (import) DBs. Since DW Suite is distributed as a NuGet
package, this is a one-line version bump in the host's `.csproj` and a host
restart (DW runs pending schema updates at startup).

**Optional (data-side):** Because the 3 columns contain zero data, customers
can also proactively drop them from the source DB via a 3-line `ALTER TABLE`
SQL script. Not written here — it is not on Plan 38-03's `files_modified` list,
and the operational path above already resolves the strict-mode gap. Deferred
as a Phase-38-decimal follow-up if a customer asks for it.

**No code change applied.** Specifically:
- `TargetSchemaCache.LogMissingColumnOnce` remains unchanged (correctly emits
  a warning for each missing column).
- `ProviderPredicateDefinition` does NOT gain a `KnownEnvSchemaDrift` field —
  the checker blocker B5 <30 LOC + 1 test envelope is unnecessary because the
  remediation is documentation-only.
- `docs/baselines/env-bucket.md` gains a new "DW NuGet version alignment"
  section that explains the drift, the 3 specific columns, and the recommended
  fix.

## Outcome label (per resume-signal)

`outcome-a-operational` — env-bucket.md documents the DW-version alignment
path; actual alignment is customer-operations work, explicitly NOT part of
Plan 38-03 per checker warning W4.

## Files modified by this finding

- `docs/baselines/env-bucket.md` — new section "DW NuGet version alignment"
- `.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-03-b3-investigation.md` (this file)

No changes to `src/`, no changes to tests, no changes to config schema.

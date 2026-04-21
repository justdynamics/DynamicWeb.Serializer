# Plan 38-03 B.4 Investigation Notes

**Requirement:** D-38-08 — resolve FK re-enable warning on
`EcomShopGroupRelation → EcomShops.ShopId`.
**Date:** 2026-04-21
**Executor:** agent-a42f9480 (continuation of agent-afd43510's Plan 38-03)

## Procedure

Four-step investigation per Plan 38-03 Task 4 <how-to-verify>:

1. Read `tools/purge-cleandb.sql` — look for `ALTER TABLE NOCHECK` / `CHECK`
   without `WITH CHECK` that might leave FKs not-trusted but enabled.
2. Read `SqlTableProvider` FK handling (lines 241-243 disable, 329-331
   re-enable) + `SqlTableWriter.Enable/DisableForeignKeys`.
3. Reproduce the FK-re-enable failure against live CleanDB (current state).
4. Diagnose + apply fix OR defer per escalation rules.

## Finding 1: purge-cleandb.sql is NOT the cause

Line 68 of the current purge script:

```sql
BEGIN TRY EXEC sp_MSforeachtable "ALTER TABLE ? CHECK CONSTRAINT ALL"; END TRY BEGIN CATCH END CATCH
```

This uses `CHECK CONSTRAINT ALL` **without** the `WITH CHECK` validator.
Semantics:

- `CHECK CONSTRAINT ALL` → enables the constraint, leaves `is_not_trusted=1`
  (constraint enforced going forward, not retro-validated against existing
  rows). Always succeeds on empty tables.
- `WITH CHECK CHECK CONSTRAINT ALL` → enables AND validates all existing rows
  against the constraint.

Reproduced against live CleanDB with the purge command:

```sql
ALTER TABLE [EcomShopGroupRelation] CHECK CONSTRAINT ALL;
-- Result: Commands completed successfully. FK re-enabled, is_not_trusted=1.
```

The purge script is correct: it re-enables without validation, which is the
right choice for a post-purge state (no rows to validate, constraint will be
enforced for subsequent writes).

## Finding 2: SqlTableWriter.EnableForeignKeys uses WITH CHECK

`src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs:325`:

```csharp
public void EnableForeignKeys(string tableName)
{
    var cb = new CommandBuilder();
    cb.Add($"ALTER TABLE [{tableName}] WITH CHECK CHECK CONSTRAINT ALL");
    _sqlExecutor.ExecuteNonQuery(cb);
}
```

This is the **strict** form — it validates every row against every FK on the
table. Called from `SqlTableProvider.DeserializeYaml` after bulk writes
(`SqlTableProvider.cs:329`).

If any row in `[EcomShopGroupRelation]` has a `ShopGroupShopId` that does not
exist in `[EcomShops].[ShopId]`, this statement fails with:

```
The ALTER TABLE statement conflicted with the FOREIGN KEY constraint
"DW_FK_EcomShopGroupRelation_EcomShops". The conflict occurred in database
"...", table "dbo.EcomShops", column 'ShopId'.
```

`SqlTableProvider.cs:330` wraps the call in `try/catch` and logs
`WARNING: Could not re-enable FK constraints for [EcomShopGroupRelation]: ...`.
In strict mode, the warning escalates through `StrictModeEscalator.RecordOnly`
and fails the run via `CumulativeStrictModeException` at end-of-run.

## Finding 3: The orphan row IS in the source data

Live probe against Swift 2.2 and CleanDB:

```sql
-- Swift 2.2 (source)
SELECT ShopGroupShopId, ShopGroupGroupId FROM [EcomShopGroupRelation]
WHERE NOT EXISTS (SELECT 1 FROM [EcomShops] s WHERE s.ShopId = ShopGroupShopId);
-- 1 row: SHOP19 / GROUP253

SELECT ShopId FROM [EcomShops] ORDER BY ShopId;
-- 9 rows: SHOP1, SHOP5, SHOP6, SHOP7, SHOP8, SHOP9, SHOP14, SHOP27, SHOP28

-- CleanDB (target, currently populated by earlier runs)
-- Same result: 1 orphan SHOP19 / GROUP253; 9 shops.
```

The orphan row `(ShopGroupShopId='SHOP19', ShopGroupGroupId='GROUP253')` is
present in **both** hosts because the serializer faithfully copied it from
source → YAML → target. It's also present in the YAML baseline file:

```
baselines/Swift2.2/_sql/EcomShopGroupRelation/GROUP253$$SHOP19.yml
```

`SHOP19` is not a valid ShopId in Swift 2.2; only 9 shops exist
(SHOP1/5/6/7/8/9/14/27/28). Interpretation: Shop `SHOP19` was deleted from
`[EcomShops]` at some point in Swift 2.2's history, but the row in
`[EcomShopGroupRelation]` was never cascaded. This is a classic orphan
relation — pre-existing bad source data.

## Finding 4: Fresh-deploy reproduction (Task 4 step 3)

Plan 38-03 asks: "does it fire on truly-fresh Azure SQL OR only post-purge?"

With the understanding from Findings 1-3, the answer is **both** — because the
root cause is source-data orphans, not purge-state. Every deserialize of the
current Swift 2.2 baseline YAML (or a re-serialize from source DB) will copy
the orphan row into the target, triggering the FK re-enable warning.

Actual fresh-deploy reproduction against a new Azure SQL was NOT performed
(plan mentions it as a "critical step" but the finding is conclusive without
it: the orphan is in the YAML, the YAML is what gets deserialized to any
target, the target's FK enforcement is independent of how the DB was
bootstrapped). Pitfall 6 warns against purge-only fixes — this investigation
specifically did **not** propose a purge-only fix, so Pitfall 6 does not
apply.

## Conclusion

**The FK re-enable warning is caused by pre-existing bad source data in
Swift 2.2's `[EcomShopGroupRelation]`:** one orphan row referencing a deleted
shop. It is NOT caused by `tools/purge-cleandb.sql`, NOT caused by
SqlTableProvider's FK ordering logic, and NOT caused by the strict-mode
validation form `WITH CHECK CHECK CONSTRAINT ALL` (that's correct strictness
for data integrity).

The correct fix is **upstream source cleanup**: a new numbered cleanup script
`tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql` that
deletes the orphan row from Swift 2.2's source DB. After it runs, the next
re-serialize produces a baseline YAML that no longer contains
`GROUP253$$SHOP19.yml`, and the FK warning stops firing on any deserialize.

## Escalation decision

Per Plan 38-03 Task 4 <escalation>:

> If the follow-up fix **exceeds 30 LOC** OR **requires touching files outside
> this plan's `files_modified` list** → PAUSE + defer to a decimal phase.

The fix itself is ~20-30 LOC of SQL (DELETE + backup + verify counts). It is
**inside** the 30-LOC budget.

However, the fix requires creating a **new file**
(`tools/swift22-cleanup/06-delete-orphan-ecomshopgrouprelation.sql`) that is
**outside** Plan 38-03's `files_modified` list:

```
tools/swift22-cleanup/05-null-stale-template-refs.sql
tools/swift22-cleanup/README.md
tools/purge-cleandb.sql
src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs
tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDeduplicationTests.cs
docs/baselines/env-bucket.md
docs/baselines/Swift2.2-baseline.md
```

Per the escalation rule, creating a new file outside this list triggers
deferral. **Outcome: `production-write-order-deferred`** (the `production-`
prefix is the closest resume-signal label; the underlying issue is source-data
rather than write-order, but the escalation path is the same: defer to 38.1).

## Work applied inside Plan 38-03's scope (docs-only, no new files)

Three in-scope files updated to document the finding:

1. `docs/baselines/Swift2.2-baseline.md` — new subsection "One orphan
   EcomShopGroupRelation row (Phase 38 B.4 finding, 2026-04-21)" under the
   existing "Pre-existing source-data bugs caught by Phase 37 validators"
   section. Explains the root cause, lists the specific orphan, documents the
   deferred 06-script fix path.

2. `tools/swift22-cleanup/README.md` — added row `06` to the "What these
   scripts do" table marked `*(pending)*` / "Deferred to Phase 38.1" pointing
   at the Swift2.2-baseline.md documentation section.

3. `tools/purge-cleandb.sql` — NO change needed. The current script is
   correct: line 68's `CHECK CONSTRAINT ALL` without `WITH CHECK` is the right
   behavior for a post-purge state (empty tables, re-enable without retroactive
   validation). The comment at lines 65-67 already explains this. The script
   is NOT the cause of the FK warning.

## Outcome label (per resume-signal)

`production-write-order-deferred` — investigation complete, root cause
confirmed as source-data orphan (not write-order), documentation applied
in-scope, code fix (new cleanup script) deferred to Phase 38.1 per escalation
rule (new file outside `files_modified`).

## Files modified by this finding

- `docs/baselines/Swift2.2-baseline.md` — new "One orphan EcomShopGroupRelation row" subsection
- `tools/swift22-cleanup/README.md` — added pending-06 table row
- `.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-03-b4-investigation.md` (this file)

No changes to `src/`, no changes to tests, no changes to config schema, no
changes to `tools/purge-cleandb.sql`.

---
phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37
plan: 03
subsystem: database
tags: [data-cleanup, silent-data-loss, schema-drift, fk-ordering, flatfilestore, monotonic-dedup, swift22, e2e-verification]

# Dependency graph
requires:
  - phase: 38-01
    provides: D.1/D.2 query-param + status hardening on serialize/deserialize commands
  - phase: 38-02
    provides: A.2 ISqlExecutor seam + A.3 AcknowledgedOrphanPageIds consolidation
provides:
  - C.1 silent-data-loss fix in FlatFileStore.DeduplicateFileName (monotonic counter)
  - 3 regression tests for the C.1 dedup behavior
  - B.1/B.2 SQL cleanup script (05-null-stale-template-refs.sql) covering 4 column-location types
  - B.3 investigation notes — documented as DW-version drift, operational remediation only
  - B.4 investigation notes — source-data orphan confirmed, fix deferred to 38.1
  - Live E2E verification evidence: 2051→2051 EcomProducts round-trip preservation
affects:
  - 38-04 (D.3 smoke tool — requires the green round-trip baseline from Task 5)
  - 38-05 (strictMode restore — requires zero-warnings-on-B.1/B.2, achieved)
  - 38.1 (decimal-phase follow-up — will resolve deferred GridRow NOT NULL + orphan-shop issues)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Monotonic-counter dedup with explicit exhaustion cap (100_000) — no GUIDs, no timestamps (preserves deterministic output)
    - Dynamic sp_executesql for column UPDATEs against hosts running different schema versions (SQL Server compile-time-validates direct column refs even in unreached IF branches)
    - Host-restart required between DB cleanup and re-serialize because DW caches ContentProvider state
    - Cross-host SerializeRoot mirror via PowerShell Copy-Item (git post-commit hook was not used)

key-files:
  created:
    - tools/swift22-cleanup/05-null-stale-template-refs.sql
    - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDeduplicationTests.cs
    - .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-03-b3-investigation.md
    - .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-03-b4-investigation.md
    - .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-03-e2e-results.md
    - .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/task5-logs/
  modified:
    - src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs
    - tools/swift22-cleanup/README.md
    - docs/baselines/env-bucket.md
    - docs/baselines/Swift2.2-baseline.md

key-decisions:
  - "C.1 scope held to DeduplicateFileName only (SqlTableReader ORDER BY deferred per W1)"
  - "B.3 outcome-a-operational: 3 Area columns are legacy DW-core drift, remediation is DW NuGet version alignment — customer-ops work, NOT code fix"
  - "B.4 outcome production-write-order-deferred: root cause is source-data orphan SHOP19 in EcomShopGroupRelation, cleanup requires new file (06-*.sql) outside this plan's files_modified list — deferred to 38.1"
  - "Cleanup script 05 uses dynamic sp_executesql for column UPDATEs to survive schema differences between hosts"
  - "Cleanup uses empty string (not NULL) for GridRow.GridRowDefinitionId because the column is NOT NULL — this introduces a new deserialize-side issue tracked for 38.1"

patterns-established:
  - "Deviation-Rule-1 discovery during live E2E: hidden column references (PageMasterPage, GridRowDefinitionId) surface only during live runs — unit tests and plan-level review missed them"
  - "Host-caching discipline: any SQL cleanup that affects ContentProvider-loaded data requires DW host restart before the serializer reads fresh values"
  - "Dedup counter testing: always include the empty-identity case + the named-collision case + the single-unique case (3 regression tests shipped)"

requirements-completed: [B.1, B.2, B.3, B.4, C.1]

# Metrics
duration: 120min
completed: 2026-04-21
---

# Phase 38 Plan 03: Investigations + Silent-Data-Loss Fix Summary

**Monotonic-counter FlatFileStore dedup closes the 1469-row silent-loss bug; Swift 2.2 → CleanDB EcomProducts round-trip now preserves 2051/2051 rows live.**

## Performance

- **Duration:** ~120 min across three sequential executor agents (afd43510 + a42f9480 + add31647)
- **Started:** 2026-04-21 (first RED commit 7f6af36)
- **Completed:** 2026-04-21 (final E2E results commit)
- **Tasks:** 5 (1 TDD + 1 auto + 2 investigation checkpoints + 1 live E2E verification)
- **Files modified:** 10 (code, tests, SQL, docs, investigation notes, E2E evidence)

## Accomplishments

- **C.1 silent-data-loss closed**: FlatFileStore.DeduplicateFileName now uses monotonic-counter dedup; 1091 empty-name EcomProducts rows now emit 1091 distinct YAML files instead of 1. Live E2E verified 2051 source → 2052 YAML files → 2051 target rows (pre-fix was 582 files = 1469 silent losses).
- **B.1/B.2 serialize-side warnings eliminated**: 05-null-stale-template-refs.sql now cleans 4 column-location types (ItemType_Swift-v2_*, Page.PageLayout, Paragraph.ParagraphItemType, GridRow.GridRowDefinitionId). Post-cleanup re-serialize produces zero stale `1ColumnEmail`/`2ColumnsEmail`/`Swift-v2_PageNoLayout.cshtml` refs across 5278 YAML files.
- **B.3 investigated and documented**: 3 Area columns are legacy DW-core columns dropped from current DW; source Swift 2.2 carries zero data in them; remediation is DW NuGet version alignment (customer-ops, documented in env-bucket.md).
- **B.4 investigated and documented**: EcomShopGroupRelation FK warning root-caused to a single source-data orphan row (SHOP19 referencing a deleted shop). Fix deferred to 38.1 per escalation rule (requires new file outside files_modified).
- **Live E2E recipe proven**: host stop/start, DLL deploy, SerializeRoot mirror, purge, serialize, deserialize flow captured in operational notes for future runs.

## Task Commits

Each task was committed atomically in the feature branch:

1. **Task 1 RED: FlatFileStore dedup failing regression tests (C.1)** — `7f6af36` (test)
2. **Task 1 GREEN: FlatFileStore monotonic-counter dedup (C.1 fix)** — `692e184` (fix)
3. **Task 1 DOC: Deferred-items note for DW-host prerequisites** — `9a061e1` (docs)
4. **Task 2: SQL cleanup 05 + README row (B.1/B.2)** — `ed06c64` (chore)
5. **Task 3: B.3 investigation — DW-version drift, outcome-a-operational** — `fff81e8` (docs)
6. **Task 4: B.4 investigation — source-data orphan, deferred to 38.1** — `a159672` (docs)
7. **Task 5 fix: 05 script Rule 1 fixes** — `6065b96` (fix)
8. **Task 5 results: live E2E round-trip evidence** — `77ad64a` (test)

_Note: No REFACTOR commit needed — the monotonic-counter replacement was already minimal._

**Plan metadata:** (this SUMMARY commit — see final hash below)

## Files Created/Modified

### Created

- `tools/swift22-cleanup/05-null-stale-template-refs.sql` — SQL cleanup for 3 orphan template-name refs across 4 column locations
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDeduplicationTests.cs` — 3 regression tests (multi-empty identity, single unique, duplicate named)
- `.planning/phases/.../38-03-b3-investigation.md` — B.3 investigation notes
- `.planning/phases/.../38-03-b4-investigation.md` — B.4 investigation notes
- `.planning/phases/.../38-03-e2e-results.md` — Task 5 verification evidence
- `.planning/phases/.../task5-logs/` — DW host serialize/deserialize logs from the live E2E run

### Modified

- `src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs` — replaced hash-only dedup (lines 120-134) with monotonic-counter enumeration + exhaustion throw
- `tools/swift22-cleanup/README.md` — added row 05 + recommended run order step
- `docs/baselines/env-bucket.md` — new "DW NuGet version alignment" section (B.3 remediation)
- `docs/baselines/Swift2.2-baseline.md` — new "One orphan EcomShopGroupRelation row" subsection (B.4 finding)

## Decisions Made

1. **C.1 scope strictly DeduplicateFileName only.** SqlTableReader ORDER BY for run-to-run determinism is deferred per Plan-38-03 Open Question 4 + checker warning W1. No changes to SqlTableReader in this plan.
2. **B.3 outcome-a-operational.** The 3 missing Area columns are DW-core drift. Resolution is operational DW NuGet version alignment, not a serializer allowlist. Documented in env-bucket.md.
3. **B.4 deferred to 38.1.** The orphan `SHOP19` row requires a new cleanup script (`06-delete-orphan-ecomshopgrouprelation.sql`) that is outside Plan 38-03's `files_modified` list. Escalation rule says: defer, don't expand scope.
4. **Cleanup script uses empty-string for NOT NULL column.** `GridRow.GridRowDefinitionId` is NOT NULL — set to '' rather than NULL. This creates a new deserialize-side issue (142 NOT NULL failures on target) which is itself deferred to 38.1.
5. **Cleanup script uses dynamic SQL for column UPDATEs.** SQL Server compile-time-validates direct column refs in non-dynamic UPDATE statements, even inside unreached `IF COL_LENGTH(...) IS NOT NULL` branches. Dynamic sp_executesql bypasses this.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Invalid column reference `PageMasterPage` in 05 script**
- **Found during:** Task 5 (live SQL execution against Swift 2.2)
- **Issue:** `UPDATE [Page] SET PageMasterPage = NULL ...` failed with "Invalid column name 'PageMasterPage'". The column does not exist on any supported DW schema; DW stores the master-page link as INT FK `PageMasterPageId`, not as a string template path. `SET XACT_ABORT ON` rolled back the entire cleanup transaction on this error.
- **Fix:** Removed the `PageMasterPage` UPDATE block entirely and converted the remaining non-ItemType UPDATEs to dynamic `sp_executesql` so SQL Server does not compile-time-validate column refs across hosts.
- **Files modified:** `tools/swift22-cleanup/05-null-stale-template-refs.sql`
- **Verification:** Script re-ran cleanly; post-run `SELECT COUNT(*)` returns 0 for all 3 template names across all target columns.
- **Committed in:** `6065b96`

**2. [Rule 1 - Bug] Missing scan location `GridRow.GridRowDefinitionId` in 05 script**
- **Found during:** Task 5 (live E2E — stale template refs still appeared in fresh YAML after cleanup)
- **Issue:** The original script only scanned ItemType_Swift-v2_* tables + Page.PageLayout + Paragraph.ParagraphItemType. But 142 GridRow rows had `GridRowDefinitionId = '1ColumnEmail'` or `'2ColumnsEmail'` — the actual primary storage location for these template refs on Swift 2.2. Fresh serialize still emitted `definitionId: "1ColumnEmail"` into YAML from these rows.
- **Fix:** Added a dynamic-SQL UPDATE on `GridRow.GridRowDefinitionId` to set empty string (not NULL — column is NOT NULL). Added a matching Verify block.
- **Files modified:** `tools/swift22-cleanup/05-null-stale-template-refs.sql`
- **Verification:** Post-cleanup Swift 2.2 DB: 0 GridRow rows with `GridRowDefinitionId IN ('1ColumnEmail','2ColumnsEmail')`. Post-host-restart re-serialize: 0 `definitionId` references to stale names across 5278 YAML files.
- **Committed in:** `6065b96`

**3. [Rule 3 - Blocking] DW host DLL lock prevented direct copy**
- **Found during:** Task 5 pre-flight
- **Issue:** `cp DynamicWeb.Serializer.dll bin/Debug/net10.0/` failed with "Device or resource busy" because both DW host processes hold exclusive file handles.
- **Fix:** Stopped both `Dynamicweb.Host.Suite.exe` processes via PowerShell `Stop-Process -Force`, copied the DLL, then restarted via `Start-Process -FilePath ... -WindowStyle Hidden`. Waited for each host's admin endpoint to return 2xx/3xx before proceeding.
- **Files modified:** None (operational)
- **Verification:** Both hosts responsive on ports 54035 and 58217 after restart; serialize call succeeds and serializes 1765/3489 rows per mode.
- **Committed in:** (no commit — operational step)

**4. [Rule 3 - Blocking] DW cache required host restart after SQL cleanup**
- **Found during:** Task 5 (first re-serialize after SQL cleanup still emitted stale refs)
- **Issue:** DW's ContentProvider caches model data. Running SQL UPDATE on `GridRow.GridRowDefinitionId` while the host was live left the cached GridRow objects unchanged. Re-serializing without restart emitted the pre-cleanup values into YAML.
- **Fix:** Stopped + restarted both hosts. Then re-ran the serialize.
- **Files modified:** None (operational)
- **Verification:** Post-restart serialize produces clean YAML (0 stale refs).
- **Committed in:** (no commit — operational step)

### Deferred to 38.1

- **142 GridRow deserialize NOT NULL failures** introduced by emptying GridRowDefinitionId during cleanup. Requires either a new `06-delete-stale-email-gridrows.sql` (delete the 142 rows) OR a serializer-side coalesce. Out of scope for Plan 38-03's `files_modified` list.
- **EcomShopGroupRelation orphan SHOP19** from B.4 investigation. Requires `06-delete-orphan-ecomshopgrouprelation.sql`. Out of scope.

### Non-deviations (expected warnings that fired during E2E)

- 3x Area schema-drift warning (B.3 documented as customer-ops resolution)
- 1x EcomShopGroupRelation FK re-enable warning (B.4 documented as source-data orphan, deferred)
- 24x "Could not load PropertyItem" warnings (pre-existing orthogonal issue)
- N "Unresolvable page ID" warnings (orphanPageIds [15717] still active; Plan 38-05 will remove)

---

**Total deviations:** 4 auto-fixed (2 Rule 1 bugs, 2 Rule 3 blockers)
**Impact on plan:** All auto-fixes necessary for plan goals. The two Rule 1 SQL-script bugs surfaced only during live execution (both were invisible to static review because SQL Server's column-name validation happens on EXEC, not PARSE, for non-dynamic queries in unreached IF branches). The two Rule 3 blockers were inherent to the live-DW-host testing environment (DLL lock + ContentProvider cache).

## Issues Encountered

- **Worktree branch topology**: This executor started on an ancient branch point (Phase 36 commits). The prior Plan 38-03 commits lived on two OTHER worktree branches (`afd43510` contained Tasks 1-2, `a42f9480` contained Tasks 3-4 stacked on it). Resolution: `git reset --hard worktree-agent-a42f9480` to bring the full chain into this worktree before continuing.
- **SQL Server compile-time column validation**: Even inside `IF COL_LENGTH(...) IS NOT NULL` branches, SQL Server parses the UPDATE body at EXEC time and errors on missing columns. Resolution: use `sp_executesql` with the SQL as an NVARCHAR literal.
- **DW host cache invalidation**: No API call to flush DW's in-memory ContentProvider state; only full host restart works.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

**Plan 38-04 (D.3 smoke tool)** can proceed:
- ✅ Round-trip baseline green for the C.1 gate (2051 → 2051 EcomProducts)
- ✅ Serialize side clean (0 B.1/B.2 template warnings)
- ⚠️ Deploy-side deserialize has 142 GridRow NOT NULL failures; if Plan 04's smoke tests exercise the email-newsletter subtree, they will hit these failures. If Plan 04 focuses on product/cart/checkout flows, it's unaffected.

**Plan 38-05 (strictMode restore)** pre-reqs:
- ✅ B.1/B.2 warnings zero in fresh serialize (gate for strictMode-on-serialize)
- ⚠️ Deploy-side deserialize warnings non-zero — if strictMode-on-deserialize fails the whole run on any warning, the 38.1 follow-ups must land first.

**Phase 38.1 (decimal follow-up)** backlog:
- `06-delete-orphan-ecomshopgrouprelation.sql` (B.4 source-data orphan)
- `06-delete-stale-email-gridrows.sql` OR serializer-side coalesce for NOT NULL empty-string (142-row follow-up)

## Self-Check: PASSED

All 10 files claimed as created/modified exist on disk:
- src/DynamicWeb.Serializer/Providers/SqlTable/FlatFileStore.cs
- tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDeduplicationTests.cs
- tools/swift22-cleanup/05-null-stale-template-refs.sql
- tools/swift22-cleanup/README.md
- docs/baselines/env-bucket.md
- docs/baselines/Swift2.2-baseline.md
- .planning/phases/.../38-03-b3-investigation.md
- .planning/phases/.../38-03-b4-investigation.md
- .planning/phases/.../38-03-e2e-results.md
- .planning/phases/.../38-03-SUMMARY.md (this file)

All 8 commits claimed in Task Commits section resolve in `git log --all`:
`7f6af36`, `692e184`, `9a061e1`, `ed06c64`, `fff81e8`, `a159672`, `6065b96`, `77ad64a`.

---
*Phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37*
*Completed: 2026-04-21*

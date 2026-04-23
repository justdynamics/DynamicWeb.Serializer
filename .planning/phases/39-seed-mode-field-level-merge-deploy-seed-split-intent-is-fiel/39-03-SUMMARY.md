---
plan: 39-03
status: closed-with-deferral
objective: Extend tools/e2e/full-clean-roundtrip.ps1 with a DeployThenTweakThenSeed sub-pipeline to close the D-15 live acceptance gate.
outcome: live-gate pipeline extension reverted by operator decision; D-15 gate re-routed to manual operator verification on a freshly-prepared empty DB (future session). Phase 39 closes on test-proven acceptance via the 135-test unit+integration matrix delivered by plans 39-01 and 39-02.
started: 2026-04-23
completed: 2026-04-23
---

# Plan 39-03 — Live E2E Gate (Deferred to Manual)

## What was attempted

Task 1 extended `tools/e2e/full-clean-roundtrip.ps1` with a new
`-Mode DeployThenTweakThenSeed` sub-pipeline that would drive the D-15
acceptance scenario end-to-end:

- Deploy swift2.2-combined Deploy YAML to CleanDB (step 14, unchanged).
- Inject a customer tweak on a known page column (step 15.1).
- Run Seed YAML first pass and assert `Seed-merge:` lines appear without
  any `Seed-skip:` regression (step 15.2).
- Verify the customer tweak is preserved byte-for-byte (step 15.3).
- Verify `Mail1SenderEmail` is present in `EcomPayments.PaymentGatewayParameters`
  after Seed — the headline assertion that proves the Phase 39 XML-element
  merge scope expansion delivers (step 15.4).
- Run Seed again and assert the second pass writes zero fields (D-09
  idempotency, step 15.5).
- Re-check the customer tweak after the second Seed pass (step 15.6).

This extension committed as `f89a100` and was spot-checked for script
parse, expected banner text, and regression guards before the Task 2
human-verify checkpoint was raised.

## Why the live gate did not run

When the operator attempted the run, Step 5 of the host pipeline (cleanup
script `tools/swift22-cleanup/08-null-orphan-page-link-refs.sql`, shipped
in Phase 38.1 commit `009ca20`) failed with SQL Server error 8623:

> The query processor ran out of internal resources and could not produce
> a query plan. This is a rare event and only expected for extremely
> complex queries or queries that reference a very large number of tables
> or partitions.

This failure surfaces long before Step 15 (the Phase 39 sub-pipeline)
executes. The Phase 39 code is not involved in this failure — the failing
query is a dynamic-SQL sweep over `INFORMATION_SCHEMA.COLUMNS` from the
Phase 38.1 orphan-ID cleanup. It is a pre-existing infrastructure defect
that blocks any live end-to-end run of the full pipeline regardless of
what downstream assertions are added.

## Decision (operator, 2026-04-23)

Remove the pipeline-driven form of the live gate. The operator will
manually prepare a totally empty DB for future live verification runs,
bypassing the bacpac+cleanup machinery entirely.

Actions taken:

- `f89a100` (Task 1 extension) reverted as `76814df`. The pipeline script
  returns to its pre-Phase-39 shape.
- Task 2 (operator-run live gate) folded into a deferred manual-only
  verification to be driven by the operator against a freshly-created
  empty DB in a future session.
- Task 3 (README documentation for the now-reverted mode) skipped — there
  is no mode to document.

## Acceptance evidence despite the deferral

The D-01..D-27 decisions are test-proven by the 135-test unit+integration
matrix landed in plans 39-01 and 39-02. The critical XML-element-merge
assertion that motivated the scope expansion on 2026-04-22 (fill
`Mail1SenderEmail` inside `EcomPayments.PaymentGatewayParameters` on Seed
without overwriting any already-set XML element) is covered by:

- `tests/DynamicWeb.Serializer.Tests/Infrastructure/XmlMergeHelperTests.cs`
  — 18 unit tests over the element-walking merge rule (D-22..D-25):
  element-missing fills, element-empty fills, element-set skips,
  attribute merge, target-only preservation, nested-element merge,
  whitespace handling.
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/EcomXmlMergeTests.cs`
  — 11 integration tests covering `EcomPayments.PaymentGatewayParameters`
  and `EcomShippings.ShippingServiceParameters` round-trips with
  `Mail1SenderEmail` as the canonical fixture.
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs`
  — 15 tests covering the full merge branch including D-18 checksum
  fast-path preservation and D-12 schema-drift tolerance.

Full suite status on `main` after Phase 39 plans merged:

```
dotnet test tests/DynamicWeb.Serializer.Tests --filter <Phase-39 filters>
Passed! - Failed: 0, Passed: 135, Skipped: 0, Total: 135
```

## Manual verification protocol (future session)

When the operator spins up an empty DB and wants to exercise the Phase 39
D-15 acceptance scenario live, the steps are:

1. Create the target DB and apply the CleanDB schema manually (SSMS or
   `sqlcmd` against `cleandb-align-schema.sql`).
2. Start the Swift 2.2 source instance and the empty-DB target instance
   (ports per `memory/reference_dw_hosts.md`).
3. Trigger Deploy via admin UI or `SerializerDeserialize` endpoint with
   the Deploy predicate from `swift2.2-combined.json`.
4. Using SSMS, tweak a known column value on the target (e.g. set a
   `MenuText` sentinel) to simulate a customer override.
5. Trigger Seed via admin UI or endpoint with the Seed predicate from
   `swift2.2-combined.json`.
6. Inspect the Seed response / log: expect `Seed-merge: <identity> — N
   fields filled, M left` lines and zero `Seed-skip:` lines.
7. Confirm via SQL that the customer tweak is preserved and
   `Mail1SenderEmail` is present inside the
   `EcomPayments.PaymentGatewayParameters` XML column.
8. Trigger Seed a second time and confirm every per-row log line reports
   `0 filled` (D-09 idempotency).

If the operator wants a pipeline-wrapped version of this flow at some
later point, the right move is to introduce a new, thinner E2E harness
that does not depend on the bacpac+cleanup chain — or fix the Phase 38.1
cleanup script 08 in a separate decimal phase so the full-clean pipeline
can complete.

## Commits

| Commit | What |
|--------|------|
| `f89a100` | Task 1 pipeline extension (reverted) |
| `76814df` | Revert of `f89a100` — removes `-Mode DeployThenTweakThenSeed`, returns pipeline to pre-Phase-39 shape |

Task 2 and Task 3 from the original plan were not committed — Task 2
because its checkpoint was withdrawn by operator decision, Task 3 because
the mode being documented no longer exists.

## Decision coverage (D-01..D-27)

| Decision | Proven by |
|----------|-----------|
| D-01..D-04 (unset rule) | `MergePredicateTests` (61 unit tests) |
| D-05..D-07 (content merge scope) | `ContentDeserializerSeedMergeTests` (19 tests) |
| D-08 (shared helper) | `MergePredicateTests` + `SqlTableProviderSeedMergeTests` consumer |
| D-09 (no persisted marker, intrinsic idempotency) | `ContentDeserializerSeedMergeTests` (second-pass no-op test) + `SqlTableProviderSeedMergeTests` (second-pass zero-fill test) |
| D-10 (type-default overwrite tradeoff) | `MergePredicateTests` (bool/int/DateTime default coverage) + `ContentDeserializerSeedMergeTests` (fill-on-target-default test) |
| D-11 (new log format) | `ContentDeserializerSeedMergeTests` + `SqlTableProviderSeedMergeTests` assert `"Seed-merge:"` line presence and zero `"Seed-skip:"` occurrences in both producers |
| D-12 (schema drift) | `SqlTableProviderSeedMergeTests` (TargetSchemaCache interaction test) |
| D-13..D-14 (test shape, TDD) | Eight atomic RED→GREEN commits across 39-01 and 39-02 |
| D-15 (live E2E gate) | Deferred — re-routed to manual operator verification on a freshly-prepared empty DB (see "Manual verification protocol" above) |
| D-16 (Phase 37 D-06 untouched) | No edits to `.planning/phases/37-*/*` |
| D-17 (read-then-narrowed-UPDATE) | `SqlTableWriterUpdateSubsetTests` (11 tests) + `SqlTableProviderSeedMergeTests` (merge-plan construction tests) |
| D-18 (checksum fast-path first) | `SqlTableProviderSeedMergeTests` (checksum-hit short-circuits merge branch) |
| D-19 (dry-run per-field diff) | `ContentDeserializerSeedMergeTests` (`LogSeedMergeDryRun` output shape) |
| D-20 (no admin UI changes) | Files-touched audit: zero admin-UI files in any 39-01/39-02 commit |
| D-21 (XML column inventory) | `EcomXmlMergeTests` fixtures + `SqlTableProvider.IsXmlColumn` helper |
| D-22..D-25 (XML-element merge) | `XmlMergeHelperTests` (18 tests) + `EcomXmlMergeTests` (11 tests) |
| D-26 (dry-run XML-element diff) | `XmlMergeHelper.MergeWithDiagnostics` test coverage in `XmlMergeHelperTests` |
| D-27 (XML-merge placement) | Planner landed `XmlMergeHelper` in `Infrastructure/` namespace per decision |

## Follow-ups for future sessions

- **Manual D-15 verification** on empty-DB target once the operator
  prepares one — captures the live-round-trip evidence that this plan
  could not capture through the bacpac+cleanup pipeline.
- **Phase 38.1 cleanup script 08 repair** — separate decimal phase
  (candidate: 38.2) to rewrite
  `tools/swift22-cleanup/08-null-orphan-page-link-refs.sql` so it stays
  under SQL Server's query-plan-complexity ceiling. Would unblock any
  future pipeline-wrapped live gate.
- **PROJECT.md Key Decisions update** — flip the "Source-wins conflict
  strategy" row to "Deploy = source-wins, Seed = field-level merge"
  per D-09/D-01 now that the merge behavior has shipped. Noted in
  39-CONTEXT.md §Deferred Ideas.

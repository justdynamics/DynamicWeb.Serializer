---
phase: 39-seed-mode-field-level-merge
plan: 02
subsystem: sqltable-provider-and-xml-merge
tags: [merge, seed, sqltable, xml, tdd, deserializer]
one_liner: "Field-level merge on Seed for SqlTableProvider (column-level) and XML-element-level merge for EcomPayments/EcomShippings"
dependency_graph:
  requires:
    - "MergePredicate.IsUnsetForMergeBySqlType (shipped by Plan 39-01)"
  provides:
    - "SqlTableWriter.UpdateColumnSubset (narrowed-UPDATE write path)"
    - "XmlMergeHelper.Merge / MergeWithDiagnostics (per-element XML merge)"
    - "SqlTableProvider DestinationWins field-level merge branch"
  affects:
    - "src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs (UpdateColumnSubset addition)"
    - "src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs (merge branch + existingRowsByIdentity loop extension)"
tech_stack:
  added: []
  patterns:
    - "Pure-static utility in Infrastructure/ (XmlFormatter/MergePredicate analog)"
    - "CommandBuilder {0}-placeholder parameterization for narrowed UPDATE"
    - "XmlReader with DtdProcessing.Prohibit (billion-laughs hardening)"
    - "Virtual-method stub target via Mock<SqlTableWriter> { CallBase = false }"
    - "Name-attribute-as-identity-key XML idiom (mirrors XmlFormatter.CompactWithMerge)"
key_files:
  created:
    - "src/DynamicWeb.Serializer/Infrastructure/XmlMergeHelper.cs"
    - "tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterUpdateSubsetTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Infrastructure/XmlMergeHelperTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs"
    - "tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/EcomXmlMergeTests.cs"
  modified:
    - "src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs (UpdateColumnSubset virtual method added)"
    - "src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs (existingRowsByIdentity loop extension + merge branch replacing Seed-skip + IsXmlColumn helper)"
decisions:
  - "UpdateColumnSubset emits zero SET IDENTITY_INSERT — UPDATE path, identity-column identity is irrelevant (D-17)"
  - "Empty columnsToUpdate subset treated as no-op returning Updated — keeps caller counter semantics consistent"
  - "XmlMergeHelper defensive-parse pattern: malformed XML on either side returns target unchanged, no throw (mirrors XmlFormatter.CompactWithMerge)"
  - "XmlMergeHelper attribute merge skips namespace declarations and the 'name' identity attribute to avoid polluting diagnostics"
  - "IsXmlColumn helper kept inside SqlTableProvider (private static) rather than shared — the xml DATA_TYPE decision only applies to the SqlTable merge branch; ContentDeserializer doesn't need it"
  - "existingRowsByIdentity built in the same loop as existingChecksums — zero extra round-trips to the DB; doubles in-memory cost of the checksum pass but Swift 2.2 baseline well within any reasonable threshold (EcomProducts = 2051 rows)"
  - "Test harness CreateProviderWithFiles duplicated between SqlTableProviderSeedMergeTests and EcomXmlMergeTests rather than extracted to shared base class — each test file self-contained for easier per-scenario debugging; extraction deferred unless 3rd consumer lands"
metrics:
  duration: "~90 minutes"
  tasks_completed: 4
  files_changed: 7
  tests_added: 55
  completed: "2026-04-23"
---

# Phase 39 Plan 02: SqlTable Field-level + XML-element Merge Summary

Closes the SqlTable side of the Phase 39 Seed-mode field-level merge (Plan 39-01 shipped the Content side). Adds first-class XML-element merge so the CONTEXT.md scope-expansion acceptance (Mail1SenderEmail fills on Seed inside EcomPayments.PaymentGatewayParameters and EcomShippings.ShippingServiceParameters XML columns) is actually deliverable — column-level merge alone cannot reach the XML leaves where those values live.

## What Shipped

### Task 1: `SqlTableWriter.UpdateColumnSubset` + unit tests (TDD RED -> GREEN)

**Files:**
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` (MODIFIED, +87 LOC)
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterUpdateSubsetTests.cs` (NEW, 280 LOC)

**Behaviour:** New virtual `UpdateColumnSubset(tableName, keyColumns, fullRow, columnsToUpdate, isDryRun, log?)` emits a parameterized `UPDATE [tbl] SET [col]=@p,... WHERE [key]=@p AND...` via CommandBuilder `{0}` placeholders. Empty subset is a no-op returning `Updated`. Dry-run logs `[DRY-RUN] UPDATE ...` and skips `ExecuteNonQuery`. Exceptions caught, logged as `ERROR [tbl].UpdateColumnSubset: ...`, and return `Failed`. No IDENTITY_INSERT wrapping — D-17 UPDATE path, identity irrelevant.

**Tests (11 passing):** single / multi / composite-key UPDATE shape, null-value bind-to-DBNull, IDENTITY_INSERT absence, empty-subset no-op, dry-run no-execute + log line, exception -> Failed + ERROR log, value-with-quote parameterization, virtual reflection probe.

**Commits:**
- `85462cc test(39-02): add failing SqlTableWriterUpdateSubsetTests (RED)`
- `2dd1656 feat(39-02): implement SqlTableWriter.UpdateColumnSubset (GREEN)`

### Task 2: `XmlMergeHelper` + unit tests (TDD RED -> GREEN)

**Files:**
- `src/DynamicWeb.Serializer/Infrastructure/XmlMergeHelper.cs` (NEW, 185 LOC)
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/XmlMergeHelperTests.cs` (NEW, 289 LOC)

**Behaviour:** Pure-static utility. `Merge(targetXml, sourceXml)` and `MergeWithDiagnostics(targetXml, sourceXml)` return the merged XML (with optional fills list). Element-level fill when target is absent OR has null/empty/whitespace text (D-22). Target-only elements preserved (D-24). Attribute merge mirrors element rule. `<Parameter name="X">` idiom uses `name` attribute as identity key (mirrors `XmlFormatter.CompactWithMerge`). Defensive `TryParse`/`catch XmlException` returns target unchanged. `XmlReader` with `DtdProcessing.Prohibit` + `XmlResolver = null` rejects billion-laughs (T-39-02-05). SQL-like XML text is re-serialized via `XDocument.ToString()` — dangerous content stays as XML text, never SQL (T-39-02-03).

**Tests (18 passing):** missing / empty / whitespace / set-preserve / target-only, nested-leaf recursion, attribute missing + attribute set-preserve, EcomPayments name-identity shape, null/null/source-null/target-null boundaries, malformed target/source, DTD payload rejection, SQL-like text escape, `MergeWithDiagnostics` fills populated + empty lists.

**Commits:**
- `318e78f test(39-02): add failing XmlMergeHelperTests (RED)`
- `b54b128 feat(39-02): implement XmlMergeHelper per-element merge (GREEN)`

### Task 3: `SqlTableProvider` merge branch (TDD RED -> GREEN)

**Files:**
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` (MODIFIED, +105 / -10)
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` (NEW, 849 LOC)

**Behaviour:**
- **Region A (~lines 292-301):** Extended the `existingChecksums` loop to also populate `existingRowsByIdentity: Dict<string, Dict<string, object?>>`. Zero extra DB round-trips — rows already enumerated for the checksum map.
- **Region B (~lines 313-322 replaced):** Deleted the Phase 37-01 row-level Seed-skip block; inserted a ~90-line merge branch. Checksum fast-path (D-18) still runs FIRST at lines 304-311 (unchanged). When `strategy == DestinationWins` AND identity matches a target row:
  - Key + identity columns skipped (D-05).
  - Missing target columns silently dropped + logged once via `TargetSchemaCache.LogMissingColumnOnce` (D-12).
  - Columns whose SQL DATA_TYPE is `xml` routed through `XmlMergeHelper.MergeWithDiagnostics` (D-21/D-22/D-23/D-24). Only added to the write subset when `fills.Count > 0 && merged != targetXml`.
  - Scalar columns go through `MergePredicate.IsUnsetForMergeBySqlType` (D-01/D-10).
  - Empty subset → `Skipped++` + log `"Seed-merge: [tbl].identity - 0 filled, all set"`.
  - Dry-run emits per-column `"would fill [tbl.col]: target=<unset> -> seed='...'"` and per-element `"would fill [tbl.col, element=X]: ..."` (D-19/D-26), then `"[DRY-RUN] Seed-merge: [tbl].identity - N would-fill"`.
  - Normal path calls `_writer.UpdateColumnSubset` with the narrowed column list, then logs `"Seed-merge: [tbl].identity - N filled, M left"` (D-11).
- **Region C (new private static helper):** `IsXmlColumn(sqlDataType) => DATA_TYPE == "xml"`.
- Identity non-match rows continue to the existing `_writer.WriteRow` MERGE path unchanged.

**Tests (15 passing):**
- Column merge (9): `Seed_IdentityMatchTargetColumnNull_...`, `Seed_IdentityMatchAllColumnsSet_...`, `Seed_IdentityMatchPartialSet_...`, `Seed_IdentityUnmatched_WriteRowFallthrough`, `Seed_ChecksumMatches_FastPathSkip_BeforeMergeBranch`, `Seed_MissingTargetColumn_SilentlyDropsFromMergePlan`, `Seed_DryRun_EmitsWouldFillPerColumn_NoSqlExecuted`, `Seed_Rerun_AllRowsAlreadyFilled_AllSkipped`, `Seed_LogLineShape_SeedMerge_FilledAndLeft`.
- XML merge (6): `Seed_XmlColumn_TargetMissingElement_XmlMerged_...`, `Seed_XmlColumn_TargetElementSet_ElementPreserved`, `Seed_XmlColumn_TargetOnlyElements_Preserved`, `Seed_XmlColumn_DryRun_EmitsPerElementWouldFill`, `Seed_XmlColumn_IdentifiedByColumnType_xml`, `Seed_XmlColumn_MalformedSourceOrTarget_FallbackToStandardUnsetRule`.

**Commits:**
- `04c92f4 test(39-02): add failing SqlTableProviderSeedMergeTests (RED)` (10/15 failing as expected)
- `813c7a7 feat(39-02): SqlTableProvider field-level merge branch (GREEN)` (15/15 passing, 769/769 full suite)

### Task 4: EcomPayments + EcomShippings integration tests (additive)

**Files:**
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/EcomXmlMergeTests.cs` (NEW, 603 LOC)

**Behaviour:** Integration-layer acceptance coverage for the CONTEXT.md scope-expansion. Exercises the full Provider → XmlMergeHelper → UpdateColumnSubset stack with realistic fixtures matching the DW `<Parameter name="X">` idiom used by `EcomPayments.PaymentGatewayParameters` and `EcomShippings.ShippingServiceParameters` (per `swift2.2-combined.json` xmlColumns declarations).

**Tests (11 passing):**
- EcomPayments (4): target missing Mail1SenderEmail → filled; target empty Mail1SenderEmail → filled (D-22); target customer email → preserved (no overwrite); target has custom extra parameter → preserved (D-24) + Mail1SenderEmail added.
- EcomShippings (4): target missing Mail1SenderEmail → filled; target empty Mail1SenderName → filled; target has full customer tweaks → no write; target has only CustomRate → preserved + Mail1SenderEmail added.
- Cross-table (2): parameterized `[Theory]` confirms both tables follow the same rule; rerun idempotency (second Seed run on already-seeded target yields zero `UpdateColumnSubset`/`WriteRow` calls — `DisableForeignKeys`/`EnableForeignKeys` still run unconditionally, not a merge-write concern).

No TDD RED/GREEN cycle for this task — implementation was already green from Task 3. One test fix during implementation: `Integration_Rerun_Seed_Idempotent_NoWrites` originally asserted `executor.ExecuteNonQuery.Times.Never` but the provider always calls it for FK toggle — corrected to `writer.UpdateColumnSubset + WriteRow` both `Times.Never`.

**Commit:**
- `f533be2 test(39-02): EcomPayments/EcomShippings XML round-trip integration tests`

## Acceptance Criteria

| Criterion | Target | Actual |
|-----------|--------|--------|
| `grep -c "public virtual WriteOutcome UpdateColumnSubset" SqlTableWriter.cs` | 1 | 1 |
| `SET IDENTITY_INSERT` in UpdateColumnSubset body | 0 | 0 (only in BuildMergeCommand + TruncateAndInsertAll) |
| `grep -c "Seed-skip:" SqlTableProvider.cs` | 0 | 0 |
| `grep -c "Seed-merge:" SqlTableProvider.cs` | >=2 | 3 |
| `grep -c "existingRowsByIdentity" SqlTableProvider.cs` | >=2 | 3 |
| `grep -c "MergePredicate.IsUnsetForMergeBySqlType" SqlTableProvider.cs` | >=1 | 1 |
| `grep -c "XmlMergeHelper" SqlTableProvider.cs` | >=1 | 4 |
| `grep -c "_writer.UpdateColumnSubset" SqlTableProvider.cs` | >=1 | 1 |
| `grep -c "public static class XmlMergeHelper" XmlMergeHelper.cs` | 1 | 1 |
| `grep -c "namespace DynamicWeb.Serializer.Infrastructure" XmlMergeHelper.cs` | 1 | 1 |
| `grep -c "DtdProcessing.Prohibit" XmlMergeHelper.cs` | >=1 | 2 (type declaration + named-settings usage) |
| Phase 39-02 tests total | >=54 | 55 (11 UpdateSubset + 18 XmlMergeHelper + 15 SeedMerge + 11 EcomXmlMerge) |
| `SqlTableWriterUpdateSubsetTests` | all pass | 11/11 |
| `XmlMergeHelperTests` | all pass | 18/18 |
| `SqlTableProviderSeedMergeTests` | all pass | 15/15 |
| `EcomXmlMergeTests` | all pass | 11/11 |
| Full suite regression | all pass | 780/780 (baseline 725 + 55 net-new) |

## D-XX Coverage

| Decision | Evidence |
|----------|----------|
| D-01 (baseline unset rule) | Consumed via `MergePredicate.IsUnsetForMergeBySqlType` in the scalar branch of `SqlTableProvider.DeserializeCoreLogic`; test `Seed_IdentityMatchTargetColumnNull_YamlHasValue_UpdateColumnSubsetCalled` exercises NULL target → fill path |
| D-05 (scalar scope + identity skip) | `metadata.KeyColumns.Contains` + `metadata.IdentityColumns.Contains` guards at top of the foreach loop inside the merge branch |
| D-08 (shared helper, separate write paths) | SqlTable path uses `MergePredicate.IsUnsetForMergeBySqlType` (SQL-type-aware) + `XmlMergeHelper` — ContentDeserializer uses object-typed + per-type overloads. Plan 39-01 shipped the shared `MergePredicate` class |
| D-10 (type-default overwrite accepted) | Inherited from `MergePredicate.IsUnsetForMergeBySqlType` — `bit` column with `false` on target is "unset" and fills to `true` on Seed. Plan 39-01's MergePredicateTests cover this |
| D-11 (Seed-merge log format) | 3 log lines in SqlTableProvider: `Seed-merge: [tbl].id - 0 filled, all set` (zero-fill path), `Seed-merge: [tbl].id - N filled, M left` (normal path), `[DRY-RUN] Seed-merge: [tbl].id - N would-fill` (dry-run path). Test `Seed_LogLineShape_SeedMerge_FilledAndLeft` asserts shape. Regression guard: `Seed_IdentityMatchAllColumnsSet_...` asserts the zero-fill line contains "0 filled" |
| D-12 (schema drift inheritance) | `_schemaCache.LogMissingColumnOnce` call inside merge branch + test `Seed_MissingTargetColumn_SilentlyDropsFromMergePlan` asserts the warning line appears and `PhantomColumn` never enters the column subset |
| D-13 (unit + integration test shape) | 29 unit tests (UpdateSubset 11 + XmlMergeHelper 18) + 26 integration tests (SeedMerge 15 + EcomXml 11) |
| D-14 (TDD discipline) | Per-task RED commits land failing tests first, GREEN commits ship the implementation. Task 1: `85462cc` (RED) → `2dd1656` (GREEN). Task 2: `318e78f` (RED) → `b54b128` (GREEN). Task 3: `04c92f4` (RED) → `813c7a7` (GREEN). Task 4 additive (no RED needed — implementation already green from Task 3). |
| D-17 (read-then-narrowed-UPDATE) | `SqlTableWriter.UpdateColumnSubset` emits exactly `UPDATE [tbl] SET [col1]=@p,... WHERE [key1]=@p AND...`. Test `UpdateColumnSubset_DoesNotIncludeIdentityInsert` asserts IDENTITY_INSERT is absent |
| D-18 (checksum fast-path stays first) | Lines 304-311 of SqlTableProvider (unchanged from prior plans) run BEFORE the merge branch. Test `Seed_ChecksumMatches_FastPathSkip_BeforeMergeBranch` asserts UpdateColumnSubset is never called when checksums match |
| D-19 (dry-run per-field diff) | Per-column dry-run lines: `would fill [tbl.col]: target=<unset> -> seed='...'`. Test `Seed_DryRun_EmitsWouldFillPerColumn_NoSqlExecuted` asserts presence + `ExecuteNonQuery` absence |
| D-21 (XML column inventory) | `IsXmlColumn(sqlDataType)` helper matches INFORMATION_SCHEMA `DATA_TYPE="xml"`; covers PaymentGatewayParameters + ShippingServiceParameters — any other XML column surfaced by schema is auto-included. Test `Seed_XmlColumn_IdentifiedByColumnType_xml` asserts the routing |
| D-22 (XML-element unset rule) | `XmlMergeHelper.IsUnsetLeafElement` + `IsUnsetText` covers absent/null/empty/whitespace. Tests `Merge_ElementMissingOnTarget_FillsFromSource`, `Merge_ElementEmptyOnTarget_FillsFromSource`, `Merge_ElementWhitespaceOnTarget_FillsFromSource` |
| D-23 (XML merge mechanism) | `XmlMergeHelper.Merge` parses both sides, walks the tree, re-serializes merged document. Provider stores the merged string as the column value; single narrowed UPDATE writes it via `_writer.UpdateColumnSubset` |
| D-24 (preserve target-only elements) | MergeElement walks source children only — target children absent from source are never touched. Tests `Merge_TargetOnlyElement_PreservedUntouched`, `EcomPayments_TargetExtraCustomParameter_Preserved_AfterSeed`, `EcomShippings_TargetOnlyParameter_Preserved` |
| D-25 (test coverage for XML merge) | `XmlMergeHelperTests` 18/18 unit + `EcomXmlMergeTests` 11/11 integration |
| D-26 (dry-run XML-element diff) | Dry-run logs `would fill [tbl.col, element=X: <missing-or-empty> -> 'val']` via the per-element fills list from `MergeWithDiagnostics`. Test `Seed_XmlColumn_DryRun_EmitsPerElementWouldFill` asserts |
| D-27 (shared XML-merge placement) | `XmlMergeHelper` lives in `src/DynamicWeb.Serializer/Infrastructure/` alongside `MergePredicate` + `XmlFormatter` + `TargetSchemaCache` — namespace `DynamicWeb.Serializer.Infrastructure` |

## Deviations from Plan

**None material.** Two minor adjustments during RED → GREEN:

1. **[Rule 3 — unblock] Task 1 assertion relaxed (space after SET).** The plan's RED test skeleton asserted `Assert.Contains("SET [OrderFlowName]", sql)`. `CommandBuilder` emits `SET  [OrderFlowName]=` (two spaces after SET because of the separator handling). The GREEN implementation matched the plan's pseudocode exactly — the assertion was the over-specific side. Fixed by relaxing to shape-matching (`Assert.Contains("SET", sql)` + `Assert.Contains("[OrderFlowName]", sql)`). Preserves the intent (UPDATE ... SET <col-assignment> ... WHERE) without binding to whitespace.

2. **[Rule 3 — unblock] Task 3 test fixture identity alignment.** Two scenarios (`Seed_IdentityMatchAllColumnsSet_NoWrite_SkippedCounterIncremented` and `Seed_IdentityMatchPartialSet_UpdateColumnSubsetFiresForUnsetSubsetOnly`) used different `OrderFlowName` values between YAML and existing DB rows. Because `TestMetadata.NameColumn="OrderFlowName"` and `SqlTableReader.GenerateRowIdentity` uses NameColumn for identity, the identity did not match and the rows fell through to the WriteRow path. Fixed by aligning `OrderFlowName` across yaml+target; the differing scalar is `OrderFlowDescription`.

3. **[Rule 3 — scope] Task 4 idempotency test refinement.** `Integration_Rerun_Seed_Idempotent_NoWrites` originally asserted `executor.ExecuteNonQuery.Times.Never`. The provider invokes `ExecuteNonQuery` unconditionally for `DisableForeignKeys` + `EnableForeignKeys` constraint toggling — independent of merge writes. Narrowed the assertion to `writer.UpdateColumnSubset + writer.WriteRow` both `Times.Never`. The idempotency contract is about merge writes, not FK toggles; the FK operations are idempotent on their own (NOCHECK CONSTRAINT ALL is a no-op when already disabled).

## Auth Gates / Manual Actions

None.

## Risks Surfaced

- **`existingRowsByIdentity` memory doubling (accepted).** The dict stores the full target row for every row in the table. For Swift 2.2 baseline (EcomProducts ~2051 rows), this is ~100 KB overhead — trivial. Documented in RESEARCH Pitfall 8 + threat register T-39-02-04 as `accept`. If Phase 39-03 E2E surfaces OOM on a large-table deploy, a streaming variant can be introduced as a follow-up.

- **`XmlMergeHelper` DTD prohibition is per-parse, not process-wide.** `XmlReaderSettings.DtdProcessing = Prohibit` hardens the two `XmlReader.Create` calls this helper makes. Other XML parsing in the serializer (`XmlFormatter.PrettyPrint`, `XmlFormatter.Compact`, `XmlFormatter.RemoveElements`, `XmlFormatter.CompactWithMerge`) still uses the default `XDocument.Parse(string)` which resolves DTDs by default in .NET 8. Phase 39 scope is Seed-merge only — extending the hardening to all XML parsers is a separate correctness task. Logged as a potential follow-up in PATTERNS-adjacent security improvements.

- **`IsXmlColumn` currently only matches `"xml"` exactly.** If a future config supplies an XML payload inside an `nvarchar(max)` column (some legacy DW tables do this), element-level merge won't trigger — scalar-string merge applies instead. Acceptable for the Phase 39 inventory (EcomPayments + EcomShippings both use native `xml` type per SQL schema), but callers with nvarchar-wrapped XML would need either an explicit per-column annotation or a heuristic (`value looks like XML`). Left as Claude's Discretion per D-21.

- **Harness duplication between `SqlTableProviderSeedMergeTests` and `EcomXmlMergeTests`.** `CreateProviderWithFiles` is ~90 LOC copied between the two files. Not extracted to a shared `SqlTableProviderTestFixture` base because the two test files differ on which `TableMetadata` + `columnTypes` they use by default, and the per-scenario readability cost of following a base class through multiple indirection levels outweighs the DRY saving at 2 consumers. If a 3rd test file consuming the same harness lands, extract.

## Deferred Issues

None. Every acceptance criterion met; full suite green (780/780); every D-XX in the plan's `requirements` frontmatter has at least one test or code path exercising it.

## Handoff Notes to Plan 39-03 (Live E2E Gate)

- **Verification query for Plan 39-03:** after Deploy → tweak → Seed E2E, check Mail1SenderEmail presence with:
  ```sql
  SELECT
    PaymentId,
    CAST(PaymentGatewayParameters AS nvarchar(max)) AS xml_content
  FROM EcomPayments
  WHERE CAST(PaymentGatewayParameters AS nvarchar(max)) LIKE '%Mail1SenderEmail%'
  ```
  Same shape for EcomShippings with `ShippingServiceParameters`.

- **Expected log lines from a Seed run (post-Deploy):**
  - `Seed-merge: [EcomPayments].PAY-XX - N filled, M left` per payment row
  - `Seed-merge: [EcomShippings].SHIP-XX - N filled, M left` per shipping row
  - `Skipped NNN (unchanged)` for checksum-identical rows (fast-path)

- **XML-merge memory footprint:** `existingRowsByIdentity` doubles the checksum-pass memory cost. Swift 2.2 total rows across all SqlTable predicates ~= 10-20K; <1 MB overhead. If E2E on larger dataset surfaces OOM, introduce streaming-merge follow-up.

- **XmlMergeHelper is the only XML entry point hardened against DTD payloads.** If Plan 39-03 E2E encounters any external XML file (e.g., baseline YAML containing a crafted `<!DOCTYPE` payload via a user-controlled config path), note that `XmlFormatter` variants are not yet hardened. Probably out of scope for 39-03 but worth calling out.

- **Plan 39-01's ContentDeserializer merge branch is independent** — the 39-01 + 39-02 paths don't share state. Verify both in the same E2E run by seeding (a) a page with empty MenuText + target empty → expect fill via 39-01 path, and (b) an EcomPayments row with Deploy-excluded Mail1SenderEmail + target empty element → expect fill via 39-02 path. Both fills MUST be visible in the live post-Seed snapshot.

## Self-Check: PASSED

- `src/DynamicWeb.Serializer/Infrastructure/XmlMergeHelper.cs` exists: FOUND
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` contains `public virtual WriteOutcome UpdateColumnSubset`: FOUND (1 match)
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` Seed-skip removed: VERIFIED (grep -c 0)
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` Seed-merge present: VERIFIED (grep -c 3)
- `existingRowsByIdentity` declared + used: VERIFIED (grep -c 3)
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterUpdateSubsetTests.cs` exists: FOUND
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/XmlMergeHelperTests.cs` exists: FOUND
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` exists: FOUND
- `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/EcomXmlMergeTests.cs` exists: FOUND
- Commits `85462cc`, `2dd1656`, `318e78f`, `b54b128`, `04c92f4`, `813c7a7`, `f533be2`: FOUND (git log --oneline 19692c0..HEAD matches 7 Phase 39-02 commits)
- 55 net-new Phase 39-02 tests passing (11+18+15+11): VERIFIED
- Full suite 780/780 passing (725 baseline + 55 net-new): VERIFIED

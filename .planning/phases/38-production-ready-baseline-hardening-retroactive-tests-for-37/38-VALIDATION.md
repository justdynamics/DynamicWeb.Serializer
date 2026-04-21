---
phase: 38
slug: production-ready-baseline-hardening-retroactive-tests-for-37
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-21
---

# Phase 38 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 + Moq 4.20.72 (.NET 8.0) |
| **Config file** | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| **Quick run command** | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "Category!=Integration" --nologo` |
| **Full suite command** | `dotnet test --nologo` |
| **Estimated runtime** | ~30 seconds quick, ~2-3 min full with integration |

---

## Sampling Rate

- **After every task commit:** Run quick command (`dotnet test ... --filter "Category!=Integration"`)
- **After every plan wave:** Run full suite (`dotnet test --nologo`)
- **Before `/gsd-verify-work`:** Full suite must be green AND live E2E round-trip (Swift 2.2 → CleanDB) must pass with `strictMode: true`
- **Max feedback latency:** 30 seconds (quick), 180 seconds (full)

---

## Per-Task Verification Map

> Populated by gsd-planner after PLAN.md files are produced. Use the 14 backlog IDs (A.1–A.3, B.1–B.5, C.1, D.1–D.3, E.1–E.2) plus D-38-16 final as anchors. Planner must map each generated task to the row.

| Task ID | Plan | Wave | Backlog ID | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 38-01-01 | 01 | 1 | D.1 | — | `?mode=seed` query param binds identically to JSON body | unit+integration | `dotnet test --filter FullyQualifiedName~SerializerSerializeCommandTests.QueryParamMode` | ❌ W0 | ⬜ pending |
| 38-01-02 | 01 | 1 | D.2 | — | 0-error serialize returns HTTP 200 | integration | `dotnet test --filter FullyQualifiedName~SerializerSerializeCommandTests.ZeroErrorsReturns200` | ❌ W0 | ⬜ pending |
| 38-01-03 | 01 | 1 | E.1 | — | `docs/baselines/Swift2.2-baseline.md` has "Pre-existing source-data bugs" section | docs-grep | `grep -q "## Pre-existing source-data bugs" docs/baselines/Swift2.2-baseline.md` | ✅ | ⬜ pending |
| 38-01-04 | 01 | 1 | E.2 | — | `docs/baselines/env-bucket.md` exists with required sections | docs-grep | `test -f docs/baselines/env-bucket.md && grep -q "GlobalSettings.config" docs/baselines/env-bucket.md` | ❌ | ⬜ pending |
| 38-02-01 | 02 | 2 | A.3 | T-38-01 (consolidation) | `AcknowledgedOrphanPageIds` exists ONLY on ProviderPredicateDefinition | build+grep | `dotnet build && ! grep -rn "ModeConfig.*AcknowledgedOrphanPageIds" src/` | ✅ | ⬜ pending |
| 38-02-02 | 02 | 2 | A.1 | T-38-02 (malicious ID) | 3 tests: malicious-reject / acknowledged-warn / unlisted-still-fatal | unit | `dotnet test --filter FullyQualifiedName~BaselineLinkSweeperAcknowledgmentTests` | ❌ W0 | ⬜ pending |
| 38-02-03 | 02 | 2 | A.2 | T-38-03 (identity insert) | Area create fails if IDENTITY_INSERT wrapping removed | integration | `dotnet test --filter FullyQualifiedName~AreaIdentityInsertTests` | ❌ W0 | ⬜ pending |
| 38-02-04 | 02 | 2 | B.5 | — | Paragraph-anchor fixture: `#ValidParaId` passes, `#UnknownParaId` flagged | unit | `dotnet test --filter FullyQualifiedName~BaselineLinkSweeperParagraphAnchorTests` | ❌ W0 | ⬜ pending |
| 38-03-01 | 03 | 3 | B.1/B.2 | — | SQL cleanup script nulls all references to 3 stale template names | SQL+E2E | Run `tools/swift22-cleanup/04-null-stale-template-refs.sql` + verify strict-mode E2E emits no B.1/B.2 warnings | ❌ | ⬜ pending |
| 38-03-02 | 03 | 3 | B.3 | — | CleanDB on current DW version OR allowlist suppresses 3 Area schema-drift warnings in strict mode | E2E+grep | Strict-mode serialize of Swift 2.2 produces zero `SchemaDriftEscalator` warnings on Area | ❌ | ⬜ pending |
| 38-03-03 | 03 | 3 | B.4 | — | FK re-enable on EcomShopGroupRelation: documented as purge-only OR write-order fixed | E2E | Strict-mode deserialize on fresh Azure SQL (no purge) emits zero FK warnings | ❌ | ⬜ pending |
| 38-03-04 | 03 | 3 | C.1 | T-38-04 (silent data loss) | `SerializeResult.RowsSerialized == filesWritten == SourceRowCount` for EcomProducts with duplicate names | integration | `dotnet test --filter FullyQualifiedName~FlatFileStoreDeduplicationTests` + Swift 2.2 EcomProducts round-trip = 2051 files | ❌ W0 | ⬜ pending |
| 38-04-01 | 04 | 4 | D.3 | — | `tools/smoke/` exits 0 when CleanDB serves all active pages as 2xx/3xx, non-zero on any 5xx | tool-test | `tools/smoke/run-smoke.ps1` against live CleanDB exits 0 | ❌ | ⬜ pending |
| 38-05-01 | 05 | 5 (final) | D-38-16 | — | `strictMode: true` restored in swift2.2-combined.json; full E2E passes | E2E | Swift 2.2 → CleanDB round-trip with config-default strict mode, zero escalated warnings | ❌ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Create test scaffolding before Wave 1 begins:

- [ ] `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperAcknowledgmentTests.cs` — stubs for A.1 (3 scenarios)
- [ ] `tests/DynamicWeb.Serializer.Tests/Infrastructure/BaselineLinkSweeperParagraphAnchorTests.cs` — stubs for B.5
- [ ] `tests/DynamicWeb.Serializer.Tests/Serialization/AreaIdentityInsertTests.cs` — stubs for A.2 (requires ISqlExecutor seam per research §A.2)
- [ ] `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/FlatFileStoreDeduplicationTests.cs` — stubs for C.1 (duplicate-name rows)
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerSerializeCommandTests.cs` — stubs for D.1 + D.2
- [ ] Verify `DynamicWeb.Serializer.Tests.csproj` references the infrastructure + provider test collections; xUnit discovers new files automatically — no config edit needed

---

## Manual-Only Verifications

| Behavior | Backlog ID | Why Manual | Test Instructions |
|----------|-----------|------------|-------------------|
| Live Swift 2.2 → CleanDB round-trip with `strictMode: true` produces zero escalated warnings | D-38-16 | Requires live DW host instances (Swift 2.2 @ :54035, CleanDB @ :58217) — not reproducible in CI without the full DB + Azure host stack | 1. Run `tools/purge-cleandb.sql` against CleanDB. 2. Run `tools/swift22-cleanup/*.sql` against Swift 2.2. 3. `curl POST /Admin/Api/SerializerSerialize` on Swift 2.2 host (JSON body with combined config, no strictMode override). 4. `curl POST /Admin/Api/SerializerDeserialize` on CleanDB. 5. Assert: both return HTTP 200, `Errors: []`, EcomProducts row count == 2051 on target. |
| SerializerSmoke tool (D.3) against live CleanDB frontend | D.3 | Hits live HTTP endpoints; not containerizable | After step 5 above, run `tools/smoke/run-smoke.ps1 -Host http://localhost:58217 -AreaId 3`. Exit 0 expected. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (6 test files above)
- [ ] No watch-mode flags in automated commands
- [ ] Feedback latency < 30s (quick) / 180s (full)
- [ ] Live E2E checklist attached for manual pre-verification sign-off (D-38-16)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

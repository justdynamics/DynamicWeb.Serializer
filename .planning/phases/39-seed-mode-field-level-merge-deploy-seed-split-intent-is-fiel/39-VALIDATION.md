---
phase: 39
slug: seed-mode-field-level-merge
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-22
---

# Phase 39 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (.NET 8) |
| **Config file** | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| **Quick run command** | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~Merge" --nologo --no-restore` |
| **Full suite command** | `dotnet test tests/DynamicWeb.Serializer.Tests --nologo` |
| **Estimated runtime** | ~30–60 seconds (unit + integration, excluding E2E) |

E2E gate (run once per phase, not per task):
- **Command:** `pwsh tools/e2e/full-clean-roundtrip.ps1 -Mode DeployThenTweakThenSeed`
- **Runtime:** ~5–10 minutes (live Swift 2.2 → CleanDB round-trip)

---

## Sampling Rate

- **After every task commit:** Run quick command (Merge filter) — returns in <30s
- **After every plan wave:** Run full suite command
- **Before `/gsd-verify-work`:** Full suite green + E2E gate green
- **Max feedback latency:** 30 seconds (quick), 60 seconds (full)

---

## Per-Task Verification Map

Populated per-plan during execution. Expected skeleton:

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 39-01-01 | 01 | 0 | D-01,D-02,D-03 | — | N/A (pure helper) | unit | `dotnet test --filter "FullyQualifiedName~MergePredicateTests"` | ❌ W0 | ⬜ pending |
| 39-01-02 | 01 | 1 | D-01,D-02,D-03,D-04,D-05,D-07,D-11,D-19 | — | Customer tweaks preserved on re-Seed (no unauthorized overwrite) | integration | `dotnet test --filter "FullyQualifiedName~ContentDeserializerSeedMergeTests"` | ❌ W0 | ⬜ pending |
| 39-02-01 | 02 | 0 | D-17 | — | Narrowed UPDATE writes only requested columns | unit | `dotnet test --filter "FullyQualifiedName~SqlTableWriterUpdateSubsetTests"` | ❌ W0 | ⬜ pending |
| 39-02-02 | 02 | 1 | D-01,D-10,D-11,D-12,D-17,D-18,D-19 | — | Seed merge honors checksum fast-path; schema drift tolerated | integration | `dotnet test --filter "FullyQualifiedName~SqlTableProviderSeedMergeTests"` | ❌ W0 | ⬜ pending |
| 39-02-03 | 02 | 1 | D-21,D-22,D-23,D-24,D-25 | — | XML-element merge fills Mail1SenderEmail without stripping target-only elements | unit+integration | `dotnet test --filter "FullyQualifiedName~XmlMergeHelperTests|FullyQualifiedName~EcomXmlMergeTests"` | ❌ W0 | ⬜ pending |
| 39-03-01 | 03 | 2 | D-15 (acceptance) | — | Deploy→tweak→Seed preserves customer tweaks AND fills Mail1SenderEmail | E2E | `pwsh tools/e2e/full-clean-roundtrip.ps1 -Mode DeployThenTweakThenSeed` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Plans finalize the exact task-to-decision mapping during planning. The
skeleton above reflects the expected shape; planner may split/combine tasks.

---

## Wave 0 Requirements

- [ ] `tests/DynamicWeb.Serializer.Tests/Merge/MergePredicateTests.cs` — unit coverage of `IsUnsetForMerge` per D-01 type matrix (NULL, "", 0, false, DateTime.MinValue, Guid.Empty, positive cases)
- [ ] `tests/DynamicWeb.Serializer.Tests/Serialization/ContentDeserializerSeedMergeTests.cs` — integration coverage of page/ItemField/PropertyItem/sub-object DTO/paragraph-recursion merge scenarios (D-02..D-07)
- [ ] `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableWriterUpdateSubsetTests.cs` — unit coverage of narrowed-UPDATE SQL shape (D-17)
- [ ] `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs` — integration coverage of merge branch + checksum fast-path interaction (D-10, D-18) + schema drift (D-12)
- [ ] `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/XmlMergeHelperTests.cs` — unit coverage of XML-element merge per D-22..D-25 (element-missing fill, element-empty fill, element-set skip, attribute merge, target-only preservation, nested-element merge, whitespace handling)
- [ ] `tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/EcomXmlMergeTests.cs` — integration coverage of round-trip on `EcomPayments.PaymentGatewayParameters` and `EcomShippings.ShippingServiceParameters` with `Mail1SenderEmail` as the canonical fixture
- [ ] `tools/e2e/full-clean-roundtrip.ps1` — extend with `Mode: DeployThenTweakThenSeed` pathway (D-15)

No new test framework install required — existing xUnit + Moq + DW test fixtures cover the full matrix.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Dry-run per-field diff output readability (D-19, D-26) | D-19, D-26 | Human-readable log wording, not machine-verifiable | Run `/serializer/deserialize?dryRun=true` via admin UI on Swift 2.2 target with post-Deploy state; inspect log for `"would fill [col=..., element=Mail1SenderEmail]: target=<missing> → seed='...'"` shape |
| Admin UI summary shows new Seed-merge log line shape (D-11) | D-11 | Visual check of admin UI response area | Trigger Seed run from admin UI, confirm summary shows `"Seed-merge: <identity> — N fields filled, M left"` per row/page rather than old "Seed-skip" line |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s (quick) / 60s (full)
- [ ] E2E gate captured as Plan 39-03 or final task in Plan 39-02
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

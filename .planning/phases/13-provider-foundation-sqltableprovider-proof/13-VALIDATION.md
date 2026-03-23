---
phase: 13
slug: provider-foundation-sqltableprovider-proof
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-23
---

# Phase 13 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x (existing project test infrastructure) |
| **Config file** | `src/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj` |
| **Quick run command** | `dotnet test --filter "Category=Phase13" --no-build` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Phase13" --no-build`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 13-01-01 | 01 | 1 | PROV-01 | unit | `dotnet test --filter "ISerializationProvider"` | ❌ W0 | ⬜ pending |
| 13-01-02 | 01 | 1 | PROV-02 | unit | `dotnet test --filter "ProviderRegistry"` | ❌ W0 | ⬜ pending |
| 13-02-01 | 02 | 1 | SQL-01 | unit | `dotnet test --filter "DataGroupReader"` | ❌ W0 | ⬜ pending |
| 13-02-02 | 02 | 1 | SQL-01 | unit | `dotnet test --filter "FlatFileStore"` | ❌ W0 | ⬜ pending |
| 13-03-01 | 03 | 2 | SQL-01, SQL-02 | integration | `dotnet test --filter "SqlTableProvider"` | ❌ W0 | ⬜ pending |
| 13-03-02 | 03 | 2 | SQL-04 | unit | `dotnet test --filter "SerializeResult"` | ❌ W0 | ⬜ pending |
| 13-03-03 | 03 | 2 | SQL-05 | integration | `dotnet test --filter "SourceWins"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Test stubs for provider interface, registry, DataGroupReader, FlatFileStore, SqlTableProvider
- [ ] ISqlExecutor mock interface for testing without live database
- [ ] Sample DataGroup XML fixture for EcomOrderFlow

*Existing xUnit infrastructure covers framework needs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| EcomOrderFlow round-trip on live DW instance | SQL-01, SQL-02, SQL-05 | Requires running DW instance with ecommerce data | 1. Start Swift 2.2 instance 2. Serialize EcomOrderFlow 3. Verify YAML files in _sql/EcomOrderFlow/ 4. Deserialize into Swift 2.1 5. Verify rows match |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

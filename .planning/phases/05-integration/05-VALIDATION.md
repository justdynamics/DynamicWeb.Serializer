---
phase: 05
slug: integration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-19
---

# Phase 05 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.x with Dynamicweb.ContentSync.Tests and IntegrationTests |
| **Config file** | `tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Dynamicweb.ContentSync.Tests` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~15 seconds (unit), ~60 seconds (integration with live DW) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Dynamicweb.ContentSync.Tests`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 05-01-01 | 01 | 1 | INF-01 | build | `dotnet build src/Dynamicweb.ContentSync` | ✅ | ⬜ pending |
| 05-01-02 | 01 | 1 | OPS-03 | build | `dotnet build src/Dynamicweb.ContentSync` | ✅ | ⬜ pending |
| 05-02-01 | 02 | 2 | OPS-01, OPS-02 | integration | `dotnet build tests/Dynamicweb.ContentSync.IntegrationTests` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Integration test stubs in `tests/Dynamicweb.ContentSync.IntegrationTests/Integration/`
- Existing xunit infrastructure and test patterns cover shared needs

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Tasks visible in DW admin | OPS-01, OPS-02 | Requires running DW instance with UI | Deploy DLL, open admin, verify tasks appear in scheduled tasks list |
| NuGet package installable | INF-01 | Requires NuGet feed or local install | `dotnet pack`, then add to test project as package reference |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

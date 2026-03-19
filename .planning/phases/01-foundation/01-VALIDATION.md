---
phase: 1
slug: foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-19
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.3 |
| **Config file** | tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj |
| **Quick run command** | `dotnet test --filter "Category=Unit"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test --filter "Category=Unit"`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | SER-01 | unit | `dotnet test --filter "Category=Unit"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | SER-02 | unit | `dotnet test --filter "Category=Unit"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | SER-04 | unit | `dotnet test --filter "Category=Unit"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/Dynamicweb.ContentSync.Tests/` — test project with xunit 2.9.3
- [ ] YAML round-trip fidelity test stubs for SER-01
- [ ] Mirror-tree file I/O test stubs for SER-02
- [ ] Deterministic output test stubs for SER-04

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

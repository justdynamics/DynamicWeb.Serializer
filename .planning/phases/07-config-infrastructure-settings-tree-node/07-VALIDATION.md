---
phase: 7
slug: config-infrastructure-settings-tree-node
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-21
---

# Phase 7 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing test project) |
| **Config file** | `src/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj` |
| **Quick run command** | `dotnet test src/Dynamicweb.ContentSync.Tests --filter "Category=Config"` |
| **Full suite command** | `dotnet test src/Dynamicweb.ContentSync.Tests` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test src/Dynamicweb.ContentSync.Tests --filter "Category=Config"`
- **After every plan wave:** Run `dotnet test src/Dynamicweb.ContentSync.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 07-01-01 | 01 | 1 | CFG-01 | unit | `dotnet test --filter "ConfigWriter"` | ❌ W0 | ⬜ pending |
| 07-01-02 | 01 | 1 | CFG-02 | unit | `dotnet test --filter "ConfigLoader"` | ✅ | ⬜ pending |
| 07-01-03 | 01 | 1 | CFG-03 | unit | `dotnet test --filter "ConfigValidation"` | ❌ W0 | ⬜ pending |
| 07-02-01 | 02 | 1 | UI-01 | manual | N/A (DW admin visual) | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `ConfigWriterTests.cs` — stubs for atomic write, round-trip, validation
- [ ] Config test fixtures — sample valid/invalid JSON config files

*Existing test infrastructure covers framework setup.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Sync node visible in DW admin tree | UI-01 | Requires running DW instance with admin UI | Start DW instance, navigate to Settings > Content, verify Sync node appears and loads a screen |
| Clicking Sync node loads settings screen | UI-01 | Requires running DW instance | Click Sync node, verify screen renders without 404 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

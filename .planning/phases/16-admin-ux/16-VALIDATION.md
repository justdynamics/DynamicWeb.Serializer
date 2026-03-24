---
phase: 16
slug: admin-ux
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-24
---

# Phase 16 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x + Moq (existing) |
| **Config file** | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` (after rename) |
| **Quick run command** | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "Category=Phase16" --no-build` |
| **Full suite command** | `dotnet test tests/DynamicWeb.Serializer.Tests` |
| **Estimated runtime** | ~20 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/DynamicWeb.Serializer.Tests --filter "Category=Phase16" --no-build`
- **After every plan wave:** Run `dotnet test tests/DynamicWeb.Serializer.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 20 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 16-01-01 | 01 | 1 | REN-01 | build | `dotnet build src/DynamicWeb.Serializer` | N/A | ⬜ pending |
| 16-01-02 | 01 | 1 | REN-01 | unit | `dotnet test tests/DynamicWeb.Serializer.Tests` | N/A | ⬜ pending |
| 16-02-01 | 02 | 2 | UX-03 | unit | `dotnet test --filter "TreeNode"` | ❌ W0 | ⬜ pending |
| 16-02-02 | 02 | 2 | UX-01 | unit | `dotnet test --filter "LogViewer"` | ❌ W0 | ⬜ pending |
| 16-02-03 | 02 | 2 | UX-02 | unit | `dotnet test --filter "FileOverview"` | ❌ W0 | ⬜ pending |
| 16-02-04 | 02 | 2 | UX-04 | unit | `dotnet test --filter "ScheduledTask"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Test stubs for tree node relocation (parent ID, breadcrumb paths)
- [ ] Test stubs for log viewer (log file parsing, JSON header extraction, advice generation)
- [ ] Test stubs for file overview injector (zip gate, output dir check, dry-run flow)
- [ ] Shared fixtures: mock file system for log files, mock DW screen context

*Existing xUnit + Moq infrastructure covers framework needs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Tree node appears at Settings > Database > Serialize | UX-03 | Requires running DW instance with admin UI | Deploy to test instance, navigate to Settings > Database, verify "Serialize" node |
| Log viewer shows per-provider breakdown | UX-01 | Requires DW admin UI rendering | Run serialize, navigate to log viewer, verify table counts |
| "Import to database" action on zip files | UX-02 | Requires DW asset management UI | Upload a serialization zip, navigate to it, verify action appears |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 20s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

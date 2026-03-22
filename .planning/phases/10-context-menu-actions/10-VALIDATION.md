---
phase: 10
slug: context-menu-actions
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-22
---

# Phase 10 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (existing) |
| **Config file** | tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj |
| **Quick run command** | `dotnet build src/Dynamicweb.ContentSync --no-restore -v q` |
| **Full suite command** | `dotnet test tests/Dynamicweb.ContentSync.Tests --no-restore -v q` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/Dynamicweb.ContentSync --no-restore -v q`
- **After every plan wave:** Run `dotnet test tests/Dynamicweb.ContentSync.Tests --no-restore -v q`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 10-01-01 | 01 | 1 | ACT-04 | build | `dotnet build` | N/A | ⬜ pending |
| 10-01-02 | 01 | 1 | ACT-02, ACT-03 | build | `dotnet build` | N/A | ⬜ pending |
| 10-02-01 | 02 | 1 | ACT-06, ACT-07 | build | `dotnet build` | N/A | ⬜ pending |
| 10-02-02 | 02 | 1 | ACT-06, ACT-07, ACT-08 | build | `dotnet build` | N/A | ⬜ pending |
| 10-03-01 | 03 | 2 | ACT-01, ACT-05 | build | `dotnet build` | N/A | ⬜ pending |
| 10-03-02 | 03 | 2 | ALL | manual | human verification | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. No new test framework or stub files needed. All ACT requirements are verified via manual testing in a running DW instance (context menu actions, file upload/download, browser behavior).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Serialize action in context menu | ACT-01 | DW admin UI rendering | Right-click any page in content tree, verify "Serialize" appears |
| Zip created with YAML files | ACT-02 | File I/O + DW integration | Click Serialize, verify zip downloads with YAML content |
| Browser download triggered | ACT-03 | Browser behavior | Click Serialize, verify file download dialog appears |
| Zip saved to export directory | ACT-04 | File I/O | Check configured export dir for zip file after serialize |
| Deserialize action in context menu | ACT-05 | DW admin UI rendering | Right-click any page, verify "Deserialize" appears |
| Upload prompt appears | ACT-06 | UI modal behavior | Click Deserialize, verify file upload + mode selection modal |
| Three mode choices available | ACT-07 | UI rendering | Verify overwrite/children/sibling options in modal |
| Reuses existing serializer | ACT-08 | Architecture | Code review: verify ContentSerializer/ContentDeserializer reuse |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-03-22 (all requirements manual-only per research)

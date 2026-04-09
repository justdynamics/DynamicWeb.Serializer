---
phase: 32
slug: config-schema-extension
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-09
---

# Phase 32 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + custom test harness |
| **Config file** | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| **Quick run command** | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ConfigLoader"` |
| **Full suite command** | `dotnet test tests/DynamicWeb.Serializer.Tests` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ConfigLoader"`
- **After every plan wave:** Run `dotnet test tests/DynamicWeb.Serializer.Tests`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 32-01-01 | 01 | 1 | CFG-01 | — | N/A | unit | `dotnet test --filter "FullyQualifiedName~ConfigLoader"` | ✅ | ⬜ pending |
| 32-01-02 | 01 | 1 | CFG-02 | — | N/A | unit | `dotnet test --filter "FullyQualifiedName~ConfigLoader"` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements.*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

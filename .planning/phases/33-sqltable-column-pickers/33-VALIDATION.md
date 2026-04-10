---
phase: 33
slug: sqltable-column-pickers
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-11
---

# Phase 33 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + DW host manual testing |
| **Config file** | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| **Quick run command** | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~PredicateCommand"` |
| **Full suite command** | `dotnet test tests/DynamicWeb.Serializer.Tests` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~PredicateCommand"`
- **After every plan wave:** Run `dotnet test tests/DynamicWeb.Serializer.Tests`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 33-01-01 | 01 | 1 | PRED-04, PRED-05 | — | N/A | unit + manual | `dotnet build src/DynamicWeb.Serializer` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| CheckboxList renders with columns from SQL schema | PRED-04, PRED-05 | UI rendering requires DW host | Open SqlTable predicate edit screen, verify CheckboxList shows table columns |
| Selections persist on save | PRED-04, PRED-05 | Save round-trip needs admin UI | Check/uncheck columns, save, reload, verify state preserved |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

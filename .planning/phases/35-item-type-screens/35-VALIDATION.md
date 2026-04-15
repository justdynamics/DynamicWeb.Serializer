---
phase: 35
slug: item-type-screens
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-15
---

# Phase 35 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (unit tests for save command) + manual DW admin UI verification |
| **Config file** | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| **Quick run command** | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ItemType" --no-build` |
| **Full suite command** | `dotnet test tests/DynamicWeb.Serializer.Tests --no-build` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/DynamicWeb.Serializer && dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ItemType" --no-build`
- **After every plan wave:** Run `dotnet test tests/DynamicWeb.Serializer.Tests --no-build`
- **Before `/gsd-verify-work`:** Full test suite must pass
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 35-01-01 | 01 | 1 | ITEM-01 | — | N/A | build | `dotnet build src/DynamicWeb.Serializer` | ✅ | ⬜ pending |
| 35-01-02 | 01 | 1 | ITEM-01 | — | N/A | build | `dotnet build src/DynamicWeb.Serializer` | ✅ | ⬜ pending |
| 35-02-01 | 02 | 2 | ITEM-02, ITEM-03 | — | N/A | build | `dotnet build src/DynamicWeb.Serializer` | ✅ | ⬜ pending |
| 35-02-02 | 02 | 2 | ITEM-03 | — | N/A | unit | `dotnet build src/DynamicWeb.Serializer && dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~ItemTypeCommand" --no-build` | Wave 0 (created in task) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Test file created within Plan 02 Task 2 (TDD):

- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeCommandTests.cs` -- covers ITEM-03 (SaveItemTypeCommand save/update/validation)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Item Types tree node visible with category folders | ITEM-01 | DW admin UI rendering | Navigate to Settings > Database > Serialize, verify "Item Types" node with category subfolders |
| Live discovery populates list | ITEM-01 | Requires live DW with item types | Open Item Types list, verify all system item types appear |
| Edit screen shows fields as SelectMultiDual | ITEM-02 | DW CoreUI rendering | Click an item type, verify SelectMultiDual with field names |
| Read-only metadata (name, category, field count) | ITEM-02 | DW CoreUI rendering | Verify read-only fields on edit screen |
| Exclusions persist and apply | ITEM-03 | End-to-end config round-trip | Select fields, save, verify config JSON, run serialize |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved

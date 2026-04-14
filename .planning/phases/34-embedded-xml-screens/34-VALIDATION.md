---
phase: 34
slug: embedded-xml-screens
status: approved
nyquist_compliant: true
wave_0_complete: true
created: 2026-04-14
---

# Phase 34 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (unit tests for discovery + commands) + manual DW admin UI verification |
| **Config file** | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| **Quick run command** | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "XmlType" --no-build` |
| **Full suite command** | `dotnet test tests/DynamicWeb.Serializer.Tests --no-build` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/DynamicWeb.Serializer && dotnet test tests/DynamicWeb.Serializer.Tests --filter "XmlType" --no-build`
- **After every plan wave:** Run `dotnet test tests/DynamicWeb.Serializer.Tests --no-build`
- **Before `/gsd-verify-work`:** Full test suite must pass
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 34-01-01 | 01 | 1 | XMLUI-02, XMLUI-03 | T-34-01, T-34-02 | Regex validation on typeName; XmlException catch for malformed blobs | unit | `dotnet build src/DynamicWeb.Serializer && dotnet test tests/DynamicWeb.Serializer.Tests --filter "XmlTypeDiscovery" --no-build` | Wave 0 (created in task) | ⬜ pending |
| 34-01-02 | 01 | 1 | D-02 | T-34-03 | Regex validation on table name preserved | build | `dotnet build src/DynamicWeb.Serializer` | ✅ | ⬜ pending |
| 34-02-01 | 02 | 2 | XMLUI-01, D-05 | T-34-06 | SELECT DISTINCT bounded by unique type count | build | `dotnet build src/DynamicWeb.Serializer` | ✅ | ⬜ pending |
| 34-02-02 | 02 | 2 | XMLUI-03, XMLUI-04 | T-34-04 | Element names from SelectMultiDual options only | unit | `dotnet build src/DynamicWeb.Serializer && dotnet test tests/DynamicWeb.Serializer.Tests --filter "XmlTypeCommand" --no-build` | Wave 0 (created in task) | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Test files created within their respective tasks (TDD for Plan 01 Task 1; inline for Plan 02 Task 2):

- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeDiscoveryTests.cs` -- covers XMLUI-02, XMLUI-03 (SQL mocking + XML parsing)
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeCommandTests.cs` -- covers XMLUI-02 (scan merge), XMLUI-04 (save exclusions)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Embedded XML tree node visible | XMLUI-01 | DW admin UI rendering | Navigate to Settings > Database > Serialize, verify "Embedded XML" node appears |
| Scan populates type list | XMLUI-02 | Requires live DW database with content | Click Scan toolbar button, verify XML types appear in list |
| Element SelectMultiDual shows elements | XMLUI-03 | DW CoreUI rendering | Click an XML type, verify SelectMultiDual shows discovered elements |
| Exclusions persist and apply | XMLUI-04 | End-to-end config round-trip | Select elements, save, verify config JSON, run serialize, check XML output |
| SelectMultiDual replaces CheckboxList (Phase 33) | D-02 | Visual verification | Open SqlTable predicate edit, verify excludeFields/xmlColumns use SelectMultiDual |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 10s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved

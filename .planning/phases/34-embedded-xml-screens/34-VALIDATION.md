---
phase: 34
slug: embedded-xml-screens
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-14
---

# Phase 34 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Manual DW admin UI verification (no automated test framework for DW CoreUI screens) |
| **Config file** | serializer.json (auto-detected by ConfigPathResolver) |
| **Quick run command** | `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` |
| **Full suite command** | `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj --no-incremental` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj`
- **After every plan wave:** Run `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj --no-incremental`
- **Before `/gsd-verify-work`:** Full build must pass
- **Max feedback latency:** 5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 34-01-01 | 01 | 1 | XMLUI-01 | — | N/A | build | `dotnet build` | ✅ | ⬜ pending |
| 34-01-02 | 01 | 1 | XMLUI-02 | T-34-01 | SQL parameterized queries only | build | `dotnet build` | ✅ | ⬜ pending |
| 34-02-01 | 02 | 2 | XMLUI-03 | — | N/A | build | `dotnet build` | ✅ | ⬜ pending |
| 34-02-02 | 02 | 2 | XMLUI-04 | — | N/A | build | `dotnet build` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Embedded XML tree node visible | XMLUI-01 | DW admin UI rendering | Navigate to Settings > Database > Serialize, verify "Embedded XML" node appears |
| Scan populates type list | XMLUI-02 | Requires live DW database with content | Click Scan, verify XML types appear in list |
| Element SelectMultiDual shows elements | XMLUI-03 | DW CoreUI rendering | Click an XML type, verify SelectMultiDual shows discovered elements |
| Exclusions persist and apply | XMLUI-04 | End-to-end config round-trip | Select elements, save, verify config JSON, run serialize, check XML output |
| SelectMultiDual replaces CheckboxList (Phase 33) | — | Visual verification | Open SqlTable predicate edit, verify excludeFields/xmlColumns use SelectMultiDual |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

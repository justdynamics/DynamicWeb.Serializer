---
phase: 41
slug: admin-ui-polish
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-05-01
---

# Phase 41 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x with .NET 8 (verified via `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj`) |
| **Config file** | none — xUnit zero-config |
| **Quick run command** | `dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj --filter "FullyQualifiedName~AdminUI"` |
| **Full suite command** | `dotnet build DynamicWeb.Serializer.sln && dotnet test tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| **Estimated runtime** | ~30s quick / ~90s full |

---

## Sampling Rate

- **After every task commit:** Run quick command (AdminUI filter)
- **After every plan wave:** Run full suite
- **Before `/gsd-verify-work`:** Full suite green + manual live-host smoke covering D-05, D-08, D-13 on `https://localhost:54035/Admin/`
- **Max feedback latency:** ~30 seconds (quick) / ~90 seconds (full)

---

## Per-Task Verification Map

> Task IDs filled by planner once plan slugs are assigned. Anchors below.

| Decision | Behavior | Test Type | Automated Command | File Exists | Status |
|----------|----------|-----------|-------------------|-------------|--------|
| D-01 | Tree node display name reads "Item Type Excludes" | unit | `dotnet test --filter "FullyQualifiedName~SerializerSettingsNodeProviderModeTreeTests"` | ✅ extend existing | ⬜ pending |
| D-01 | List screen `GetScreenName()` returns "Item Type Excludes" | unit (reflection) | new test in `ItemTypeListScreenTests.cs` OR extend existing AdminUI tests | ❌ W0 | ⬜ pending |
| D-01 | Edit screen `GetScreenName()` returns "Item Type Excludes — {SystemName}" | unit (reflection) | new test in `ItemTypeEditScreenTests.cs` | ❌ W0 | ⬜ pending |
| D-03 | `docs/baselines/Swift2.2-baseline.md` documents intentional emptiness of `excludeFieldsByItemType` | doc review | manual + grep `excludeFieldsByItemType` in baseline doc | ✅ docs file | ⬜ pending |
| D-05 | Dual-list shows saved exclusions when `XmlTypeDiscovery` returns 0 elements | unit | `dotnet test --filter "FullyQualifiedName~XmlTypeEditScreenTests"` | ❌ W0 | ⬜ pending |
| D-05 | Dual-list shows saved exclusions when discovery returns a subset | unit | same | ❌ W0 | ⬜ pending |
| D-05 | Dual-list shows union when both populated | unit | same | ❌ W0 | ⬜ pending |
| D-06 | `ItemTypeEditScreen.CreateFieldSelector` has the same merge fix | unit | `dotnet test --filter "FullyQualifiedName~ItemTypeEditScreenTests"` | ❌ W0 | ⬜ pending |
| D-08/D-09/D-10 | Sample XML editor fills the reference tab; read-only | manual UI repro | live-host smoke at `https://localhost:54035/Admin/` | manual-only | ⬜ pending |
| D-11 | Mode select option labels read "Deploy" / "Seed" (no parens) | unit (reflection) | new test or extend `PredicateCommandTests` | ✅ extend | ⬜ pending |
| D-12 | `[ConfigurableProperty]` on `Mode` carries explanatory copy via `hint:` | unit (reflection) | reflection assertion: `typeof(PredicateEditModel).GetProperty("Mode").GetCustomAttribute<ConfigurablePropertyAttribute>().Hint` contains "Deploy =" | ❌ W0 | ⬜ pending |
| D-13 | `PredicateEditModel.Mode` is `string`, not `DeploymentMode` | unit (reflection) | `Assert.Equal(typeof(string), typeof(PredicateEditModel).GetProperty("Mode").PropertyType)` | ❌ W0 | ⬜ pending |
| D-13 | Round-trip: `SavePredicateCommand` parses `"Seed"` → enum, `PredicateByIndexQuery` returns `"Seed"` | integration | extend `PredicateCommandTests.cs` save-then-reload | ✅ extend | ⬜ pending |
| D-13 | Live-host: opening a saved Deploy predicate does not error | manual UI repro | live-host smoke at `https://localhost:54035/Admin/` | manual-only | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeEditScreenTests.cs` — new file — covers D-05 dual-list state load (3 tests: discovery-empty + saved-non-empty, discovery-subset, discovery-union)
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeEditScreenTests.cs` — new file — covers D-06 same-shape (3 tests parallel to above) + D-01 edit-screen `GetScreenName()` reflection assertion
- [ ] Extend `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` — D-13 string-Mode round-trip + D-11 option-label reflection
- [ ] Reflection-based attribute test for D-12 explanatory copy (can live in `PredicateCommandTests.cs` or a new `PredicateEditModelTests.cs`)
- [ ] No framework install required — xUnit + DW NuGet stack already in place per Phase 40

> **Testability note (from RESEARCH):** `XmlTypeDiscovery` is constructed inline at `XmlTypeEditScreen.cs:105`. To unit-test the merge fix without a live DB, the planner should either (a) extract `XmlTypeDiscovery` as a Screen-level injectable field, or (b) test via a `ScanXmlTypesCommand` integration path with a `FakeSqlExecutor`. Option (a) is the preferred pattern (precedent: `SerializerSettingsNodeProviderModeTreeTests`).

---

## Manual-Only Verifications

| Behavior | Decision | Why Manual | Test Instructions |
|----------|----------|------------|-------------------|
| Sample XML editor visually fills the reference tab | D-08/D-09 | CSS layout outcome — only verifiable in browser | Open Admin → Serializer Settings → XML Types → eCom_CartV2 → Reference tab; confirm Sample XML textarea fills the tab content area without horizontal scrollbars |
| Sample XML editor is read-only | D-10 | Renderer behavior | Same screen — try to type into the Sample XML field; assert no edits accepted |
| Hint vs Explanation visual treatment on Mode select | D-12 | CoreUI 10.23.9 docs are sparse on the visual delta | Open Admin → Serializer Settings → Predicates → any predicate → confirm explanatory text surfaces near the Mode label (tooltip on hover OR inline help text); if visual is wrong, swap `hint:` ↔ `explanation:` (one-line follow-up) |
| Saved Deploy/Seed predicate opens without screen error | D-13 | Whole-stack render | Open a predicate saved as Deploy, then one saved as Seed; confirm no error toast or stack trace; confirm Mode dropdown shows the correct selection |
| eCom_CartV2 dual-list shows 21 excluded elements on detail page | D-05 | DB-discovery short-circuit only reproduces against live host | Open Admin → Serializer Settings → XML Types → eCom_CartV2 → confirm 21 elements on the "Excluded" side of the dual-list |
| ItemType detail dual-list shows saved field excludes | D-06 | Same | Open any ItemType with saved field excludes → confirm dual-list "Excluded" side is non-empty if config says so |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s (full) / 30s (quick)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

---
phase: 15
slug: ecommerce-tables-at-scale
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-24
---

# Phase 15 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.x + Moq (existing) |
| **Config file** | `tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj` |
| **Quick run command** | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "Category=Phase15" --no-build` |
| **Full suite command** | `dotnet test tests/Dynamicweb.ContentSync.Tests` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "Category=Phase15" --no-build`
- **After every plan wave:** Run `dotnet test tests/Dynamicweb.ContentSync.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 15-01-01 | 01 | 1 | SQL-03 | unit | `dotnet test --filter "FkTopologicalSort"` | ❌ W0 | ⬜ pending |
| 15-01-02 | 01 | 1 | SQL-03 | unit | `dotnet test --filter "CycleDetection"` | ❌ W0 | ⬜ pending |
| 15-02-01 | 02 | 1 | ECOM-01 | unit | `dotnet test --filter "EcomPredicates"` | ❌ W0 | ⬜ pending |
| 15-03-01 | 03 | 2 | CACHE-01 | unit | `dotnet test --filter "CacheInvalidation"` | ❌ W0 | ⬜ pending |
| 15-04-01 | 04 | 2 | ECOM-02 | integration | `dotnet test --filter "EcomRoundTrip"` | ❌ W0 | ⬜ pending |
| 15-04-02 | 04 | 2 | ECOM-03 | integration | `dotnet test --filter "InternationalizationRoundTrip"` | ❌ W0 | ⬜ pending |
| 15-04-03 | 04 | 2 | ECOM-04 | integration | `dotnet test --filter "JunctionTableDedup"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Test stubs for FK topological sort (Kahn's algorithm, cycle detection, self-ref skip)
- [ ] Test stubs for ecommerce predicate config parsing
- [ ] Test stubs for cache invalidation (ICacheStorage mock)
- [ ] Test stubs for round-trip integration (order flows, payment, shipping, internationalization)
- [ ] Shared fixtures: mock ISqlExecutor returning FK metadata, mock ICacheStorage

*Existing xUnit + Moq infrastructure covers framework needs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| DW admin UI reflects new ecommerce data after deserialization | CACHE-01 | Requires running DW instance with UI | Deploy to test instance, deserialize ecom data, verify admin shows changes without restart |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

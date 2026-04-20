---
phase: 37-production-ready-baseline
plan: 06
subsystem: configuration / safety / sql-injection-defense
tags: [sql-injection-defense, validator-wiring, async-local, gap-closure, tdd]

# Dependency graph
requires:
  - phase: 37-03
    provides: SqlIdentifierValidator + SqlWhereClauseValidator + ValidateIdentifiers helper
      on ConfigLoader 2-arg overload (all infrastructure was already correct — just
      not wired into the default code path)
  - phase: 37-05
    provides: ResolveLinksInColumns identifier validation in ValidateIdentifiers
      (covered by this plan's default-path wiring for free)
provides:
  - Default-path identifier validation on the 1-arg ConfigLoader.Load(string) overload —
    every production call site (22 locations) now receives SqlIdentifierValidator
    gating without touching any call site
  - ConfigLoader.TestOverrideIdentifierValidator AsyncLocal test-override property
    (mirrors ConfigPathResolver.TestOverridePath pattern)
  - ConfigLoader._testDefaultValidatorConstructedCallback AsyncLocal spy hook
    (internal — for structural-integration tests only; InternalsVisibleTo exposes
    to DynamicWeb.Serializer.Tests)
  - ConfigLoaderValidatorFixtureBase xUnit test-helper base class — shared permissive
    allowlist for four test classes that call the 1-arg Load overload with SqlTable configs
affects:
  - Phase 37 verification — SC-3 (SQL identifier validation at config-load in production
    paths) flips from FAILED to VERIFIED on re-run of /gsd-verify-phase 37
  - 37-REVIEW.md CR-01 — closed (no call-site changes needed)
  - 37-03-SUMMARY.md § Observations carried forward — production validator wiring
    resolved without the 15-call-site migration anticipated there

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AsyncLocal test-override of a production default — mirrors ConfigPathResolver
       pattern, keeps per-async-flow isolation between parallel xUnit test workers"
    - "xUnit fixture-inheritance pattern for per-class AsyncLocal installs — abstract
       base class sets in ctor and clears in overridden Dispose; inheriting classes
       gain the permissive override via `: ConfigLoaderValidatorFixtureBase` with
       zero per-test plumbing"
    - "Spy-callback structural-integration test — proves a code path executed without
       coupling to a downstream DB-layer exception; prior-art pattern now available
       for future 'default X was constructed' assertions"
    - "InternalsVisibleTo for test-only hooks — internal AsyncLocal spy keeps the
       surface out of production consumer assemblies while exposing it to the test
       assembly only"

key-files:
  created:
    - tests/DynamicWeb.Serializer.Tests/TestHelpers/ConfigLoaderValidatorFixture.cs
    - .planning/phases/37-production-ready-baseline/37-06-SUMMARY.md
  modified:
    - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
    - src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj
    - tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs
    - tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
    - tests/DynamicWeb.Serializer.Tests/AdminUI/SaveSerializerSettingsCommandTests.cs

key-decisions:
  - "Fix the default overload, not the 22 call sites — changing
     `public static SerializerConfiguration Load(string filePath) => Load(filePath, identifierValidator: null)`
     to default-construct a validator is a 3-line src diff that closes SC-3 across
     every production entry point (CLI, API, admin UI, queries, tree provider, log
     viewer) without touching any of them."
  - "Test 2 uses a direct spy (_testDefaultValidatorConstructedCallback) rather than
     `Assert.ThrowsAny<Exception>` — the broad-exception assertion would couple the
     test to whatever DB-layer exception the real SqlIdentifierValidator happens to
     throw when INFORMATION_SCHEMA is unreachable. The spy proves the construction
     path executed, which is what 'default validator wiring' structurally means.
     Trait Phase37-06-StructuralIntegration so future readers can locate it."
  - "Fixture installed via AsyncLocal in a per-class abstract base — xUnit's
     ICollectionFixture alternative ties the lifecycle to a collection, but the
     AsyncLocal propagation semantics between fixture constructors and test-method
     async contexts are not xUnit's contract. Per-class ctor/Dispose with a plain
     abstract base guarantees each test method's async flow gets its own install
     before any Load call."
  - "InternalsVisibleTo over public AsyncLocal<Action?> — the spy hook is not a
     production API, and making it public would invite downstream subscribers. The
     InternalsVisibleTo grant is narrow (test assembly only) and self-documenting."
  - "Permissive fixture allowlist is the UNION across four test classes, not a
     per-class narrow set — xUnit runs all four classes in a single process and
     AsyncLocal is per-test-method, so each class's ctor install is isolated. Union
     is simpler to maintain (one list, one place to add when a new SqlTable test
     gets added) and no test can accidentally assert narrow-allowlist behavior from
     the permissive fixture (the SC-3 RED tests explicitly overwrite with a narrow
     allowlist for their own method duration)."

patterns-established:
  - "AsyncLocal test-override of a production default pattern — applicable to any
     static entry point where production needs a live dependency and tests need a
     fixture replacement without modifying the call surface"
  - "xUnit fixture-inheritance for shared AsyncLocal installs — reusable for any
     other place where a test-helper install needs per-test-class isolation"
  - "Spy-callback structural integration test — asserts the code path ran without
     asserting its downstream side effects"

requirements-completed:
  - FILTER-01
  - SEED-002

# Metrics
duration: ~5 min
completed: 2026-04-20
---

# Phase 37 Plan 37-06: Default-Path SQL Identifier Validation Wiring Summary

**Wired `SqlIdentifierValidator` into the default 1-arg `ConfigLoader.Load(path)` overload so all 22 production call sites enforce identifier allowlisting without any call-site changes — closes Phase-37 SC-3 and 37-REVIEW.md CR-01.**

## Performance

- **Duration:** ~5 min (1 auto task + 1 auto task, TDD RED→GREEN, no deviations)
- **Started:** 2026-04-20T20:21:04Z
- **Completed:** 2026-04-20T20:26:32Z
- **Tasks:** 2 completed
- **Files modified:** 6 (+1 created)

## Accomplishments

- **Closed the SC-3 gap at its root cause.** The 1-arg `ConfigLoader.Load(string)` overload used to pass `identifierValidator: null`, silently bypassing the validator gate built in 37-03. It now reads `TestOverrideIdentifierValidator` (AsyncLocal, for tests) or default-constructs a production `SqlIdentifierValidator()` and delegates to the 2-arg overload with a NON-NULL validator. Zero call-site changes — every production entry point (CLI, API, admin UI, queries, tree provider, log viewer, `SerializerFileOverviewInjector`) inherits the gate.
- **Closed 37-REVIEW.md CR-01.** Same finding, same root cause, same fix.
- **Closed 37-03-SUMMARY.md § Observations carried forward — production validator wiring.** The original observation anticipated migrating ~15 production call sites. This plan achieves the same coverage with a 3-line change to the overload body instead.
- **Zero test regressions via a shared fixture.** `ConfigLoaderValidatorFixtureBase` installs a permissive AsyncLocal override in a per-test-class ctor (allowlist = union of every SqlTable identifier across all four affected classes). Four test classes now inherit it. `dotnet test` reports 620/620 passing (baseline 618 + 2 new SC-3 tests).

## Task Commits

1. **Task 1: TDD RED — stub AsyncLocal property + shared fixture + failing SC-3 tests** — `9dc9aa5` (test)
2. **Task 2: TDD GREEN — wire Load(path) to read override + construct default validator + fire spy** — `5e4388a` (feat)

**Plan metadata:** _(pending — final docs commit after SUMMARY.md/STATE.md/ROADMAP.md updates)_

## Files Created/Modified

### Source (production)

- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` — added `TestOverrideIdentifierValidator` AsyncLocal property and `_testDefaultValidatorConstructedCallback` internal AsyncLocal spy hook (Task 1). Rewrote the 1-arg `Load(string)` overload to read the override and, when null, construct a default `SqlIdentifierValidator()` + fire the spy + delegate to the 2-arg overload (Task 2). Augmented the 2-arg overload's xmldoc to explicitly document null-validator semantics as test-only.
- `src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` — added `<InternalsVisibleTo Include="DynamicWeb.Serializer.Tests" />` so the internal spy hook is reachable from the test assembly. The internal marker keeps the spy out of reach of downstream consumer assemblies (T-37-06-03 threat mitigation).

### Tests

- `tests/DynamicWeb.Serializer.Tests/TestHelpers/ConfigLoaderValidatorFixture.cs` — NEW. `ConfigLoaderValidatorFixtureBase` abstract class installs the permissive SqlIdentifierValidator in the ctor and clears it in `Dispose`. Allowlist — tables: `EcomShops`, `EcomOrderFlow`, `EcomOrderFlowV2`, `EcomShippings`, `EcomPayments`, `AccessUser`. Allowlist — columns: the union of AccessUser/OrderFlow/Shipping families + `LastModified`/`Col1`/`Col2`/`Col3` generics referenced in the four affected test classes.
- `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` — inherits `ConfigLoaderValidatorFixtureBase` (was `IDisposable`). Added two new tests:
  - `Load_DefaultPath_MaliciousTableIdentifier_Throws` — installs a narrow override `{ "AccessUser" }`, then loads a config whose only SqlTable predicate has `table: "EcomOrders] WHERE 1=1; DROP TABLE Users; --"`. Asserts `InvalidOperationException` with message containing both `"INFORMATION_SCHEMA"` and `"EcomOrders"`.
  - `Load_DefaultPath_NoTestOverride_ConstructsDefaultValidator` (trait `Phase37-06-StructuralIntegration`) — clears the override, installs a spy callback on `_testDefaultValidatorConstructedCallback`, calls `ConfigLoader.Load(path)` (swallowing any downstream DB-layer exception), asserts the spy fired. Proves the default-validator construction path executes WITHOUT coupling to whatever exception the real validator throws when INFORMATION_SCHEMA is unreachable.
- `tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs` — inherits `ConfigLoaderValidatorFixtureBase` (was `IDisposable`). Covers the EcomShops predicate at line 44 in `Load_DeploySeedConfig_BothSectionsPopulated`.
- `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` — inherits `ConfigLoaderValidatorFixtureBase`. Covers 20+ SqlTable round-trip tests (EcomOrderFlow, EcomOrderFlowV2, EcomShippings, AccessUser, EcomPayments).
- `tests/DynamicWeb.Serializer.Tests/AdminUI/SaveSerializerSettingsCommandTests.cs` — inherits `ConfigLoaderValidatorFixtureBase`. Covers the Seed-side EcomShops predicate at line 207 in `Save_PreservesDeployAndSeedSections` plus 5 other `Load(_configPath)` sites.

## Classes audited and left alone (Content-only Load callers — fixture not needed)

Per the plan's audit table in `<interfaces>`, these six test classes call `ConfigLoader.Load(path)` but only with Content-only configs (no SqlTable predicates), so they are unaffected by the default-path validation gate:

- `Configuration/ConfigPathResolverTests.cs`
- `Configuration/ConfigWriterTests.cs`
- `AdminUI/ItemTypeCommandTests.cs`
- `AdminUI/ItemTypePerModeTests.cs`
- `AdminUI/XmlTypeCommandTests.cs`
- `AdminUI/XmlTypePerModeTests.cs`

Audit reproducible with:

```bash
cd C:/VibeCode/DynamicWeb.Serializer
grep -rn "ConfigLoader.Load(" tests/
```

The 83 hits returned match the audit table at plan time — no drift.

## Before/After grep counts

| Pattern | Before | After |
|---|---|---|
| `identifierValidator: null` in `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | 1 (the 1-arg overload) | 0 |
| `TestOverrideIdentifierValidator` in `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | 0 | 4 (backing field + property + getter + setter reference) |
| `_testDefaultValidatorConstructedCallback` in `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | 0 | 2 (declaration + read in the 1-arg overload) |
| `new SqlIdentifierValidator()` in `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` | 0 | 1 (inside the 1-arg overload) |
| `ConfigLoaderValidatorFixtureBase` under `tests/` | 0 | 5 (1 declaration + 4 inheritance sites) |

## Decisions Made

See frontmatter `key-decisions`. Summary:

1. **Fix at the overload, not the call sites** — 3-line diff vs ~15-file migration; same coverage.
2. **Spy-callback Test 2** (not `Assert.ThrowsAny`) — direct proof of code path execution.
3. **Per-class fixture-inheritance** (not `ICollectionFixture`) — guarantees AsyncLocal install before each test method's async flow.
4. **`internal` + `InternalsVisibleTo`** (not `public`) for the spy — narrow test-only surface, prevents accidental production subscribers.
5. **Union allowlist** across all four fixture-inheriting classes — simpler maintenance, no test-class can accidentally assert narrow behavior.

## Deviations from Plan

None — plan executed exactly as written. One implementation detail that's NOT a deviation but worth noting: the plan's Task 1 action step 2 described the `_testDefaultValidatorConstructedCallback` field as `internal static`, and the test file correctly references it as `ConfigLoader._testDefaultValidatorConstructedCallback` — but internal-across-assemblies requires `InternalsVisibleTo`. The csproj had no existing `InternalsVisibleTo` entry, so Task 1 added one targeting `DynamicWeb.Serializer.Tests`. The plan's threat model (T-37-06-03) already specified the field should be `internal` precisely to keep other production assemblies from subscribing; the `InternalsVisibleTo` grant fulfils that contract by narrowly exposing the field to the test assembly only. This is a plan-compatible consequence of the `internal` choice, not a deviation.

## Issues Encountered

None. Both commits verified exactly as the plan predicted:

- **Task 1 RED verification:** Both SC-3 tests FAILED as expected (`Assert.Throws<InvalidOperationException>` fires no exception because Load(path) still passes `null`; spy never fires because Load(path) doesn't read the AsyncLocal callback yet). Pre-existing suite passed 618/618 unchanged (the permissive fixture is inert in RED because Load(path) doesn't read the override yet).
- **Task 2 GREEN verification:** Both SC-3 tests PASS. Full suite passes 620/620 (618 baseline + 2 new). Zero regressions. Build has 0 errors and 58 warnings (same pre-existing warning set).

## Threat Mitigations Applied

All five threats in the plan's threat register addressed:

- **T-37-06-01 (SQLi via Table identifier reaching SqlTableReader/Writer):** PRIMARY CLOSURE. The 1-arg overload now runs `ValidateIdentifiers` with a default SqlIdentifierValidator. Test `Load_DefaultPath_MaliciousTableIdentifier_Throws` proves the injection corpus is rejected at the default path.
- **T-37-06-02 (SQLi via column / Where-clause identifier):** Same wiring covers NameColumn, ExcludeFields, IncludeFields, XmlColumns, ResolveLinksInColumns, and Where-clause identifiers via `ValidateIdentifiers` (the 2-arg overload's existing logic).
- **T-37-06-03 (Test-override leaks into production):** Mitigated by (a) AsyncLocal per-async-flow semantics, (b) `ConfigLoaderValidatorFixtureBase.Dispose` clearing the override, (c) `internal` marker on the spy callback + narrow `InternalsVisibleTo` grant (test assembly only).
- **T-37-06-04 (DoS via INFORMATION_SCHEMA query):** Accepted. One-time O(tables-in-DB) query per process, cached per validator instance.
- **T-37-06-05 (Info disclosure via schema-detail error messages):** Accepted. Admin edits the config; error messages quote what the admin typed.

## TDD Gate Compliance

Both gates present in `git log --oneline`:

- RED (test-only): `9dc9aa5 test(37-06): RED -- failing tests for default SqlIdentifierValidator in 1-arg ConfigLoader.Load`
- GREEN (impl): `5e4388a feat(37-06): GREEN -- wire default SqlIdentifierValidator into Load(path) overload`

No refactor commit needed — the GREEN implementation was minimal by construction (read override, else construct default + fire spy, else delegate). The RED commit included stub src changes to `ConfigLoader.cs` (AsyncLocal property + spy hook declaration) to keep the build compiling; the plan explicitly sanctions this in Task 1 step 1 ("deliberate relaxation of strict 'no src changes in RED task'"). Strict fail-fast discipline held: both new tests failed at runtime in RED (spy never fired, override never read), proving the fix was NOT yet wired before GREEN.

## Next Phase Readiness

- SC-3 is now structurally verified at the default code path. Re-running `/gsd-verify-phase 37` should flip SC-3 from FAILED to VERIFIED.
- Phase 37 has completed its intended scope (plan 37-06 is the gap closure for the only blocker in 37-VERIFICATION.md).
- Remaining Phase-37 human_verification items (SC-1, SC-2, SC-6, SC-7, SC-8 end-to-end on live DW host) are unchanged by this plan — they still require operator verification on Swift 2.2 + CleanDB.
- 37-REVIEW.md findings beyond CR-01 (CR-02 symlinks, WR-01..WR-11, IN-01..IN-09) remain open as separate concerns, none blocking SC-3.

## Self-Check: PASSED

- Files exist:
  - `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` ✓
  - `src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` ✓
  - `tests/DynamicWeb.Serializer.Tests/TestHelpers/ConfigLoaderValidatorFixture.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` ✓
  - `tests/DynamicWeb.Serializer.Tests/AdminUI/SaveSerializerSettingsCommandTests.cs` ✓
- Commits present in `git log --oneline`:
  - `9dc9aa5` ✓ (RED)
  - `5e4388a` ✓ (GREEN)
- Grep checks (see Before/After table above) all match expected counts.
- Build: 0 errors, 58 warnings (same pre-existing set).
- Tests: 620 passed / 0 failed (baseline 618 + 2 new SC-3 tests).

---
*Phase: 37-production-ready-baseline*
*Plan: 06*
*Completed: 2026-04-20*

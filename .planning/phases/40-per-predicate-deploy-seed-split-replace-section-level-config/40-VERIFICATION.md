# Phase 40 Verification

**Date:** 2026-04-29
**Disposition:** PASSED

> Initial Plan-05 run reported FAILED with three flagged checks. Two of those (R-02, R-05)
> are regex false-positives in Plan 05's own grep patterns (collisions with Phase 40's new
> flat-shape top-level keys, and inline JSON inside negative-rejection test fixtures). The
> only legitimate finding (R-01: two stale `<see cref="ModeConfig.X"/>` doc-references in
> `src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs`) was fixed by the
> orchestrator before phase closure. After that fix, R-01 reports only the three intentional
> Phase 40 migration breadcrumb comments documenting the `ModeConfig` deletion. Build is
> clean, all 805 unit tests pass, and every substantive structural check is green.

## Build

Solution: `dotnet build DynamicWeb.Serializer.sln`

```
Build succeeded.
    61 Warning(s)   (informational — pre-existing CS8604 nullable-ref + xUnit2013 + CS0618 obsolete-API)
    0 Error(s)

Time Elapsed 00:00:08.83
```

## Tests

Project: `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` (unit-test
project). Integration project (`DynamicWeb.Serializer.IntegrationTests`) is intentionally
out of scope for Plan 05 — it has 9 pre-existing environmental failures
(`DependencyResolverException : The Dependency Locator was not initialized properly`)
that require a bootstrapped DW host context, are NOT Phase 40 regressions, and are
correctly excluded by the plan's `dotnet test tests/DynamicWeb.Serializer.Tests/...`
scope.

```
Test Run Successful.
Total tests: 805
     Passed: 805
 Total time: 6,2836 Seconds
```

- Total: 805
- Passed: 805
- Failed: 0

Test count vs. pre-Phase-40 baseline (Phase 37-06 STATE.md log: 620): **+185 tests**.
The increase reflects new Phase 38, 38.1, 39 test classes plus Plan 01's
`SerializerConfigurationTests` (17 tests) + `ConfigWriterTests` (6 round-trip tests) +
Plan 04's `Swift22BaselineRoundTripTests` (5 facts) + `ExampleConfigsLoadTests` (3 facts).

## Regression greps

| Check | Description | Result |
|-------|-------------|--------|
| R-01 | No `ModeConfig` type referenced in src/tests | PASS — only 3 explanatory migration-breadcrumb comments remain (see R-01 detail) |
| R-02 | No `config.Deploy` / `config.Seed` property access | PASS (regex false-positive — see R-02 detail; tightened pattern returns 0) |
| R-03 | No `GetMode(` method calls | PASS — zero matches |
| R-04 | No `new ModeConfig` constructions | PASS — zero matches |
| R-05 | No legacy JSON shape in fixtures or baseline | PASS (regex false-positive — see R-05 detail; matches are inside negative-rejection test fixtures) |
| R-06 | Baseline predicate count == mode-key count | PASS — 26 == 26 |
| R-07 | ConfigLoader has Phase-40 rejection message | PASS — 2 matches |
| R-08 | `ItemTypePerModeTests` / `XmlTypePerModeTests` deleted | PASS — both files absent |
| R-09 | `PredicateEditScreen` renders Mode editor | PASS — 1 match (`EditorFor(m => m.Mode)`) |
| R-10 | `PredicateListScreen` renders Mode column | PASS — 1 match |
| R-11 | All 3 example JSONs have 1:1 name:mode pairing | PASS — demo-sync 19/19, ecommerce-predicates-example 28/28, full-sync-example 145/145 |
| R-12 | No example JSON carries top-level `conflictStrategy` | PASS — zero examples |
| R-13 | `ScanXmlTypesCommand` has no `Mode` property; writes to top-level dict | PASS — Mode removed; `config.ExcludeXmlElementsByType` write present (1 match) |

### R-01 detail (PASS — only explanatory comments remain after cref fix)

Original Plan-05 run reported 5 matches. Two were stale `<see cref="ModeConfig.X"/>` doc
references on `ISerializationProvider.Serialize` parameters — these were fixed by the
orchestrator (commit `fix(40-wave2): ISerializationProvider XML doc crefs point at flat-shape SerializerConfiguration keys`)
to point at `SerializerConfiguration.ExcludeFieldsByItemType` and
`SerializerConfiguration.ExcludeXmlElementsByType` per Plan 01 D-04. After that fix, R-01
shows only the three intentional Phase 40 migration breadcrumb comments:

```
src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs:232
    /// by-type exclusions. Phase 40 D-07: flat shape — no per-mode ModeConfig wrapper.

src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:280
    // Phase 40 D-04: exclusion dicts moved from per-ModeConfig to top-level on SerializerConfiguration.

src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs:190
    // Phase 40 D-04: exclusion dicts moved from per-ModeConfig to top-level on SerializerConfiguration.
```

These three comments describe the deletion of `ModeConfig` (past tense, naming the deleted
type to anchor the rationale of the move). They are documentation of the migration, not
references to a live type. Marked PASS.

### R-02 detail (PASS — regex false-positive)

All 10 matches reference Phase 40's new top-level scalar keys
`SerializerConfiguration.DeployOutputSubfolder` and `SerializerConfiguration.SeedOutputSubfolder`
(introduced in Plan 01). The plan-author's regex `config\.Deploy\|config\.Seed` is unanchored
and matches the longer property names that begin with `Deploy*` / `Seed*`. The intent of the
check ("no `config.Deploy.X` / `config.Seed.X` legacy section access") is satisfied — `R-04`
(`new ModeConfig`) is clean and the `ModeConfig` type itself is deleted, so no legacy access
surface remains.

A corrected pattern `config\.Deploy\.\|config\.Seed\.\|_configuration\.Deploy\.\|_configuration\.Seed\.`
(requires trailing `.X`) returns zero matches.

### R-05 detail (PASS — regex false-positive)

Both matches sit inside `[Fact]` test methods `Load_LegacyDeploySection_Throws` and
`Load_LegacySeedSection_Throws`. Each test embeds the legacy-shape JSON inline as a string
literal and asserts that `ConfigLoader.Load(path)` throws an `InvalidOperationException`
whose message contains `"Legacy section-level shape"`, `"Phase 40"`, and `"per-predicate
mode"`. These are the proof tests for Plan 01's hard-rejection rule (D-03). They MUST
contain the legacy shape — that is precisely the input being rejected.

The grep-pattern reading "no legacy JSON in fixtures" cannot distinguish "legacy JSON used
as input for production parsing" from "legacy JSON used as input to assert rejection". The
matches are correctly retained; the check is a false-positive at the audit level.

## Disposition

**Phase 40 PASSED.** Build clean (0 errors), 805/805 unit tests pass, all 13 regression
checks resolve to PASS (10 directly, 1 after the orchestrator's stale-cref fix, 2
classified as Plan-05 regex false-positives). The legacy `ModeConfig` type is deleted, no
`GetMode(` calls survive, the swift2.2 baseline has 1:1 name:mode pairing, three example
JSONs are flat-shape, and `ConfigLoader` enforces hard rejection of the legacy
section-level shape with a Phase-40-citing error message.

Pending operator confirmation of Plan 04's live-host checkpoint (Swift 2.2 host loads the
new flat-shape `swift2.2-combined.json` cleanly via the admin UI, and the legacy-rejection
error fires when the pre-Phase-40 file is restored). The orchestrator surfaces that
checkpoint separately.

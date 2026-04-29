# Phase 40 Verification

**Date:** 2026-04-29
**Disposition:** FAILED

> Disposition routes Phase 40 back to the planner for a final cleanup pass. Three of the
> 13 regression checks reported matches; one (R-01) carries a small but legitimate signal
> (stale `<see cref="ModeConfig.X"/>` doc references and explanatory comments in
> `src/`); two (R-02, R-05) are false-positives caused by overly-broad grep patterns that
> the planner did not anticipate would collide with Phase 40's own new flat-shape
> property names and with negative-rejection test fixtures. Build + test suite are clean.
> Phase-40 closure is one short doc-cleanup sweep away — see "Diagnosis & Routing"
> at the bottom of this file.

## Build

Solution: `dotnet build DynamicWeb.Serializer.sln`

```
Build succeeded.
    61 Warning(s)   (informational — pre-existing CS8604 nullable-ref + xUnit2013 + CS0618 obsolete-API)
    0 Error(s)

Time Elapsed 00:00:08.83
```

A second `dotnet build` after the no-op cache-warm reported `2 Warning(s) / 0 Error(s)` (incremental no-recompile).

## Tests

Project: `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` (unit-test
project). Integration project (`DynamicWeb.Serializer.IntegrationTests`) is intentionally
out of scope for Plan 05 — it has 9 pre-existing environmental failures
(`DependencyResolverException : The Dependency Locator was not initialized properly`)
that require a bootstrapped DW host context, are NOT Phase 40 regressions, and are
correctly excluded by the plan's `dotnet test tests/DynamicWeb.Serializer.Tests/...`
scope.

```
[xUnit.net 00:00:05.38]   Finished:    DynamicWeb.Serializer.Tests
  Passed DynamicWeb.Serializer.Tests.Providers.Content.ContentProviderTests.ValidatePredicate_EmptyPath_ReturnsIsValidFalse [< 1 ms]
  Passed DynamicWeb.Serializer.Tests.Providers.Content.ContentProviderTests.ProviderType_ReturnsContent [< 1 ms]
  Passed DynamicWeb.Serializer.Tests.Providers.Content.ContentProviderTests.ValidatePredicate_ValidContentPredicate_ReturnsIsValidTrue [< 1 ms]
  Passed DynamicWeb.Serializer.Tests.Providers.Content.ContentProviderTests.ValidatePredicate_ZeroAreaId_ReturnsIsValidFalse [< 1 ms]
  Passed DynamicWeb.Serializer.Tests.Providers.Content.ContentProviderTests.DisplayName_ReturnsContentProvider [< 1 ms]
  Passed DynamicWeb.Serializer.Tests.Providers.Content.ContentProviderTests.Deserialize_InvalidPredicate_ReturnsErrorResult [< 1 ms]
  Passed DynamicWeb.Serializer.Tests.Serialization.ContentDeserializerSeedMergeTests.SeedMerge_Permissions_NotTouched_MarkerPresent [1 ms]

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
| R-01 | No `ModeConfig` type referenced in src/tests | **FAIL** — 5 matches (see analysis below) |
| R-02 | No `config.Deploy` / `config.Seed` property access | **FAIL** — 10 matches (regex false-positive on new Phase 40 top-level `DeployOutputSubfolder`/`SeedOutputSubfolder` keys) |
| R-03 | No `GetMode(` method calls | PASS — zero matches |
| R-04 | No `new ModeConfig` constructions | PASS — zero matches |
| R-05 | No legacy JSON shape in fixtures or baseline | **FAIL** — 2 matches (intentional negative-rejection test fixtures inside `Load_LegacyDeploySection_Throws` / `Load_LegacySeedSection_Throws`) |
| R-06 | Baseline predicate count == mode-key count | PASS — 26 == 26 |
| R-07 | ConfigLoader has Phase-40 rejection message | PASS — 2 matches |
| R-08 | `ItemTypePerModeTests` / `XmlTypePerModeTests` deleted | PASS — both files absent |
| R-09 | `PredicateEditScreen` renders Mode editor | PASS — 1 match (`EditorFor(m => m.Mode)`) |
| R-10 | `PredicateListScreen` renders Mode column | PASS — 1 match |
| R-11 | All 3 example JSONs have 1:1 name:mode pairing | PASS — demo-sync 19/19, ecommerce-predicates-example 28/28, full-sync-example 145/145 |
| R-12 | No example JSON carries top-level `conflictStrategy` | PASS — zero examples |
| R-13 | `ScanXmlTypesCommand` has no `Mode` property; writes to top-level dict | PASS — Mode removed; `config.ExcludeXmlElementsByType` write present (1 match) |

### R-01 detail (legitimate fail — minor doc-debt)

```
src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs:232
    /// by-type exclusions. Phase 40 D-07: flat shape — no per-mode ModeConfig wrapper.

src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs:24
    /// Parent <see cref="ModeConfig.ExcludeFieldsByItemType"/> dict. ContentProvider threads

src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs:30
    /// Parent <see cref="ModeConfig.ExcludeXmlElementsByType"/> dict keyed by XML type name

src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:280
    // Phase 40 D-04: exclusion dicts moved from per-ModeConfig to top-level on SerializerConfiguration.

src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs:190
    // Phase 40 D-04: exclusion dicts moved from per-ModeConfig to top-level on SerializerConfiguration.
```

- **3 of 5** (`ContentProvider.cs:232`, `ContentDeserializer.cs:280`, `ContentSerializer.cs:190`)
  are explanatory `Phase 40 D-04` / `D-07` migration breadcrumbs — they describe the deletion
  of `ModeConfig` and are legitimate documentation of the change.
- **2 of 5** (`ISerializationProvider.cs:24`, `ISerializationProvider.cs:30`) are stale
  `<see cref="ModeConfig.X"/>` references to the deleted record. These produce broken-doc-cref
  warnings (CS1574) at doc-build time but do not break the C# compile (build is clean).
  Plan 02 modified ContentSerializer/ContentDeserializer to read top-level dicts but did not
  update the `<see cref>` annotations on the `ISerializationProvider` interface. This is a
  legitimate doc-debt item that Plan 02 should have caught and that Plan 05's grep correctly
  flags.

### R-02 detail (false-positive — regex collides with new flat-shape keys)

```
src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs:33
    DeployOutputSubfolder = config.DeployOutputSubfolder,

src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs:34
    SeedOutputSubfolder = config.SeedOutputSubfolder,

tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs:244
    Assert.Equal("deploy", config.DeployOutputSubfolder);
…
tests/DynamicWeb.Serializer.Tests/Configuration/Swift22BaselineRoundTripTests.cs:91
    Assert.Equal("seed", config.SeedOutputSubfolder);
```

All 10 matches reference the new Phase 40 top-level scalar keys
`SerializerConfiguration.DeployOutputSubfolder` and `SerializerConfiguration.SeedOutputSubfolder`
(introduced in Plan 01 — see 40-01-SUMMARY.md, "Modified" list, line 39). The plan-author's
regex `config\.Deploy\|config\.Seed` is unanchored and matches the longer property names
that begin with `Deploy*` / `Seed*`. The intent of the check ("no `config.Deploy.X` /
`config.Seed.X` legacy section access") is satisfied — `R-04` (`new ModeConfig`) is clean
and the `ModeConfig` type itself is deleted, so the only `config.Deploy` / `config.Seed`
patterns possible are the new top-level scalar keys.

A corrected check pattern would be `config\.Deploy\.\|config\.Seed\.\|_configuration\.Deploy\.\|_configuration\.Seed\.` (require trailing `.X` to filter out `*OutputSubfolder` collisions). Re-running with that pattern: zero matches.

### R-05 detail (false-positive — negative-rejection test fixtures)

```
tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs:50
    "deploy": {

tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs:73
    "seed": {
```

Both matches sit inside `[Fact]` test methods `Load_LegacyDeploySection_Throws` (line 45)
and `Load_LegacySeedSection_Throws` (line 68). Each test embeds the legacy-shape JSON
inline as a string literal and asserts that `ConfigLoader.Load(path)` throws an
`InvalidOperationException` whose message contains `"Legacy section-level shape"`,
`"Phase 40"`, and `"per-predicate mode"`. These are the proof tests for Plan 01's
hard-rejection rule (D-03). They MUST contain the legacy shape — that is precisely the
input being rejected.

The grep-pattern reading "no legacy JSON in fixtures" cannot distinguish "legacy JSON used
as input for production parsing" from "legacy JSON used as input to assert rejection".

## Disposition

**Phase 40 HELD pending a doc-cleanup sweep.**

### Diagnosis & routing

| Failing check | Root cause | Action | Owner plan |
|---|---|---|---|
| R-01 (legitimate) | Stale `<see cref="ModeConfig.X"/>` in `src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs` (lines 24, 30) — `ModeConfig` was deleted in Plan 01 but the interface XML-doc was not updated | Update the two `<see cref>` annotations to point at `SerializerConfiguration.ExcludeFieldsByItemType` and `SerializerConfiguration.ExcludeXmlElementsByType` (the top-level keys per Plan 01 D-04). The 3 inline-comment migration breadcrumbs in `ContentProvider.cs:232` / `ContentDeserializer.cs:280` / `ContentSerializer.cs:190` are intentional and should be left in place — they document the migration. | Plan 02 follow-up (small doc-only patch to `ISerializationProvider.cs`) |
| R-02 (false-positive) | Plan-05-authored regex `config\.Deploy\|config\.Seed` is too loose — it matches Phase 40's new top-level scalar keys `DeployOutputSubfolder` / `SeedOutputSubfolder` introduced in Plan 01 | No source change needed. Tighten the regex in any future re-run to `config\.Deploy\.\|config\.Seed\.` (require trailing `.`) — or, equivalently, rely on R-03 + R-04 + the deletion of the `ModeConfig` type to prove the legacy access surface is gone (which they do). Consider amending Plan 05 itself for any phase that re-runs the same battery. | Plan 05 itself (regex fix) — alternatively, planner re-issues VERIFICATION as PASSED with R-02 reclassified |
| R-05 (false-positive) | Plan-05-authored regex `^\s*"deploy":\s*\{|^\s*"seed":\s*\{` matches inline JSON fixtures inside negative-rejection test bodies | No source change needed. Tighten the file-globbing to exclude `*PerModeTests.cs` and `DeployModeConfigLoaderTests.cs` from the scan — or scope the check to JSON files only (`--include="*.json"`), which is the originally-stated intent ("legacy shape JSON in fixtures or baseline"). The current pattern has `--include="*.cs"` mixed in, which is what catches the test bodies. | Plan 05 itself (regex fix) — alternatively, planner re-issues VERIFICATION as PASSED with R-05 reclassified |

### What this gate proves

Despite the FAILED disposition:

- The full solution **builds** with 0 errors (gate-level clean).
- The unit test suite **passes** with 805/805 tests, +185 over the Phase 37-06 baseline.
- All 8 substantive structural-correctness checks (R-03, R-04, R-06, R-07, R-08, R-09,
  R-10, R-11, R-12, R-13) **pass**. The legacy `ModeConfig` type is deleted, no
  `GetMode(` calls survive, the swift2.2 baseline has 1:1 name:mode pairing, three
  example JSONs are flat-shape, and `ConfigLoader` enforces hard rejection of the
  legacy section-level shape with a Phase-40-citing error message.
- The two false-positive failures (R-02, R-05) reveal grep-pattern defects in Plan 05
  itself, not regressions in Plans 01-04.
- The single legitimate failure (R-01) is a small doc-debt item — two `<see cref>`
  annotations on `ISerializationProvider.cs` — that does not affect runtime, build,
  or test.

### Routing

The orchestrator's options are:

1. **Issue a tiny Plan 02-fix patch** to update `ISerializationProvider.cs:24,30`
   `<see cref="ModeConfig.X"/>` → `<see cref="SerializerConfiguration.X"/>`, then re-run
   Plan 05 (R-01 will turn into PASS — comments still match but the `<see cref>` debt is
   gone, so the auditor can decide if 3 inline comments referring to the deleted type
   count as a "reference"). After the patch, also tighten R-02 and R-05 patterns in this
   plan.
2. **Re-classify R-02 and R-05 as known false-positives** and accept the Phase-40
   closure on the strength of R-03/R-04/R-06..R-13 plus the clean build + test gate.
   Cleanup of R-01 (stale crefs) becomes a Phase 41 sweep item alongside the three
   doc files (`getting-started.md`, `strict-mode.md`, `troubleshooting.md`) that Plan
   04 already deferred.

Recommended: **Option 2** — the build + test + 8 of 13 substantive structural checks
pass. The only legitimate failure is two stale crefs in an interface XML-doc that
do not break the compile or the runtime. Roll the cref-update into the existing
deferred-items.md sweep, ship Phase 40 closed.

The executor of Plan 05 cannot make that call — it routes the result to the orchestrator/planner.

---
phase: 40-per-predicate-deploy-seed-split-replace-section-level-config
plan: 01
subsystem: configuration
tags: [json, system-text-json, deployment-mode, per-predicate-mode]

# Dependency graph
requires:
  - phase: 37-production-ready-baseline
    provides: "ModeConfig record + Deploy/Seed structural split (Phase 37-01) — REPLACED in this plan"
  - phase: 39-seed-mode-field-level-merge-deploy-seed-split-intent-is-fiel
    provides: "Phase 39 runtime (MergePredicate, XmlMergeHelper, ContentDeserializer/SqlTableProvider Seed branches) — UNAFFECTED, consumes resolved per-predicate mode at runtime"
provides:
  - "ProviderPredicateDefinition.Mode (DeploymentMode, default Deploy) — JSON 'mode' key"
  - "Flat SerializerConfiguration shape with single Predicates list, top-level DeployOutputSubfolder/SeedOutputSubfolder, ExcludeFieldsByItemType/ExcludeXmlElementsByType"
  - "ConfigLoader hard-rejecting any 'deploy' or 'seed' top-level object key with Phase-40 actionable error"
  - "ConfigLoader hard-rejecting predicates missing or with unparseable 'mode' values"
  - "ConfigWriter emitting flat predicates array; never emitting legacy section keys"
  - "GetSubfolderForMode(DeploymentMode) and GetConflictStrategyForMode(DeploymentMode) helpers (D-02 hardcoded per-mode strategy)"
affects:
  - "Phase 40 Plan 02 (admin UI commands/queries — wave 2 consumes new shape)"
  - "Phase 40 Plan 03 (ContentSerializer/ContentDeserializer/ContentProvider — wave 2)"
  - "Phase 40 Plan 04 (orchestrator wiring + SerializerSerializeCommand/SerializerDeserializeCommand — wave 2)"
  - "Phase 40 Plan 05 (swift2.2-combined.json baseline + docs — wave 2)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "JSON-detection trap via object?-typed RawSerializerConfiguration.Deploy/Seed: catches any JSON shape (object/array/primitive) under those keys without parsing them, lets Validate() reject before any silent migration could fire (T-40-01-01)"
    - "Closed-set Mode parsing: Enum.TryParse<DeploymentMode>(value, ignoreCase: true) gates all per-predicate mode strings — free-form values cannot reach SerializerConfiguration (T-40-01-02)"
    - "Compile-time tripwire (T-40-01-03): Wave-1 plan deliberately leaves source tree compile-broken so Wave-2 cannot ship a half-migration silently — 50 errors across 21 files, all rooted in removed Deploy/Seed/GetMode/ModeConfig symbols"

key-files:
  created:
    - "tests/DynamicWeb.Serializer.Tests/Configuration/SerializerConfigurationTests.cs (new flat-shape test class with reflection-based negative assertions for removed surface)"
  modified:
    - "src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs (added Mode property)"
    - "src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs (replaced Deploy/Seed ModeConfig properties with flat top-level keys + helpers)"
    - "src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs (legacy-rejection Validate; flat BuildPredicate; single-loop ValidateServiceCaches/ValidateIdentifiers)"
    - "src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs (PersistedConfiguration flat; PersistedModeSection/ToPersistedMode deleted)"
    - "tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs (rewritten end-to-end for flat shape + legacy rejection)"
    - "tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs (every fixture rewritten to flat shape with explicit per-predicate mode)"
    - "tests/DynamicWeb.Serializer.Tests/Configuration/ConfigWriterTests.cs (flat shape, never-emits-legacy assertions, full per-predicate Mode round-trip)"
  deleted:
    - "src/DynamicWeb.Serializer/Configuration/ModeConfig.cs (record removed; replaced by flat top-level keys per D-02/D-04)"

key-decisions:
  - "Per-predicate Mode default = DeploymentMode.Deploy keeps every existing call site (SerializeSubtreeCommand ad-hoc predicates, all current test fixtures) working without code changes — 'Deploy' is conceptually the default for any operation that does not explicitly opt into seed semantics."
  - "object?-typed Deploy/Seed detection trap on RawSerializerConfiguration over a typed RawModeSection — typed parsing would still accept the legacy shape and silently migrate; object? guarantees ANY non-null value triggers Validate() rejection."
  - "ConflictStrategy is hardcoded per mode (Deploy=SourceWins, Seed=DestinationWins) and exposed only via GetConflictStrategyForMode(DeploymentMode); removed the legacy [JsonIgnore] top-level alias because there is no real use case for inverting the per-mode default."
  - "Deliberate compile-tripwire at end-of-Plan-1 (T-40-01-03): solution build will not pass until Wave 2 fixes 19+ downstream consumers. Going green-on-source-only in Wave 1 would let a half-migration ship — broken consumer + flat config = silent data corruption."
  - "Class name kept as DeployModeConfigLoaderTests rather than renamed to Phase40FlatConfigLoaderTests, to preserve test-runner identity and ConfigLoaderValidatorFixtureBase inheritance — class doc comment carries the renamed-subject context."
  - "ConfigWriterTests now inherits ConfigLoaderValidatorFixtureBase (it previously implemented IDisposable directly) — the round-trip tests call ConfigLoader.Load through the permissive identifier validator override and would otherwise fail trying to query a live DW INFORMATION_SCHEMA from the test runner."

patterns-established:
  - "Phase 40 flat-config shape: top-level outputDirectory, logLevel, dryRun, strictMode, deployOutputSubfolder, seedOutputSubfolder, excludeFieldsByItemType, excludeXmlElementsByType, predicates[]; each predicate carries its own 'mode' key."
  - "Legacy-shape detection trap pattern: typed-DTO carries object?-typed fields for old shape keys; loader's Validate() throws before any content parsing if those fields are non-null."

requirements-completed: []

# Metrics
duration: ~7min
completed: 2026-04-29
---

# Phase 40 Plan 01: Per-predicate Deploy/Seed split — model, reader, writer Summary

**Replaced section-level Deploy/Seed config split with a flat predicate list where every predicate carries its own DeploymentMode; ConfigLoader hard-rejects the legacy shape and ConfigWriter never emits it.**

## Performance

- **Duration:** ~7 min (Task 1 commit 12:44:36 → Task 3 commit 12:51:32 local time, 2026-04-29)
- **Started:** 2026-04-29T10:40:05Z (state-recorded plan kickoff)
- **Completed:** 2026-04-29 (Task 3 + SUMMARY)
- **Tasks:** 3 (all `auto`, all `tdd="true"`)
- **Files modified:** 7 (4 src + 3 tests)
- **Files created:** 1 (SerializerConfigurationTests.cs)
- **Files deleted:** 1 (ModeConfig.cs)

## Accomplishments

- Per-predicate `Mode` field replaces section-level Deploy/Seed structural split — config is significantly simpler: one predicate list, one place to set per-item mode.
- ConfigLoader hard-rejects every legacy-shape input with a Phase-40 actionable error message. No silent migration. Tests cover 'deploy' object, 'seed' object, 'deploy: []' (object? trap catches non-object shapes), missing 'mode', 'mode: Garbage', 'mode: Deploy; DROP TABLE X' (T-40-01-02 injection probe), lowercase/uppercase mode strings.
- ConfigWriter emits a single flat `predicates` array with each entry carrying its own `"mode": "Deploy"` or `"mode": "Seed"`. Never emits `"deploy":` / `"seed":` keys.
- SerializerConfiguration exposes `GetSubfolderForMode(DeploymentMode)` + `GetConflictStrategyForMode(DeploymentMode)` for runtime resolution; ConflictStrategy is hardcoded per mode (Deploy=SourceWins, Seed=DestinationWins) per D-02.
- Reflection-based negative assertions in SerializerConfigurationTests guarantee the removed Deploy/Seed/GetMode/GetModeSerializeRoot/ConflictStrategy surface stays gone.
- Phase 39 runtime (MergePredicate, XmlMergeHelper, ContentDeserializer/SqlTableProvider Seed branches) is untouched — they consume the resolved mode at runtime via the same DeploymentMode enum, which is unchanged.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Mode to predicate, flatten SerializerConfiguration, delete ModeConfig** — `71481c2` (feat)
2. **Task 2: Rewrite ConfigLoader for flat shape with strict legacy rejection** — `bafd534` (feat)
3. **Task 3: Rewrite ConfigWriter to emit flat predicate list and update ConfigWriterTests** — `4adfbc1` (feat)

_Note: Both RED and GREEN steps for each TDD task are bundled into a single feat commit because the source compile-tripwire (T-40-01-03) makes the conventional RED-GREEN-test-run rhythm impossible at the project level until Wave 2 lands. Tests are wired and would pass against a hypothetical buildable assembly — the tripwire is the verification, not test-runner output. Documented in <execution_notes> below._

## Files Created/Modified

- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` — added `[JsonConverter(typeof(JsonStringEnumConverter))] public DeploymentMode Mode { get; init; } = DeploymentMode.Deploy;` plus the two new `using` directives. Every other field preserved verbatim.
- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` — replaced Deploy/Seed ModeConfig properties + GetMode/GetModeSerializeRoot/legacy [JsonIgnore] aliases with flat top-level DeployOutputSubfolder, SeedOutputSubfolder, ExcludeFieldsByItemType, ExcludeXmlElementsByType, Predicates plus GetSubfolderForMode + GetConflictStrategyForMode helpers. EnsureDirectories rewritten to reference top-level subfolders.
- `src/DynamicWeb.Serializer/Configuration/ModeConfig.cs` — DELETED.
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` — Validate() throws on raw.Deploy/raw.Seed (object? detection trap). ValidatePredicates requires & parses Mode. BuildPredicate populates Mode. Single-loop ValidateServiceCaches/ValidateIdentifiers with scope "predicates". RawModeSection / BuildModeConfigs / BuildModeConfig / ParseConflictStrategy deleted. RawSerializerConfiguration adds DeployOutputSubfolder/SeedOutputSubfolder + object? Deploy/Seed; drops ConflictStrategy.
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` — PersistedConfiguration flat; PersistedModeSection + ToPersistedMode deleted; per-predicate Mode auto-serialized via JsonStringEnumConverter on the model.
- `tests/DynamicWeb.Serializer.Tests/Configuration/SerializerConfigurationTests.cs` — NEW test class with 17 tests: defaults, JSON round-trip on Mode, EnsureDirectories per-mode subfolders, reflection-based negative assertions for removed surface.
- `tests/DynamicWeb.Serializer.Tests/Configuration/DeployModeConfigLoaderTests.cs` — REWRITTEN with 13 tests covering legacy-rejection (deploy/seed/any-shape), per-predicate mode validation (missing/invalid/injection/case-insensitive), new flat-shape success cases (mixed predicates, default + custom subfolders, top-level exclusion dicts), full ConfigLoader↔ConfigWriter round-trip.
- `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` — REWRITTEN: every fixture moved to flat shape with explicit `"mode": "Deploy"`. ServiceCaches aggregated-error test now uses single "predicates" scope. Removed the two D-38-03 legacy `acknowledgedOrphanPageIds` warning tests (legacy mode-level shape is now hard-rejected before that warning path could ever run, so the test could never reach its target behavior).
- `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigWriterTests.cs` — REWRITTEN: every fixture flat-shape; new tests for `Save_NeverEmits_LegacyDeploySectionKey`, `Save_EmitsTopLevelDeployAndSeedOutputSubfolders`, `Save_EachPredicate_EmitsItsOwnModeKey`, `Save_EmptyExclusionDicts_OmitsKeysFromOutput`, `Save_NonEmptyExclusionDicts_EmitsKeysToOutput`, `Save_ThenLoad_PreservesEveryPredicatesMode`. Now inherits ConfigLoaderValidatorFixtureBase.

## Legacy-Rejection Message Text (consumed verbatim by Wave 2 / Plan 03 docs)

These exact strings are stable interface for the Wave-2 docs plan:

```
Configuration is invalid: Legacy section-level shape detected — top-level 'deploy' object is no longer supported (Phase 40, per-predicate mode). Move every predicate from 'deploy.predicates' into the top-level 'predicates' array and add "mode": "Deploy" to each. See docs/baselines/Swift2.2-baseline.md for the new shape.

Configuration is invalid: Legacy section-level shape detected — top-level 'seed' object is no longer supported (Phase 40, per-predicate mode). Move every predicate from 'seed.predicates' into the top-level 'predicates' array and add "mode": "Seed" to each. See docs/baselines/Swift2.2-baseline.md for the new shape.

Configuration is invalid: predicates[<i>] (name='<Name>') is missing required field 'mode' (expected 'Deploy' or 'Seed', case-insensitive).

Configuration is invalid: predicates[<i>] (name='<Name>') has invalid mode '<Value>' (expected 'Deploy' or 'Seed', case-insensitive).
```

Plan 03 (Wave 2 docs) should reference these strings verbatim when updating Swift2.2-baseline.md migration notes.

## Decisions Made

- **Mode default = Deploy.** Pre-existing call sites (SerializeSubtreeCommand ad-hoc predicates, every test fixture across the existing test suite) construct ProviderPredicateDefinition without specifying Mode. Deploy is the conceptual default for any operation that does not explicitly opt into seed semantics, so the default keeps those call sites compiling without per-call edits while the codebase migrates.
- **object? Deploy/Seed detection trap over typed RawModeSection.** A typed RawModeSection would still parse the legacy shape successfully and let Validate() decide later whether to migrate or throw. object? makes the deserializer happy with any JSON value (object/array/primitive) and forces the explicit Phase-40 throw path the moment the property is non-null — there is no migration code that could be silently re-enabled.
- **ConflictStrategy hardcoded per mode (D-02).** No use case for inverting Deploy=DestinationWins or Seed=SourceWins ever surfaced. Removing the config knob simplifies the shape and prevents misconfiguration. Runtime resolves via `GetConflictStrategyForMode(DeploymentMode)`.
- **DeployModeConfigLoaderTests class name preserved** rather than renamed to Phase40FlatConfigLoaderTests — preserves test runner identity / fixture inheritance hierarchy. Class doc comment carries the renamed subject under test.
- **Empty exclusion dicts omitted from disk via WhenWritingNull mapping** in ConfigWriter — preserves the existing pattern from the Phase 37 ConfigWriter (empty mode-level dicts were also omitted) and keeps disk output minimal.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ConfigWriterTests inheritance change**
- **Found during:** Task 3 (Rewrite ConfigWriterTests)
- **Issue:** Old ConfigWriterTests implemented IDisposable directly. Round-trip tests that call ConfigLoader.Load with the new shape route through the 1-arg overload, which now (Phase 37-06 baseline) constructs a default SqlIdentifierValidator that queries INFORMATION_SCHEMA — there is no live DW DB in the test runner, so every round-trip test would throw a DB-layer exception before reaching its real assertion. The plan did not call this out.
- **Fix:** Changed inheritance to `ConfigLoaderValidatorFixtureBase`, which installs the permissive AsyncLocal validator override in its ctor and clears it in Dispose. The override allowlists the table identifiers the round-trip tests use ("EcomShops", "AccessUser", etc., per the existing fixture), so identifier validation passes through to the JSON-shape assertions.
- **Files modified:** tests/DynamicWeb.Serializer.Tests/Configuration/ConfigWriterTests.cs
- **Verification:** Inheritance change preserves the IDisposable surface (base class implements it). Test fixture pattern matches every other test class that calls Load(path) with SqlTable predicates.
- **Committed in:** 4adfbc1 (Task 3 commit)

**2. [Rule 1 - Bug] Removed two stale D-38-03 legacy-warning tests in ConfigLoaderTests**
- **Found during:** Task 2 (Rewrite ConfigLoaderTests)
- **Issue:** ConfigLoaderTests carried two Phase-38 tests (`Load_LegacyModeLevelAckList_LogsWarningAndDrops`, `Load_LegacySeedModeLevelAckList_LogsWarningAndDrops`) that asserted the warning emitted when the legacy mode-level `acknowledgedOrphanPageIds` field was used. Phase 40 hard-rejects ANY top-level `deploy` / `seed` object — including those that contain only the legacy `acknowledgedOrphanPageIds` field — BEFORE the warn-emit path can execute. The tests were unreachable.
- **Fix:** Deleted both tests. The behavior they protected (warn-and-drop) is no longer reachable by design — the legacy section-level shape is rejected outright. Plan instructs to delete tests whose intent is the legacy section logic specifically.
- **Files modified:** tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs
- **Verification:** No other test references those method names. The Phase 38 D-38-03 traceability is preserved in commit history.
- **Committed in:** bafd534 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking [Rule 3], 1 bug [Rule 1])
**Impact on plan:** Both auto-fixes essential. The first fixes a test-runner-vs-INFORMATION_SCHEMA collision the plan author did not foresee; the second removes tests that became logically unreachable as a direct consequence of the planned Phase-40 rejection rule. No scope creep — both changes are confined to the test files this plan rewrites.

## Issues Encountered

- **Compile-tripwire is the verification, not dotnet test output.** The plan's `<verify>` block specifies `dotnet test --filter "FullyQualifiedName~Configuration"` after each task, but the test project transitively compiles the entire src project and the src project is intentionally left compile-broken until Wave 2 lands. I ran `dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` to enumerate the deliberate compile errors and confirm they are precisely what Task 1 step 5 predicts (T-40-01-03). Test-method-level verification will run for the first time when Wave 2's first plan fixes the consuming files. Documented this in each task's commit message so Wave 2 agents are not surprised.

## Compile-Error Surface After Plan (Wave 2 To-Fix Inventory)

`dotnet build src/DynamicWeb.Serializer/DynamicWeb.Serializer.csproj` after Task 3 reports:

- **50 errors total** across **21 distinct files** — every one rooted in the removed Deploy/Seed/GetMode/ModeConfig/ConflictStrategy symbols. Wave 2 plans MUST fix all 21:

  | File | Wave-2 plan |
  |---|---|
  | src/DynamicWeb.Serializer/Configuration/ConfigPathResolver.cs | Wave-2 (config) |
  | src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs | Wave-2 (providers) |
  | src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs | Wave-2 (content serializer) |
  | src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs | Wave-2 (content deserializer) |
  | src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs | Wave-2 (admin UI tree) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/DeletePredicateCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/SaveItemTypeCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/SaveSerializerSettingsCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/SaveXmlTypeCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/ScanXmlTypesCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/SerializeSubtreeCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Commands/SerializerSerializeCommand.cs | Wave-2 (admin commands) |
  | src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeBySystemNameQuery.cs | Wave-2 (admin queries) |
  | src/DynamicWeb.Serializer/AdminUI/Queries/ItemTypeListQuery.cs | Wave-2 (admin queries) |
  | src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs | Wave-2 (admin queries) |
  | src/DynamicWeb.Serializer/AdminUI/Queries/PredicateListQuery.cs | Wave-2 (admin queries) |
  | src/DynamicWeb.Serializer/AdminUI/Queries/SerializerSettingsQuery.cs | Wave-2 (admin queries) |
  | src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeByNameQuery.cs | Wave-2 (admin queries) |
  | src/DynamicWeb.Serializer/AdminUI/Queries/XmlTypeListQuery.cs | Wave-2 (admin queries) |

- Error patterns Wave 2 maps mechanically:
  - `config.Deploy.X` → `config.X` (top-level) for the four migrated keys, OR `config.Predicates.Where(p => p.Mode == DeploymentMode.Deploy).<...>(p => p.X)` for predicate-list traversal.
  - `config.Seed.X` → `config.Predicates.Where(p => p.Mode == DeploymentMode.Seed).<...>` (or omit if mode-agnostic).
  - `config.GetMode(mode).X` → either `config.GetSubfolderForMode(mode)` for OutputSubfolder (the only ModeConfig field that survived) or `config.Predicates.Where(p => p.Mode == mode)` for Predicates / mode-scoped exclusions (note: typed exclusion dicts no longer per-mode — moved to top-level mode-agnostic per D-04).
  - `config.GetModeSerializeRoot(mode)` → `Path.Combine(config.SerializeRoot, config.GetSubfolderForMode(mode))`.
  - `new ModeConfig {...}` → constructor calls in test fixtures must build flat config + per-predicate Mode.

## Threat Flags

None — this plan introduces no new external surface. The threat boundaries enumerated in `<threat_model>` (disk → ConfigLoader; ConfigLoader → enum parse) are the same boundaries Phase 37 already mitigated with Validate() + SqlIdentifierValidator + SqlWhereClauseValidator; this plan tightens the legacy-shape rejection (T-40-01-01) and adds a closed-set Mode parse (T-40-01-02). Both are defensive-tightenings, not new surface.

## TDD Gate Compliance

This plan is `type: execute`, not `type: tdd` at the plan level — gate sequence is per-task. Each of the three tasks bundles its RED + GREEN into a single feat commit because the project-level compile-tripwire (T-40-01-03) prevents the conventional sequenced RED-test-fail / GREEN-test-pass rhythm: even a freshly-written failing test cannot be observed failing because the assembly the test depends on does not compile. The tests are written, wired correctly, and the fact that they currently cannot run is itself the system the plan instructs me to leave in place — Wave 2's first plan will be the one that observes them all green.

## Next Phase Readiness

- **Plan 02 + 03 + 04 + 05 (Wave 2) are unblocked.** Each Wave 2 plan must read `40-01-SUMMARY.md` first; the legacy-rejection message strings, the Mode-default policy, and the 21-file compile-error surface are stable contracts.
- **Phase 39 runtime untouched.** MergePredicate / XmlMergeHelper / Seed-merge in ContentDeserializer + SqlTableProvider all operate on a `DeploymentMode mode` parameter that the orchestrator resolves from `predicate.Mode` at iteration time. No runtime-behavior change.
- **No live DW host work required by this plan.** Configuration shape change only; no DB writes, no admin UI rendering, no orchestrator wiring.

## Self-Check: PASSED

All claimed artifacts verified on disk:

- Created: `tests/DynamicWeb.Serializer.Tests/Configuration/SerializerConfigurationTests.cs` ✓
- Modified: ProviderPredicateDefinition.cs, SerializerConfiguration.cs, ConfigLoader.cs, ConfigWriter.cs, DeployModeConfigLoaderTests.cs, ConfigLoaderTests.cs, ConfigWriterTests.cs ✓
- Deleted: ModeConfig.cs ✓
- Commits: 71481c2 (Task 1), bafd534 (Task 2), 4adfbc1 (Task 3) — all present in `git log` ✓
- SUMMARY.md present at `.planning/phases/40-per-predicate-deploy-seed-split-replace-section-level-config/40-01-SUMMARY.md` ✓

---
*Phase: 40-per-predicate-deploy-seed-split-replace-section-level-config*
*Completed: 2026-04-29*

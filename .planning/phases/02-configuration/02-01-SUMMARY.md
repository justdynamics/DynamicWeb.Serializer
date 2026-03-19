---
phase: 02-configuration
plan: "01"
subsystem: configuration
tags: [configuration, json, predicates, tdd]
dependency_graph:
  requires: []
  provides: [SyncConfiguration, PredicateDefinition, ConfigLoader, ContentPredicate, ContentPredicateSet]
  affects: [phase-03-serialization, phase-04-deserialization, phase-05-scheduling]
tech_stack:
  added: []
  patterns: [record-types, static-loader, path-boundary-matching, tdd]
key_files:
  created:
    - src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs
    - src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs
    - src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs
    - src/Dynamicweb.ContentSync/Configuration/ContentPredicate.cs
    - tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderTests.cs
    - tests/Dynamicweb.ContentSync.Tests/Configuration/ContentPredicateTests.cs
  modified: []
decisions:
  - "System.Text.Json with PropertyNameCaseInsensitive for camelCase JSON config format"
  - "Raw nullable deserialization model in ConfigLoader for clear validation error messages (avoids required-property exceptions)"
  - "Path boundary check via starts-with-slash prevents /Customer Center2 matching /Customer Center"
  - "ContentPredicateSet as separate class from ContentPredicate for aggregate OR evaluation"
  - "OrdinalIgnoreCase for all path comparisons — case-insensitive without culture sensitivity"
metrics:
  duration: 2 minutes
  completed_date: "2026-03-19"
  tasks_completed: 2
  files_created: 6
  files_modified: 0
---

# Phase 2 Plan 01: Configuration System and Predicate Evaluator Summary

**One-liner:** JSON config loader with fail-fast validation and case-insensitive path-boundary predicate include/exclude evaluator, no DynamicWeb dependencies.

## What Was Built

The configuration control plane for ContentSync: a typed JSON configuration model, a loader with descriptive validation errors, and a predicate system that evaluates content paths against include/exclude rules.

### Task 1: Configuration Model and JSON Loader

Created the `Dynamicweb.ContentSync.Configuration` namespace with three classes:

- **SyncConfiguration** (`src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs`) — top-level config record with `OutputDirectory` (required), `LogLevel` (default "info"), and `Predicates` list.
- **PredicateDefinition** (`src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs`) — predicate rule record with `Name`, `Path`, `AreaId` (all required), and `Excludes` (default empty list).
- **ConfigLoader** (`src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs`) — static `Load(filePath)` method using System.Text.Json with `PropertyNameCaseInsensitive`. Uses a two-step approach: deserialize to a raw nullable model, then validate and map to the typed model. This produces clear `InvalidOperationException` messages naming the missing field, rather than generic JSON deserialization errors.

Validation rules: `FileNotFoundException` for missing files; `InvalidOperationException` for empty `outputDirectory`, null/empty `predicates` array, and any predicate missing `name`, `path`, or `areaId > 0`.

**Tests:** 10 tests in `ConfigLoaderTests` — valid load, null excludes defaulting to empty, logLevel defaulting to info, missing file, and 6 validation error cases.

### Task 2: Predicate Include/Exclude Evaluator

Created the evaluator in `ContentPredicate.cs`:

- **ContentPredicate** — single-predicate evaluator. `ShouldInclude(contentPath, areaId)` checks areaId match, then path inclusion via `IsUnderPath()` (OrdinalIgnoreCase exact match or starts-with-slash boundary check), then exclude rules.
- **ContentPredicateSet** — aggregate evaluator constructed from `SyncConfiguration`. Returns true if ANY predicate includes the path (OR logic).

The path boundary check (`candidatePath.StartsWith(basePath + "/", OrdinalIgnoreCase)`) correctly prevents `/Customer Center2` matching `/Customer Center` while allowing `/Customer Center/Products`.

**Tests:** 15 tests in `ContentPredicateTests` — 6-case [Theory] for basic path matching, 3-case [Theory] for exclude override, 3-case [Theory] for multiple excludes, and 3 [Fact] tests for ContentPredicateSet OR logic.

## Test Results

Full test suite: 53 tests, 0 failures.
- Phase 1 tests (FileSystemStore, etc.): 28 passing (unchanged)
- ConfigLoaderTests: 10 passing
- ContentPredicateTests: 15 passing

## Deviations from Plan

None — plan executed exactly as written.

## Decisions Made

1. **Raw model for JSON deserialization** — Used a private `RawSyncConfiguration` with nullable properties for deserialization, then mapped to the typed model after validation. This produces descriptive error messages ("predicate[0] is missing required field 'path'") rather than generic `JsonException` from `required` property constraints.

2. **Path boundary check implementation** — `IsUnderPath` checks for exact match OR starts-with `basePath + "/"`. The slash suffix is the key that prevents `/Customer Center2` from matching `/Customer Center`. This is a standard path-based predicate pattern.

3. **OrdinalIgnoreCase throughout** — All path comparisons use `StringComparison.OrdinalIgnoreCase` for case-insensitive, culture-neutral matching.

4. **ContentPredicateSet as peer class** — Placed in the same file as `ContentPredicate` for cohesion. Both are in the `Dynamicweb.ContentSync.Configuration` namespace.

## Self-Check: PASSED

Files confirmed to exist:
- src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs: FOUND
- src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs: FOUND
- src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs: FOUND
- src/Dynamicweb.ContentSync/Configuration/ContentPredicate.cs: FOUND
- tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderTests.cs: FOUND
- tests/Dynamicweb.ContentSync.Tests/Configuration/ContentPredicateTests.cs: FOUND

Commits verified:
- 1d33639: test(02-01): add failing ConfigLoaderTests
- 9cfdbf8: feat(02-01): implement SyncConfiguration, PredicateDefinition, and ConfigLoader
- 65a5b9d: test(02-01): add failing ContentPredicateTests
- 70b2413: feat(02-01): implement ContentPredicate and ContentPredicateSet

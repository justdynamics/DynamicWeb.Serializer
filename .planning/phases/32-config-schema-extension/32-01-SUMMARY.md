---
phase: 32-config-schema-extension
plan: 01
subsystem: configuration
tags: [config, schema, dictionaries, backward-compat]
dependency_graph:
  requires: []
  provides: [ExcludeFieldsByItemType, ExcludeXmlElementsByType]
  affects: [ConfigLoader, SerializerConfiguration, ConfigWriter]
tech_stack:
  added: []
  patterns: [null-coalesce-to-empty-dictionary, Dictionary<string,List<string>>-config-property]
key_files:
  created: []
  modified:
    - src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs
    - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
    - tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs
decisions:
  - No changes needed to ConfigWriter -- JsonSerializer handles Dictionary<string, List<string>> natively with camelCase policy
metrics:
  duration: 118s
  completed: 2026-04-09T16:14:46Z
  tasks_completed: 2
  tasks_total: 2
  tests_added: 4
  tests_total_passing: 30
---

# Phase 32 Plan 01: Config Schema Extension Summary

Extended config model with ExcludeFieldsByItemType and ExcludeXmlElementsByType typed dictionaries, enabling per-item-type and per-XML-type exclusion settings for downstream v0.6.0 UI phases.

## Commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 | be26382 | feat(32-01): add ExcludeFieldsByItemType and ExcludeXmlElementsByType to config model |
| 2 | cc2c5c4 | test(32-01): add typed exclusion dictionary tests for load, backward compat, and round-trip |

## Task Details

### Task 1: Add dictionary properties to SerializerConfiguration and ConfigLoader

Added two `Dictionary<string, List<string>>` properties to `SerializerConfiguration` with empty default initializers. Added matching nullable properties to the private `RawSerializerConfiguration` class in `ConfigLoader`. Added null-coalesce mapping in `Load()` method to convert nullable raw values to non-nullable empty dictionaries.

### Task 2: Add config loading and round-trip tests for new dictionaries

Added 4 new test methods to `ConfigLoaderTests`:
- `Load_ConfigWithExcludeFieldsByItemType_DeserializesDictionary` -- verifies multi-key dictionary with list values
- `Load_ConfigWithExcludeXmlElementsByType_DeserializesDictionary` -- verifies single-key dictionary
- `Load_ConfigWithoutTypedDictionaries_DefaultsToEmptyDictionaries` -- backward compatibility (CFG-02)
- `Save_ThenLoad_RoundTripsTypedDictionaries` -- ConfigWriter/ConfigLoader round-trip fidelity

All 30 ConfigLoaderTests pass (26 existing + 4 new).

## Decisions Made

1. **No ConfigWriter changes needed** -- `JsonSerializer.Serialize()` with `JsonNamingPolicy.CamelCase` handles `Dictionary<string, List<string>>` natively, producing correct camelCase JSON keys.

## Deviations from Plan

None -- plan executed exactly as written.

## Verification

- `dotnet build src/DynamicWeb.Serializer` exits 0 (21 warnings, 0 errors)
- `dotnet test --filter ConfigLoaderTests` exits 0 (30 passed, 0 failed)
- SerializerConfiguration.cs has both new dictionary properties with empty defaults
- ConfigLoader.cs maps nullable raw to non-nullable with empty-dict defaults

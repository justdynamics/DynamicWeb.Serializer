# Phase 38 — Deferred Items

Items discovered during Phase 38 execution that are OUT OF SCOPE of the current plan
per the `<deviation_rules>` scope boundary. Pre-existing issues unrelated to the
plan's changes.

## From Plan 38-03 execution (2026-04-21)

### Integration tests require live DW runtime (pre-existing)

**Discovered during:** Task 1 full-suite verification.

**Issue:** All 9 tests in `tests/DynamicWeb.Serializer.IntegrationTests/` fail with:
```
Dynamicweb.Extensibility.Dependencies.DependencyResolverException:
  The Dependency Locator was not initialized properly.
```

These tests (`CustomerCenterSerializationTests`, `CustomerCenterDeserializationTests`)
try to use `Dynamicweb.Content.PageService.GetCache` which expects an initialized
DW `DependencyResolver` — which only exists inside a running DW host process.

**Scope assessment:** Pre-existing environmental setup issue. Not caused by C.1
dedup fix (which only touches filename generation). The unit test suite
`tests/DynamicWeb.Serializer.Tests/` runs cleanly in CI without a live DW host
(643/643 passing); the integration suite was evidently designed for local-host
execution only.

**Deferred:** Not in Plan 38-03's scope (`files_modified` does not include
IntegrationTests or any DI bootstrapper). Potential future work: a separate
phase to either:
1. Wire the integration tests to a disposable DW host/in-memory fixture, or
2. Mark them `[Trait("Category","RequiresLiveHost")]` and exclude from default runs.

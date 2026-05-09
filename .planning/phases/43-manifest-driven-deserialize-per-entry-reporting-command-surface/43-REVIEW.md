---
phase: 43-manifest-driven-deserialize-per-entry-reporting-command-surface
reviewed: 2026-05-09T00:00:00Z
depth: standard
files_reviewed: 22
files_reviewed_list:
  - src/DynamicWeb.Serializer/Reporting/EntryStatus.cs
  - src/DynamicWeb.Serializer/Reporting/ProviderCounts.cs
  - src/DynamicWeb.Serializer/Reporting/EntryOutcome.cs
  - src/DynamicWeb.Serializer/Configuration/SerializerPathResolver.cs
  - src/DynamicWeb.Serializer/Infrastructure/StrictModeDeprecationWarning.cs
  - src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs
  - src/DynamicWeb.Serializer/Providers/ISerializationProvider.cs
  - src/DynamicWeb.Serializer/Providers/SerializationProviderBase.cs
  - src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs
  - src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/DeserializeFromZipCommand.cs
  - src/DynamicWeb.Serializer/Infrastructure/Manifest.cs
  - tests/DynamicWeb.Serializer.Tests/Helpers/ToPredicateExtensions.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SerializerOrchestratorTests.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/SerializerDeserializeCommandTests.cs
  - tests/DynamicWeb.Serializer.Tests/Integration/StrictModeIntegrationTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/Content/ContentProviderTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/EcomXmlMergeTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderCoercionTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderDeserializeTests.cs
  - tests/DynamicWeb.Serializer.Tests/Providers/SqlTable/SqlTableProviderSeedMergeTests.cs
findings:
  critical: 0
  warning: 4
  info: 6
  total: 10
status: issues_found
---

# Phase 43: Code Review Report

**Reviewed:** 2026-05-09T00:00:00Z
**Depth:** standard
**Files Reviewed:** 22
**Status:** issues_found

## Summary

Phase 43 ships the manifest-driven deserialize pivot end-to-end: `EntryStatus` /
`ProviderCounts` / `EntryOutcome` reporting types are clean, the new
manifest-driven `DeserializeAll(modeRoot, mode, ...)` correctly threads through
`ManifestWriter.Read`, FK reorder runs on entries, and `EntryOutcome.From / Skipped /
Failed / RunLevelError` factories cover the four `EntryStatus` values 1:1. The
`StrictModeResolver.Resolve(entryPoint, configValue: null, requestValue: ...)` literal
is grep-friendly per CONTEXT D-04 and the one-shot deprecation warning peeks via
`JsonDocument` without going through `ConfigLoader.Load`.

The most serious defect is in the **`HasErrors` aggregation contract**: the doc-comment
claims `EntryOutcomes.Any(e => e.Status == Failed)` covers exactly the same surface
as the dropped `DeserializeResults.Any(r => r.HasErrors)` clause, but the legacy
`[Obsolete] DeserializeAll(predicates, ...)` overload does NOT populate `EntryOutcomes`
— it only fills `DeserializeResults`. Any caller still on the legacy overload with a
provider returning `Failed > 0` will observe `HasErrors == false`, a silent regression
that escapes test coverage because no `[Obsolete]`-path test asserts `HasErrors == true`
when only `result.Failed > 0` is set (the `Errors` list is also empty in that scenario).

Other findings flag the dead `EntryStatus.Warned` enum value (no production code path
creates it), the empty `catch` swallow in `StrictModeDeprecationWarning`, an empty
log gate at the run-level error site, and the inconsistent canonical-surface usage
between `AdviceGenerator` (DeserializeResults) and `SerializerDeserializeCommand`
summary builder (EntryOutcomes).

## Warnings

### WR-01: `OrchestratorResult.HasErrors` regression on legacy `DeserializeAll(predicates,...)` overload

**File:** `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs:728-731`
**Issue:** `HasErrors` aggregates from `Errors` + `SerializeResults.Any(r => r.HasErrors)` +
`EntryOutcomes.Any(e => e.Status == Failed)`. The doc comment (lines 717-727) asserts that
the dropped `DeserializeResults.Any(r => r.HasErrors)` clause is fully covered by
`EntryOutcomes`, because `EntryOutcome.From` propagates `ProviderDeserializeResult.HasErrors`
into `EntryStatus.Failed`. **That equivalence only holds for the new manifest-driven path.**
The legacy `[Obsolete] DeserializeAll(predicates, ...)` body (lines 166-360) populates
`results: List<ProviderDeserializeResult>` and writes them only into `DeserializeResults`
(line 359) — `EntryOutcomes` stays empty. Run-level `errors` collects only the no-provider /
strict-mode branches; per-row `Failed > 0` from a provider does NOT bubble into `errors`.

A legacy caller whose provider returns `ProviderDeserializeResult { Failed = 5 }` (no run-level
exception, no orchestrator-level error) will observe `result.HasErrors == false` despite
five rows having failed. This is a behavior regression versus the pre-Phase-43
`DeserializeResults.Any(r => r.HasErrors)` clause and is not caught by tests because no
`[Obsolete]`-path test asserts `HasErrors == true` for the `Failed > 0 && Errors.Empty`
shape (the strict-mode tests pass because escalator adds to `errors` directly).

**Fix:** Either (a) restore the `DeserializeResults.Any(r => r.HasErrors)` clause
in `HasErrors` until Phase 44 deletes the legacy overload, OR (b) make the legacy
DeserializeAll body project each `ProviderDeserializeResult` into an `EntryOutcome`
and add to `EntryOutcomes` so the canonical surface is populated regardless of entry path.
Option (a) is the smallest diff:
```csharp
public bool HasErrors =>
    Errors.Count > 0 ||
    SerializeResults.Any(r => r.HasErrors) ||
    DeserializeResults.Any(r => r.HasErrors) ||  // restore until CONVERGE-04 deletes legacy overload
    EntryOutcomes.Any(e => e.Status == EntryStatus.Failed);
```
Add a regression test against the legacy overload: provider returning
`ProviderDeserializeResult { Failed = 1, TableName = "X" }` with no escalator, must
yield `HasErrors == true`.

### WR-02: `EntryStatus.Warned` is dead — no production code path produces it

**File:** `src/DynamicWeb.Serializer/Reporting/EntryOutcome.cs:39-60` (factory) +
`src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs:563` (only call site)
**Issue:** `EntryOutcome.From(entry, r, duration, warnings)` produces `EntryStatus.Warned`
when `warnings` is non-empty and `r.HasErrors` is false. The orchestrator's
`DeserializeEntries` dispatch loop at line 563 calls `EntryOutcome.From(entry, result, sw.Elapsed)`
without the `warnings` parameter — defaults to `null`. No other production call site invokes
`EntryOutcome.From` with warnings populated. Result: `EntryStatus.Warned` is an unused enum value.

This is misleading: the public reporting contract advertises four statuses, but consumers will
never see `Warned`. Strict-mode warnings are routed through `EntryOutcome.RunLevelError`
(Status=Failed) at line 626, not as per-entry warnings. The XmlDoc on `EntryOutcome.From`
explicitly documents the Warned branch (line 36-37) which is currently unreachable.

**Fix:** Either (a) thread per-entry warnings into the dispatch loop — capture warnings
that the wrapped log accumulates per-entry (would require a per-entry warning buffer
or routing the escalator's `RecordOnly` per-entry instead of run-level), OR (b) drop
`Warned` from `EntryStatus` and the `EntryOutcome.From` warning branch until a future phase
needs it. Option (b) reduces the public surface to match production reality:
```csharp
public enum EntryStatus { Succeeded, Failed, Skipped }
```
If keeping `Warned` for v0.7.0 forward-compat, document on the enum that no current
dispatch path produces it and update the XmlDoc on `From` accordingly.

### WR-03: `StrictModeDeprecationWarning` swallows ALL exceptions in catch block

**File:** `src/DynamicWeb.Serializer/Infrastructure/StrictModeDeprecationWarning.cs:40-43`
**Issue:** The `try/catch` wrapping the `JsonDocument.Parse` peek catches every `Exception`
silently:
```csharp
catch
{
    // Peek failure is non-fatal — the deserialize path doesn't need this; leave silent.
}
```
This hides `OutOfMemoryException`, `IOException` from a transient file-share failure,
permission errors, and any other unexpected runtime exception. The XmlDoc says "Silent
on absent file, missing property, or any JSON-parse failure" — but the implementation
also silences I/O and OS-level failures that an operator would want to know about.

**Fix:** Narrow to expected exception types so genuine bugs surface:
```csharp
catch (JsonException) { /* malformed JSON — non-fatal advisory, swallow */ }
catch (IOException) { /* file race or permission — non-fatal advisory, swallow */ }
// don't catch the rest
```
This preserves the "advisory only" semantics for the documented failure modes while
letting unexpected exceptions reach the caller's outer try/catch in
`SerializerDeserializeCommand.Handle` (which already wraps and reports as Error).

### WR-04: SerializerDeserializeCommand passes `Log` (instance method) to `StrictModeDeprecationWarning` BEFORE the log file is initialised

**File:** `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs:115-138`
**Issue:** Line 115: `_logFile = LogFileWriter.CreateLogFile(...)`. Line 116:
`Log("=== Serializer Deserialize (API) started ===")`. The `Log` method appends to
`_logLines` (in-memory buffer). Lines 138 calls `StrictModeDeprecationWarning.EmitIfLegacyValueSet(configPath, Log)`.
The `Log` method writes ONLY to `_logLines`; it does NOT flush to `_logFile` until
`FlushLog(_logFile, summary)` at line 181 is called.

That's actually fine for the happy path — the WARNING ends up in `_logLines` and gets
flushed at the end. But if `DeserializeAll` THROWS (line 192 outer catch), the catch
block at lines 192-194 returns an Error CommandResult **without flushing `_logLines`**.
Every accumulated log line, including the deprecation WARNING, is lost. The legacy
`SerializeAll`/zip command has the same issue, but the WARNING here is a NEW
diagnostic surface — operators expect to see it on every run, including failed ones.

**Fix:** Wrap the throw-prone region in a try/finally that flushes the log on the way out:
```csharp
try
{
    var result = orchestrator.DeserializeAll(...);
    // build summary, flush, return
}
catch (Exception ex)
{
    Log($"ERROR: Deserialization failed: {ex.Message}");
    if (_logFile != null)
    {
        var failSummary = new LogFileSummary { /* minimal */ };
        try { FlushLog(_logFile, failSummary); } catch { }
    }
    return new() { Status = CommandResult.ResultType.Error, Message = $"Deserialization failed: {ex.Message}" };
}
```

## Info

### IN-01: `AdviceGenerator` still drives off `DeserializeResults` while command summary drives off `EntryOutcomes` — split-brain canonical surface

**File:** `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs:158`
**Issue:** Line 158 calls `AdviceGenerator.GenerateAdvice(result)`, which (in
`Infrastructure/AdviceGenerator.cs:16`) iterates `result.DeserializeResults` for
its FK / "group not found" / "duplicate" pattern advice. The summary block at lines
164-180 drives off `result.EntryOutcomes`. Phase 43 designates `EntryOutcomes` as
canonical and `DeserializeResults` as transient (per the `OrchestratorResult` doc-comments
at line 692-696). The advice generator was not migrated, so it sees only the per-table
results; orchestrator-level synthetic outcomes (e.g. `RunLevelError` from strict-mode
escalation) are missed by advice generation.

The new `DeserializeEntries` does still populate `legacyResults` into `DeserializeResults`
(line 562), so the advice generator's input is non-empty for the new path — but it
won't see `RunLevelError` or `Skipped` outcomes, which are the new diagnostics worth
pattern-matching on.

**Fix:** Phase 44 / CONVERGE-04 candidate: migrate `AdviceGenerator` to consume
`EntryOutcomes`. For Phase 43, document the split explicitly via a comment near
the `GenerateAdvice` call site, or mirror the pattern via:
```csharp
// Phase 44: migrate to EntryOutcomes — DeserializeResults is transient.
var advice = AdviceGenerator.GenerateAdvice(result);
```

### IN-02: `OrchestratorResult.Summary` `else if (DeserializeResults.Count > 0)` branch is dead post-Phase-43

**File:** `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs:756-765`
**Issue:** The doc-comment at lines 745-747 explicitly says "fall back to DeserializeResults
for the transient state where Task 2 has shipped but Task 6 has not. The else-if branch
is removed in Task 6." Phase 43 completed all 9 tasks but the `else if (DeserializeResults.Count > 0)`
branch is still present. This is dead code — the new path always populates `EntryOutcomes`
with at least one entry per dispatched provider call (or a Skipped outcome), AND the legacy
`[Obsolete] DeserializeAll(predicates, ...)` body never populates `EntryOutcomes` so the
fallback IS still reached when callers go through the legacy overload.

If the comment is correct (Task 6 should remove it), that's a missed cleanup. If the comment
is stale (legacy overload still drives the fallback), the comment is the bug.

**Fix:** Update the comment to reflect reality (the fallback is needed until CONVERGE-04
deletes the legacy overload), e.g.:
```csharp
// Fallback for the [Obsolete] DeserializeAll(predicates, ...) overload — that body
// only populates DeserializeResults, not EntryOutcomes. Phase 44 / CONVERGE-04
// deletes both the overload and this fallback.
```
Or remove the fallback if the legacy overload's body is updated to project entry outcomes
into `EntryOutcomes`.

### IN-03: `EntryOutcome.RunLevelError` uses literal `"<run-level>"` for both `EntryId` and `ProviderType`

**File:** `src/DynamicWeb.Serializer/Reporting/EntryOutcome.cs:104-115`
**Issue:** `RunLevelError` returns an outcome with `EntryId = "<run-level>"` and
`ProviderType = "<run-level>"`. The angle-bracket literals are visible in the per-entry
log line (`SerializerOrchestrator.cs:566`-style format) and the `LogFileSummary.Predicates`
list built in `SerializerDeserializeCommand.cs:164-173` (which projects `o.EntryId` to
`PredicateSummary.Name`). A run with strict-mode escalation will produce a log entry
like `[<run-level>] Failed: Strict mode: 3 warning(s) ...`. The angle brackets imply a
synthetic / pseudo-id, which is intent, but the discriminator is shared between two
distinct fields (no provider can have ProviderType `"<run-level>"` registered).

If multiple run-level errors ever occur in a single run, they would all share the same
EntryId, breaking any downstream consumer that expects unique EntryIds. Currently this
is impossible (escalator throws once per run), but the contract is fragile.

**Fix:** Either reserve and document the literal explicitly:
```csharp
public const string RunLevelEntryId = "<run-level>";
public const string RunLevelProviderType = "<run-level>";
```
…and validate that no provider can register with the reserved type, OR generate unique
EntryIds per run-level error (e.g. `$"<run-level-{Guid.NewGuid():N}>"`). The former
captures intent; the latter is forward-compat for multiple synthetic errors.

### IN-04: `SerializerPathResolver.EnsureDirectories` doesn't validate `filesSystemDir` parameter

**File:** `src/DynamicWeb.Serializer/Configuration/SerializerPathResolver.cs:35-54`
**Issue:** `EnsureDirectories(string filesSystemDir)` immediately calls
`Path.Combine(filesSystemDir, DefaultOutputDirectory)` without null/empty check.
Passing `null` throws `ArgumentNullException` deep in `Path.Combine`. Passing `""`
returns `Path.GetFullPath(DefaultOutputDirectory)` which resolves relative to the
current working directory — a very surprising silent fallback for a config-free helper
introduced specifically to remove ambient dependencies.

The single production caller (`SerializerDeserializeCommand.cs:104-106`) computes
`Path.Combine(filesRoot, "System")` from `Path.GetDirectoryName(configPath)!` which
is guaranteed non-null, so the bug is latent. But the method is `public static` and
the XmlDoc invites third-party callers.

**Fix:**
```csharp
public static SerializerPaths EnsureDirectories(string filesSystemDir)
{
    if (string.IsNullOrEmpty(filesSystemDir))
        throw new ArgumentException("filesSystemDir must be a non-empty path", nameof(filesSystemDir));
    // ... existing body
}
```

### IN-05: `ToPredicateExtensions.ToManifestEntry` for SqlTable falls back to `string.Empty` when `predicate.Table` is null

**File:** `tests/DynamicWeb.Serializer.Tests/Helpers/ToPredicateExtensions.cs:65`
**Issue:** Line 65: `Table = predicate.Table ?? string.Empty`. The shim is the bridge
that lets Layer B Phase 43 tests dispatch through `provider.Deserialize` without
rewriting fixtures. If a test predicate accidentally has `Table = null` (e.g. a typo
in a new fixture), the shim silently produces a `SqlTableEntry` with `Table = ""`,
which then drives the synthesised SqlTableProvider's `tableName` to `""` and
`metadata.TableName` queries downstream. The first failure point is far from the
typo and the diagnostic ("Table not found") will be confusing.

For the production `SqlTableProvider.BuildManifestEntry` path (line 162 of
`SqlTableProvider.cs`) the equivalent expression is `predicate.Table!` (null-forgiving)
because validation rejects null Table predicates upstream. The shim doesn't have that
guard.

**Fix:** Throw fast on null Table to match production validation semantics:
```csharp
if (string.IsNullOrEmpty(predicate.Table))
    throw new InvalidOperationException(
        $"ToManifestEntry: SqlTable predicate '{predicate.Name}' has null/empty Table");
```
Test-only code, so the throw cost is zero in production.

### IN-06: `EntryOutcome` constants `"<run-level>"` are stringly-typed; the synthetic ID gets propagated into `LogFileSummary.Predicates[].Name`

**File:** `src/DynamicWeb.Serializer/AdminUI/Commands/SerializerDeserializeCommand.cs:166-167`
**Issue:** Lines 166-167:
```csharp
Name = o.EntryId,
Table = o.EntryId,
```
For a `RunLevelError` outcome, `o.EntryId` is `"<run-level>"`. Both `Name` and `Table`
fields of the resulting `PredicateSummary` end up as `"<run-level>"`. The summary log
viewer will display a row with both columns identical and angle-bracketed.

This works (no crash) but is awkward UX: a strict-mode escalation will appear as a
"predicate" row in the summary, when conceptually it's a run-level diagnostic that
should sit outside the per-predicate table.

**Fix:** Filter run-level outcomes from the `Predicates` list, surfacing them only via
`Errors`:
```csharp
Predicates = result.EntryOutcomes
    .Where(o => o.EntryId != "<run-level>")
    .Select(o => new PredicateSummary { /* ... */ })
    .ToList(),
```
Tighter coupling to the literal — but combined with IN-03's proposed
`EntryOutcome.RunLevelEntryId` constant, the `.Where` becomes self-documenting.

---

_Reviewed: 2026-05-09T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

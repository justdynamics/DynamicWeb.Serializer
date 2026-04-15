---
phase: 36-area-screens
reviewed: 2026-04-15T00:00:00Z
depth: standard
files_reviewed: 12
files_reviewed_list:
  - src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs
  - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
  - src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs
  - src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs
  - src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs
  - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
  - tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs
  - src/DynamicWeb.Serializer/Models/SerializedArea.cs
  - src/DynamicWeb.Serializer/Serialization/ContentMapper.cs
  - src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs
  - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
findings:
  critical: 0
  warning: 4
  info: 3
  total: 7
status: issues_found
---

# Phase 36: Code Review Report

**Reviewed:** 2026-04-15
**Depth:** standard
**Files Reviewed:** 12
**Status:** issues_found

## Summary

This phase delivers area-level predicate screens: `PredicateEditScreen`, `PredicateByIndexQuery`, `SavePredicateCommand`, plus new `ExcludeAreaColumns` plumbing that flows from the model through config loading, serialization, and deserialization. The overall design is sound — the provider-branching pattern, D-02 provider-type lock, cascade-skip semantics, and dry-run path are all correctly implemented and well-tested.

Four warnings were found, all logic/correctness issues. No critical issues or security vulnerabilities. Three informational items follow.

---

## Warnings

### WR-01: `ParseConflictStrategy` silently ignores unrecognised values with no warning

**File:** `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs:76`

**Issue:** The `else` branch of `ParseConflictStrategy` falls through to `return ConflictStrategy.SourceWins` with only a comment. An unrecognised strategy string (e.g., a typo like `"source_wins"` or a future enum member) silently becomes `SourceWins` with no log or exception. The comment says "Unknown values default to source-wins" which hides misspellings from operators loading a broken config.

```csharp
// current — silently swallows bad values
private static ConflictStrategy ParseConflictStrategy(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return ConflictStrategy.SourceWins;
    if (string.Equals(value, "source-wins", StringComparison.OrdinalIgnoreCase))
        return ConflictStrategy.SourceWins;
    return ConflictStrategy.SourceWins; // Unknown values default to source-wins
}
```

**Fix:** Emit a warning (or throw) on unrecognised values. Since a `Console.Error.WriteLine` pattern is already used in `Load()` for the directory check, the same pattern works here:

```csharp
private static ConflictStrategy ParseConflictStrategy(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return ConflictStrategy.SourceWins;
    if (string.Equals(value, "source-wins", StringComparison.OrdinalIgnoreCase))
        return ConflictStrategy.SourceWins;
    Console.Error.WriteLine(
        $"[Serializer] Warning: Unknown conflictStrategy '{value}'. Defaulting to 'source-wins'.");
    return ConflictStrategy.SourceWins;
}
```

---

### WR-02: `WriteAreaProperties` builds a dynamic SQL `UPDATE` with column names interpolated directly from YAML data

**File:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:1200-1205`

**Issue:** Column names read from the serialized YAML are interpolated directly into the SQL string:

```csharp
cb.Add($"UPDATE [Area] SET [{kvp.Key}] = {{0}}", kvp.Value ?? DBNull.Value);
// ...
cb.Add($", [{kvp.Key}] = {{0}}", kvp.Value ?? DBNull.Value);
```

The *value* is parameterised (safe), but `kvp.Key` is a column name embedded as a string literal in the SQL text. If YAML is ever edited by hand or produced by an adversarial source, a key containing `]` or other SQL syntax could corrupt the statement. In the current threat model (config file controlled by developers) this is low-severity, but it is a real injection vector. The `CommandBuilder` bracket-quoting (`[...]`) does not escape embedded `]` characters.

**Fix:** Validate column names against an allowed-list before building the SQL. The `ReadAreaProperties` method in `ContentMapper` already queries the live schema via `GetColumnTypes("Area")` or a similar `SELECT *` approach. Re-use that column set:

```csharp
// Build allowed-column set once from the schema before iterating properties
var allowedColumns = new HashSet<string>(knownAreaColumns, StringComparer.OrdinalIgnoreCase);

foreach (var kvp in properties)
{
    if (excludeAreaColumns?.Contains(kvp.Key) == true) continue;
    if (!allowedColumns.Contains(kvp.Key))
    {
        Log($"WARNING: Skipping unknown Area column '{kvp.Key}' — not in schema.");
        continue;
    }
    // ... safe to interpolate kvp.Key here
}
```

Alternatively, add a simple regex guard: `if (!Regex.IsMatch(kvp.Key, @"^[A-Za-z_][A-Za-z0-9_]*$")) continue;`.

---

### WR-03: `DeserializePredicate` proceeds with a `null` `SerializedArea` when `ReadTree` returns null

**File:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:95-97`

**Issue:** `_store.ReadTree(...)` can return null (e.g., when the area directory does not exist on disk — the area was never serialised, or the name does not match). The result is passed directly to `DeserializePredicate` without a null check:

```csharp
var area = _store.ReadTree(_configuration.OutputDirectory, areaName);
var result = DeserializePredicate(predicate, area, globalPageGuidCache, allAreaPages);
```

`DeserializePredicate` has signature `(ProviderPredicateDefinition predicate, SerializedArea area, ...)` — `area` is not nullable in the signature, so a null reference here causes a `NullReferenceException` at first access inside that method (line 215: `area.Properties.Count`).

**Fix:** Guard and skip gracefully before calling `DeserializePredicate`:

```csharp
var area = _store.ReadTree(_configuration.OutputDirectory, areaName);
if (area == null)
{
    var skipMsg = $"Warning: No serialized data found for predicate '{predicate.Name}' " +
                  $"(area '{areaName}' not found in '{_configuration.OutputDirectory}'). Skipping.";
    Log(skipMsg);
    allErrors.Add(skipMsg);
    continue;
}
var result = DeserializePredicate(predicate, area, globalPageGuidCache, allAreaPages);
```

---

### WR-04: `PredicateByIndexQuery.GetModel()` throws on a malformed config instead of returning null

**File:** `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs:25`

**Issue:** `ConfigLoader.Load(configPath)` is called without a try/catch. If the config file is malformed (JSON parse error, missing required field, etc.), the exception propagates to the DW admin framework, which will surface as an unhandled error page rather than a graceful "could not load configuration" state. The `SavePredicateCommand` wraps its call in `try/catch` (line 154) — this query should do the same.

**Fix:**

```csharp
public override PredicateEditModel? GetModel()
{
    if (Index < 0)
        return new PredicateEditModel();

    var configPath = ConfigPathResolver.FindConfigFile();
    if (configPath == null) return null;

    SerializerConfiguration config;
    try
    {
        config = ConfigLoader.Load(configPath);
    }
    catch (Exception)
    {
        // Malformed config — return null so the screen can show an appropriate message
        return null;
    }

    if (Index >= config.Predicates.Count) return null;
    // ... rest of mapping
}
```

---

## Info

### IN-01: `ProviderPredicateDefinition` mutable `List<string>` properties risk unintended sharing

**File:** `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs:34,40,50`

**Issue:** Three `List<string>` properties (`Excludes`, `ServiceCaches`, `ExcludeAreaColumns`) use `= new()` as the default initialiser on a `record`. Because `init`-only setters are used, callers using `with` expressions will shallow-copy the record but share the same list instances for any property not explicitly overridden. Mutation of the list in one copy would affect others. This is unlikely to be triggered today but is a latent hazard for any future code that mutates these lists after a `with` copy.

**Fix:** Either document the lists as read-only by convention, or switch to `IReadOnlyList<string>` / `ImmutableList<string>` for all three properties to make the immutability contract explicit.

---

### IN-02: `ContentMapper.ReadAreaProperties` issues a `SELECT *` on every page-tree serialisation

**File:** `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs:298-299`

**Issue:** `SELECT * FROM [Area] WHERE [AreaID] = {0}` returns every column in the Area table. For large installations the Area table may have many custom columns. This is a minor concern but the query runs once per predicate (not once per page), so it is tolerable. No action required unless performance becomes an issue — noting here for completeness.

**Fix:** No immediate action needed; the existing filter logic on `excludeAreaColumns` and the DTO-column removal below it are correct.

---

### IN-03: Bare `catch { /* skip unreadable areas */ }` in cross-area link resolution swallows all exceptions silently

**File:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:126`

**Issue:**

```csharp
catch { /* skip unreadable areas */ }
```

This swallows the exception without any log output. If `ReadTree` throws due to a corrupted YAML file, or a disk permission error, there is no trace in the log output. This makes debugging link-resolution failures difficult.

**Fix:** Log the skipped area and the exception message:

```csharp
catch (Exception ex)
{
    Log($"  Warning: Could not read area directory '{Path.GetFileName(areaDir)}' for link resolution: {ex.Message}");
}
```

---

_Reviewed: 2026-04-15_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

---
phase: 33-sqltable-column-pickers
reviewed: 2026-04-09T00:00:00Z
depth: standard
files_reviewed: 2
files_reviewed_list:
  - src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs
  - tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs
findings:
  critical: 1
  warning: 2
  info: 1
  total: 4
status: issues_found
---

# Phase 33: Code Review Report

**Reviewed:** 2026-04-09
**Depth:** standard
**Files Reviewed:** 2
**Status:** issues_found

## Summary

Two files reviewed: the new `PredicateEditScreen` with `CheckboxList` column pickers, and the extended `PredicateCommandTests` covering round-trip persistence of `ExcludeFields` and `XmlColumns`. The screen logic and test coverage are generally solid. One critical SQL injection vulnerability was found in `DataGroupMetadataReader.GetColumnTypes`, which is directly invoked from `CreateColumnCheckboxList`. Two warnings cover a silent exception swallow in the screen and a missing `WithReloadOnChange` on the `Table` field that will break the column pickers in practice.

---

## Critical Issues

### CR-01: SQL Injection in `DataGroupMetadataReader.GetColumnTypes` via user-supplied table name

**File:** `src/DynamicWeb.Serializer/Providers/SqlTable/DataGroupMetadataReader.cs:31`

**Issue:** `GetColumnTypes` builds its SQL query by directly interpolating `tableName` into the query string:

```csharp
cb.Add($"SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'");
```

`tableName` originates from `Model.Table`, a free-text field typed by the user in the admin UI and passed straight through `CreateColumnCheckboxList` on line 130 of `PredicateEditScreen.cs`. A malicious or misconfigured value (e.g., `'; DROP TABLE EcomOrder--`) is injected verbatim into SQL. The same pattern exists in `GetNotNullColumns` (line 51), `TableExists` (line 92), `QueryPrimaryKeyColumns` (line 102), `QueryIdentityColumns` (line 120), `QueryAllColumns` (line 138), and `QueryColumnDefinitions` (line 162) — all in the same file.

**Fix:** Use a parameterised query via `CommandBuilder`. The `INFORMATION_SCHEMA` queries should use a `@tableName` parameter:

```csharp
var cb = new CommandBuilder();
cb.Add("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName");
cb.AddParameter("@tableName", tableName);
```

Apply the same pattern to every other method in `DataGroupMetadataReader` that interpolates `tableName`. For `QueryAllColumns`, the `SELECT TOP 0 * FROM [{tableName}]` form cannot use parameters for identifiers — validate `tableName` against a whitelist pattern (e.g., `^[A-Za-z_][A-Za-z0-9_]*$`) before use, then keep the bracket-quoted form.

---

## Warnings

### WR-01: Silent `catch` in `CreateColumnCheckboxList` swallows all exceptions

**File:** `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs:154`

**Issue:** The `catch` block catches every `Exception` and replaces `editor.Explanation` with a generic message. This silently discards connection errors, permission errors, and programming mistakes (e.g., null reference inside `GetColumnTypes`). In production it masks real failures; during development it hides bugs.

```csharp
catch
{
    editor.Explanation = "Could not query database columns.";
}
```

**Fix:** At minimum, catch only expected exceptions (e.g., `SqlException` / `DbException`) and re-throw or log the rest. If a logging API is available in the DW context, log before swallowing:

```csharp
catch (Exception ex)
{
    // Log for diagnostics; show safe message to user
    Logging.Logger.Warning($"[PredicateEditScreen] Column query failed for table '{tableName}': {ex.Message}");
    editor.Explanation = "Could not query database columns.";
}
```

---

### WR-02: `Table` field has no `WithReloadOnChange` — column pickers never populate on first entry

**File:** `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs:49`

**Issue:** `EditorFor(m => m.Table)` is rendered as a plain text field with no `WithReloadOnChange()` call. The two column-picker `CheckboxList` editors (`NameColumn` is not a picker, but `ExcludeFields` and `XmlColumns` are) both call `CreateColumnCheckboxList(Model?.Table, ...)`, which requires `Model.Table` to have a value at render time. Without a reload trigger on `Table`, the screen renders with empty checkbox lists and the user has no mechanism to populate them without manually reloading the page. This is the same pattern that `AreaId` correctly uses (line 72) via `WithReloadOnChange()`.

**Fix:** Override `GetEditor` for `Table` and apply `WithReloadOnChange()`:

```csharp
nameof(PredicateEditModel.Table) => new TextInput
{
    Label = "Table",
    Explanation = "SQL table name (e.g., EcomOrderFlow)"
}.WithReloadOnChange(),
```

---

## Info

### IN-01: `Save_Content_NewPredicate_PersistsContentFields` asserts `CompareColumns` is null, but model default is `string.Empty`

**File:** `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs:316`

**Issue:** The test asserts `Assert.Null(pred.CompareColumns)` on line 316. This is correct because `SavePredicateCommand` maps `string.IsNullOrWhiteSpace(Model.CompareColumns) ? null : ...`, and the model property defaults to `string.Empty`. The assertion is valid, but a follow-up reader may find the combination surprising: the model always carries a non-null string, while the persisted definition may be null. A brief comment at the assertion site would aid maintainability.

**Fix:** Add a short inline comment:

```csharp
Assert.Null(pred.CompareColumns); // empty string in model maps to null in persisted definition
```

---

_Reviewed: 2026-04-09_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

---
phase: 33-sqltable-column-pickers
verified: 2026-04-11T00:30:00Z
status: human_needed
score: 3/3 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Open SqlTable predicate edit screen in DW admin for a valid table (e.g. EcomOrderFlow)"
    expected: "ExcludeFields and XmlColumns both render as CheckboxLists with column names from the database — no free-text textareas visible for these two fields"
    why_human: "UI rendering of CheckboxList vs Textarea can only be confirmed in a live DW admin session — automated checks confirm the conditional branching in code but cannot render the DW CoreUI framework"
  - test: "Open SqlTable predicate edit screen for a table name that does not exist in the database"
    expected: "Both CheckboxLists show empty Options and display a warning message ('Table not found in database. Verify the table name.')"
    why_human: "Requires live database connection to verify the zero-column fallback path renders correctly in the UI"
  - test: "Open an existing SqlTable predicate that has ExcludeFields populated (e.g. ['OrderFlowID']), then re-open edit screen"
    expected: "The relevant column checkbox is pre-checked when the screen loads"
    why_human: "Pre-check behaviour (editor.Value being set to the List<string>) is only observable in the rendered UI — automated code inspection confirms the Value assignment but not the visual pre-check state"
---

# Phase 33: SqlTable Column Pickers Verification Report

**Phase Goal:** SqlTable predicate editing uses auto-populated column selectors instead of free-text entry
**Verified:** 2026-04-11T00:30:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SqlTable predicate edit screen shows ExcludeFields as a CheckboxList populated from the table's actual SQL column schema | ✓ VERIFIED | `PredicateEditScreen.cs` lines 88-95: `GetEditor` returns `CreateColumnCheckboxList(...)` for `ExcludeFields` when `ProviderType == "SqlTable"` |
| 2 | SqlTable predicate edit screen shows XmlColumns as a CheckboxList populated from the table's actual SQL column schema | ✓ VERIFIED | `PredicateEditScreen.cs` lines 96-103: same conditional pattern for `XmlColumns` |
| 3 | Selections persist to config JSON and are applied during serialize/deserialize | ✓ VERIFIED | `SavePredicateCommand` parses the newline-separated string (model stays `string` per D-05); `SqlTableProvider.cs` lines 50-52 consume `predicate.ExcludeFields` and `predicate.XmlColumns` in both serialize and deserialize paths; 5 round-trip tests pass |

**Score:** 3/3 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` | CheckboxList editors for ExcludeFields and XmlColumns when ProviderType is SqlTable | VERIFIED | Contains `CreateColumnCheckboxList` (lines 112-166), `new CheckboxList`, `GetColumnTypes` call, `using DynamicWeb.Serializer.Providers.SqlTable`, `new Textarea` for non-SqlTable branch, "Table not found in database" warning |
| `tests/DynamicWeb.Serializer.Tests/AdminUI/PredicateCommandTests.cs` | Round-trip tests for CheckboxList-style values | VERIFIED | Contains all 5 required test methods (lines 500-649); 25 PredicateCommand tests pass, 0 failures |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `PredicateEditScreen.GetEditor` | `DataGroupMetadataReader.GetColumnTypes` | `CreateColumnCheckboxList` instantiates `new DataGroupMetadataReader(new DwSqlExecutor())` and calls `GetColumnTypes(tableName)` | WIRED | Confirmed at `PredicateEditScreen.cs` lines 136-137 |
| `PredicateEditScreen.GetEditor` | `CheckboxList` | Conditional editor based on `ProviderType == "SqlTable"` | WIRED | `new CheckboxList` returned at lines 114-119 within `CreateColumnCheckboxList`; Content branch returns `new Textarea` (lines 91-95, 100-103) |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `PredicateEditScreen.cs` (CheckboxList for ExcludeFields/XmlColumns) | `editor.Options` (column names) | `DataGroupMetadataReader.GetColumnTypes(tableName)` → INFORMATION_SCHEMA.COLUMNS query in `DwSqlExecutor` | Yes — DB query with real column introspection; zero-column case handled as warning | FLOWING |
| `PredicateEditScreen.cs` (pre-check state) | `editor.Value` | `Model?.ExcludeFields` / `Model?.XmlColumns` split by `\r\n` | Yes — parsed from config string loaded by `PredicateByIndexQuery` | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build compiles with 0 errors | `dotnet build src/DynamicWeb.Serializer` | 0 errors, 22 warnings (all pre-existing) | PASS |
| All 25 PredicateCommand tests pass | `dotnet test --filter "PredicateCommand"` | 25 passed, 0 failed | PASS |
| Commits documented in SUMMARY exist in git | `git log --oneline c62ccbd efda64a` | Both commits confirmed present | PASS |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| PRED-04 | 33-01-PLAN.md | SqlTable predicate excludeFields uses CheckboxList populated from table schema instead of textarea | SATISFIED | `GetEditor` returns `CreateColumnCheckboxList` for ExcludeFields when ProviderType is SqlTable; round-trip test `Save_SqlTable_ExcludeFields_RoundTrips` passes |
| PRED-05 | 33-01-PLAN.md | SqlTable predicate xmlColumns uses CheckboxList populated from table schema instead of textarea | SATISFIED | `GetEditor` returns `CreateColumnCheckboxList` for XmlColumns when ProviderType is SqlTable; round-trip test `Save_SqlTable_XmlColumns_RoundTrips` passes |

No orphaned requirements: REQUIREMENTS.md maps PRED-04 and PRED-05 to Phase 33 and both are accounted for by 33-01-PLAN.md.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| None found | — | — | — | — |

Scan notes: No TODO/FIXME/placeholder comments in modified files. No `return null` or `return {}` stubs. The `catch` block at line 162 sets `Explanation` rather than silently swallowing — appropriate defensive handling. The inline table name validation regex (line 128) is a safe guard added beyond the plan spec.

### Human Verification Required

#### 1. CheckboxList Renders for Valid SqlTable Predicate

**Test:** In DW admin, open a SqlTable predicate edit screen for a predicate pointing at an existing table (e.g. EcomOrderFlow on a populated DW instance). Observe the Filtering group.
**Expected:** ExcludeFields and XmlColumns both display as CheckboxList controls with column names as checkbox options — not free-text textareas.
**Why human:** DW CoreUI framework rendering cannot be verified by static code inspection. The code correctly branches to `CreateColumnCheckboxList` but only a live admin session confirms the framework renders it as a visual checkbox picker.

#### 2. Empty/Missing Table Shows Warning, Not Error

**Test:** In DW admin, open a new SqlTable predicate edit screen before entering a table name, or enter a table name that does not exist in the database.
**Expected:** Both CheckboxLists are empty (no checkboxes shown) and display the warning text "Enter a table name to see available columns." (no table name) or "Table not found in database. Verify the table name." (table not found). No exception or error page.
**Why human:** Requires a live database connection to exercise the zero-column fallback and the null/whitespace guard.

#### 3. Existing Exclusions Are Pre-Checked on Load

**Test:** Ensure a SqlTable predicate in config already has ExcludeFields entries (e.g. `["OrderFlowID"]`). Open the edit screen for that predicate.
**Expected:** The checkbox for "OrderFlowID" is already checked when the screen opens.
**Why human:** `editor.Value = selected` is set in code (line 158) but whether the DW CheckboxList widget visually pre-checks those options requires runtime UI observation.

### Gaps Summary

No automated gaps. All three roadmap success criteria are verified at the code level. Phase goal is structurally achieved: the conditional branching in `GetEditor` replaces Textarea with CheckboxList for both ExcludeFields and XmlColumns on SqlTable predicates, column options are populated from `DataGroupMetadataReader.GetColumnTypes` (INFORMATION_SCHEMA), existing values are pre-set as `editor.Value`, and round-trip persistence through `SavePredicateCommand` is confirmed by 5 new tests that all pass.

Three human verification items remain for UI rendering confirmation in a live DW admin environment.

---

_Verified: 2026-04-11T00:30:00Z_
_Verifier: Claude (gsd-verifier)_

---
phase: 38-production-ready-baseline-hardening-retroactive-tests-for-37
fixed_at: 2026-04-21T17:30:00Z
review_path: .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 38: Code Review Fix Report

**Fixed at:** 2026-04-21T17:30:00Z
**Source review:** .planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (Critical + Warning; Info excluded by fix_scope)
- Fixed: 3
- Skipped: 0

Note: Info-level findings (IN-01 through IN-06) were out of scope for this fix
pass and remain documented in the source review for future cleanup passes.

## Fixed Issues

### WR-01: ContentSerializer silently skips Seed predicates via legacy alias

**Files modified:** `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs`
**Commit:** abdb8ee
**Applied fix:** Replaced the legacy `_configuration.Predicates` alias with
explicit `_configuration.Deploy.Predicates` in `ContentSerializer.Serialize()`
loop at line 59. This makes the Deploy-scoped intent visible at the call site
(matching the Deploy-only exclusion dict read on line 182 and the XML docs
at line 178-180) and removes reliance on the silent alias that could mask
Seed-predicate data loss if any future caller wires them directly into
`SerializerConfiguration.Seed`. Added an inline comment citing the threat
and rationale. Behavior is unchanged for current production callers because
`ContentProvider.BuildSerializerConfiguration` already wires every predicate
into `Deploy.Predicates`.

### WR-02: Area create SQL relies on inline IDENTITY_INSERT batch execution

**Files modified:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs`
**Commit:** 924f923
**Applied fix:** Wrapped the `INSERT INTO [Area]` in a `BEGIN TRY ... END TRY
BEGIN CATCH ... END CATCH;` block in `CreateAreaFromProperties` (line 488-502)
so `SET IDENTITY_INSERT [Area] OFF` is always emitted — both on the happy
path (outer OFF terminator) and when the INSERT throws (CATCH block emits
OFF then `THROW;` to re-raise the original exception). This guarantees
the pooled connection's session state is restored even on FK violations or
duplicate AreaUniqueId failures. The existing `AreaIdentityInsertTests`
ordered-regex assertion (`ON ... INSERT ... OFF` with `Singleline` mode) still
matches because the BEGIN TRY / END TRY markers sit between ON and INSERT,
and the final OFF is on the success path. Regression for the failure path
(INSERT throws -> OFF emitted in CATCH) is a future test enhancement.

### WR-03: PowerShell auto-variable $errors shadowed in smoke script

**Files modified:** `tools/smoke/Test-BaselineFrontend.ps1`
**Commit:** 258c3ae
**Applied fix:** Renamed the script-scope `$errors` variable to
`$transportErrors` at all five usage sites (declaration at line 127, append
at line 167, summary count read at line 211, empty-check + table print at
lines 223/226, and exit-code check at line 229). This avoids shadowing the
PowerShell automatic `$Error` variable (PSScriptAnalyzer rule
`PSAvoidAssignmentToAutomaticVariable`) and preserves natural debugging
semantics for operators running the smoke test. Added an inline comment
citing the rule. Functional behavior is unchanged (pure rename). Verified
with PowerShell AST parser: no syntax errors.

## Verification

- All three fixes compiled cleanly: `dotnet build` reports 0 errors (pre-existing
  CS8604 / CS8631 warnings on unrelated files only).
- PowerShell syntax check: AST parser reports no errors on the renamed script.
- Full non-integration test suite re-run after all three commits:
  **643 passed / 0 failed / 0 skipped** — phase 38 test parity preserved.
- One transient stderr-capture flake was observed on the first full-suite run
  (`ConfigLoaderTests.Load_LegacySeedModeLevelAckList_LogsWarningAndDrops` —
  pre-existing parallel-test pollution of `Console.SetError`) but did NOT
  reproduce on re-run and is unrelated to any of the three fixed files.

## Skipped Issues

None — all in-scope findings were fixed successfully.

---

_Fixed: 2026-04-21T17:30:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

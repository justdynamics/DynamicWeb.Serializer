---
phase: 34-embedded-xml-screens
fixed_at: 2026-04-14T00:00:00Z
review_path: .planning/phases/34-embedded-xml-screens/34-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 34: Code Review Fix Report

**Fixed at:** 2026-04-14
**Source review:** .planning/phases/34-embedded-xml-screens/34-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3
- Fixed: 3
- Skipped: 0

## Fixed Issues

### CR-01: SQL injection via string interpolation in `DiscoverElementsForType`

**Files modified:** `src/DynamicWeb.Serializer/AdminUI/Infrastructure/XmlTypeDiscovery.cs`
**Commit:** 1cd0be8
**Applied fix:** Replaced both string-interpolated SQL queries in `DiscoverElementsForType` with parameterized `CommandBuilder` calls using `cb.Add(sql)` + `cb.AddParameter("@typeName", typeName)`. Removed the now-redundant regex guard block (`if (!Regex.IsMatch(...)) return elements;`) and its unused `using System.Text.RegularExpressions;` import. The existing test `DiscoverElementsForType_RejectsInvalidTypeName` continues to pass — with parameterized queries the injection attempt string simply finds no matching rows and returns empty.

### WR-01: `FakeSqlExecutor` duplicated across both test classes

**Files modified:** `tests/DynamicWeb.Serializer.Tests/TestHelpers/FakeSqlExecutor.cs` (new), `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeCommandTests.cs`, `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeDiscoveryTests.cs`
**Commit:** 72f340e
**Applied fix:** Created `tests/DynamicWeb.Serializer.Tests/TestHelpers/FakeSqlExecutor.cs` with `internal sealed class FakeSqlExecutor` and `internal static class TestTableHelper` (containing `CreateSingleColumnTable`). Removed the private duplicate definitions from both test classes. Added `using DynamicWeb.Serializer.Tests.TestHelpers;` to both test files and updated all `CreateSingleColumnTable(` call sites to `TestTableHelper.CreateSingleColumnTable(`. Removed now-unused `using System.Data;`, `using DynamicWeb.Serializer.Providers.SqlTable;`, and `using Dynamicweb.Data;` imports from both test files.

### WR-02: `XmlTypeByNameQuery.GetModel()` silently returns `null` for unknown type names

**Files modified:** `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs`
**Commit:** da0dcaa
**Applied fix:** Added a `Model is null` guard at the top of `BuildEditScreen()` that renders a read-only "Type not found" text editor with a user-readable explanation message ("This XML type no longer exists in configuration. Run 'Scan for XML types' to refresh.") and returns early, preventing a silent empty-screen render when the model is null.

---

_Fixed: 2026-04-14_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

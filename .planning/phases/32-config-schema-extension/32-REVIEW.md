---
phase: 32-config-schema-extension
reviewed: 2026-04-09T00:00:00Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs
  - src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs
  - src/DynamicWeb.Serializer/Configuration/ExclusionMerger.cs
  - src/DynamicWeb.Serializer/Serialization/ContentMapper.cs
  - src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs
  - src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs
  - tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs
  - tests/DynamicWeb.Serializer.Tests/Configuration/ExclusionMergerTests.cs
findings:
  critical: 0
  warning: 4
  info: 3
  total: 7
status: issues_found
---

# Phase 32: Code Review Report

**Reviewed:** 2026-04-09
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

This phase adds typed exclusion dictionaries (`ExcludeFieldsByItemType`, `ExcludeXmlElementsByType`) at the config root level, an `ExclusionMerger` utility that merges them with per-predicate flat lists at runtime, and wires the merged exclusions through `ContentMapper`, `ContentSerializer`, and `ContentDeserializer`. The schema extension itself is implemented cleanly and the `ExclusionMerger` logic is correct.

Four warnings were found: a silent bare `catch` in the cross-area link resolution pass, two places where a failed DW entity lookup after INSERT silently drops item fields with no error logged, and a GridRow mapping gap where item field exclusions are never applied. Three informational items cover dead code in `ParseConflictStrategy`, the always-true null guard on `globalPageGuidCache`, and a minor test coverage gap.

---

## Warnings

### WR-01: Bare catch in cross-area directory scan swallows all exceptions silently

**File:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:129`

**Issue:** The directory scan that builds the cross-area YAML page list uses a bare `catch { }` with no logging. Any failure (I/O error, corrupt YAML, unexpected exception) is swallowed completely, causing the affected area to be silently excluded from link resolution. The caller has no way to know that cross-area link resolution operated on an incomplete dataset.

**Fix:**
```csharp
catch (Exception ex)
{
    Log($"WARNING: Could not read area directory '{Path.GetFileName(areaDir)}' for link resolution: {ex.Message}");
}
```

---

### WR-02: Item fields silently lost when page re-fetch returns null after INSERT

**File:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:503-525`

**Issue:** After inserting a new page via `Services.Pages.SavePage(page)`, the code refetches the page with `Services.Pages.GetPage(saved.ID)`. If `refetched` is null (which can happen if the DW service layer fails to return the new row), the entire block — including `SaveItemFields`, layout re-apply, and `SavePropertyItemFields` — is skipped without any log message or error. The page is counted as `Created` and the caller sees success, but all Item fields are lost.

**Fix:**
```csharp
var refetched = Services.Pages.GetPage(saved.ID);
if (refetched == null)
{
    Log($"WARNING: Could not re-fetch page ID={saved.ID} after insert — Item fields not applied");
}
else
{
    // ... existing field application logic
}
```

---

### WR-03: Item fields silently lost when paragraph re-fetch returns null after INSERT

**File:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:827-868`

**Issue:** Same pattern as WR-02, but for paragraphs. After `Services.Paragraphs.SaveParagraph(para)`, the code queries `GetParagraphsByPageId(pageId).FirstOrDefault(p => p.UniqueId == dto.ParagraphUniqueId)`. If `saved == null`, item fields and all the re-save checks are skipped silently. The paragraph is counted as `Created` with no indication of the failure.

**Fix:**
```csharp
var saved = Services.Paragraphs.GetParagraphsByPageId(pageId)
    .FirstOrDefault(p => p.UniqueId == dto.ParagraphUniqueId);

if (saved == null)
{
    Log($"WARNING: Could not re-fetch paragraph {dto.ParagraphUniqueId} after insert — Item fields not applied");
    ctx.Created++;
    Log($"CREATED paragraph {dto.ParagraphUniqueId} on page {pageId}");
    return;
}
// ... existing field application logic
```

---

### WR-04: GridRow item fields are never filtered by exclusion rules

**File:** `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs:157-195`

**Issue:** `MapGridRow` does not accept `excludeFields` or `excludeFieldsByItemType` parameters. It always serializes all item fields unconditionally (lines 162-172). Every other mapping method (`MapArea`, `MapPage`, `MapParagraph`, `BuildColumns`) accepts and applies the exclusion parameters. This means exclusion rules in config — whether per-predicate `excludeFields` or global `ExcludeFieldsByItemType` — silently have no effect on GridRow item fields.

**Fix:** Add exclusion parameters to `MapGridRow` and apply them consistently with the other mappers:
```csharp
public SerializedGridRow MapGridRow(GridRow gridRow, List<SerializedGridColumn> columns,
    IReadOnlySet<string>? excludeFields = null,
    IReadOnlyDictionary<string, List<string>>? excludeFieldsByItemType = null)
{
    var effectiveExcludeFields = excludeFieldsByItemType != null
        ? ExclusionMerger.MergeFieldExclusions(
            excludeFields?.ToList() ?? new List<string>(),
            excludeFieldsByItemType,
            gridRow.ItemType)
        : excludeFields;

    var fields = new Dictionary<string, object>();
    if (!string.IsNullOrEmpty(gridRow.ItemType) && !string.IsNullOrEmpty(gridRow.ItemId))
    {
        var itemEntry = Services.Items.GetItem(gridRow.ItemType, gridRow.ItemId);
        if (itemEntry != null)
        {
            var dict = new Dictionary<string, object?>();
            itemEntry.SerializeTo(dict);
            foreach (var kvp in dict)
            {
                if (kvp.Value != null && effectiveExcludeFields?.Contains(kvp.Key) != true)
                    fields[kvp.Key] = kvp.Value;
            }
        }
    }
    // ... rest of method unchanged
```

The call site in `ContentSerializer.cs` (line 136) would also need to pass the parameters through.

---

## Info

### IN-01: Dead code branch in ParseConflictStrategy — unknown values silently default

**File:** `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs:72-79`

**Issue:** `ParseConflictStrategy` has three return statements but only two reachable paths. The check for `"source-wins"` on line 76 is redundant with the final fallback on line 78 — both return `ConflictStrategy.SourceWins`. An unknown value like `"destination-wins"` silently produces `SourceWins` with no warning, which could mask a config typo in a future-proofed scenario.

**Fix:** Emit a warning for unrecognised values rather than silently defaulting:
```csharp
private static ConflictStrategy ParseConflictStrategy(string? value)
{
    if (string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, "source-wins", StringComparison.OrdinalIgnoreCase))
        return ConflictStrategy.SourceWins;

    Console.Error.WriteLine(
        $"[Serializer] Warning: Unknown conflictStrategy '{value}'. Defaulting to 'source-wins'.");
    return ConflictStrategy.SourceWins;
}
```

---

### IN-02: Always-true null guard on globalPageGuidCache

**File:** `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs:111`

**Issue:** `globalPageGuidCache` is declared and initialized on line 91 as `new Dictionary<Guid, int>()`. The null guard `if (!_isDryRun && globalPageGuidCache != null && globalPageGuidCache.Count > 0)` on line 111 will never evaluate `globalPageGuidCache != null` as false. The redundant null check adds noise without protection.

**Fix:** Remove the null check:
```csharp
if (!_isDryRun && globalPageGuidCache.Count > 0)
```

---

### IN-03: No test covering MergeXmlExclusions with explicit null xmlTypeName

**File:** `tests/DynamicWeb.Serializer.Tests/Configuration/ExclusionMergerTests.cs`

**Issue:** The `ExclusionMergerTests` for `MergeXmlExclusions` tests with `"SomeModule"` (a non-matching key) but does not explicitly test with a `null` `xmlTypeName`. The implementation handles `null` correctly via `string.IsNullOrEmpty`, but adding an explicit test case would document and protect that contract.

**Fix:** Add a test:
```csharp
[Fact]
public void MergeXmlExclusions_NullXmlTypeName_ReturnsFlatOnly()
{
    var flat = new List<string> { "Settings" };
    var dict = new Dictionary<string, List<string>>
    {
        ["ProductListModule"] = new List<string> { "Sorting" }
    };

    var result = ExclusionMerger.MergeXmlExclusions(flat, dict, null);

    Assert.NotNull(result);
    Assert.Single(result!);
    Assert.Contains("Settings", result);
}
```

---

_Reviewed: 2026-04-09_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

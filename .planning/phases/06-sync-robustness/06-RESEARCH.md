# Phase 6: Sync Robustness - Research

**Researched:** 2026-03-20
**Domain:** Gap closure — multi-column paragraphs, dry-run diff completeness, config validation, code documentation
**Confidence:** HIGH

## Summary

Phase 6 closes four specific tech-debt items identified in the v1.0 milestone audit. All four gaps are narrowly scoped and touch code that already exists — this is enhancement/fix work, not greenfield. The changes span serialization (FileSystemStore paragraph column attribution), deserialization (LogDryRunPageUpdate PropertyFields diff), configuration (ConfigLoader OutputDirectory validation), and documentation (SerializedArea.AreaId code comments).

The most complex item is multi-column paragraph round-trip (SER-01). Currently, `FileSystemStore.WriteParagraph` writes all paragraphs from all columns flat into the grid-row folder as `paragraph-{SortOrder}.yml`, and `ReconstructColumns` puts them all back into column 1 on read. The fix requires storing column identity in the paragraph file (or filename) so read-back can distribute paragraphs to the correct `SerializedGridColumn`. The other three items are straightforward single-method changes.

**Primary recommendation:** Add a `columnId` property to `SerializedParagraph`, persist it in the YAML file, and use it in `ReconstructColumns` to distribute paragraphs back to the correct column. This is the simplest approach that preserves backward compatibility (paragraphs without `columnId` default to column 1).

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SER-01 | Serialize full content tree (Area > Page > Grid > Row > Paragraph) to YAML files | Multi-column paragraph attribution fix — paragraphs must carry their GridRowColumn identity through the file round-trip |
| DES-04 | Dry-run mode — report what would change without applying | PropertyFields (Icon, SubmenuType) must appear in dry-run diff output alongside existing Fields diff |
| CFG-01 | Standalone config file defining sync scope | OutputDirectory existence validation at load time with clear warning |
</phase_requirements>

## Architecture Patterns

### Gap 1: Multi-Column Paragraph Round-Trip (SER-01)

**Current state:**
- `ContentMapper.BuildColumns()` correctly groups paragraphs by `Paragraph.GridRowColumn` during serialization (line 163 of ContentMapper.cs)
- `FileSystemStore.WriteTree()` writes column metadata to `grid-row.yml` (columns with Id/Width but empty Paragraphs) and writes paragraph files flat in the grid-row folder as `paragraph-{SortOrder}.yml`
- `FileSystemStore.ReconstructColumns()` puts ALL paragraphs into column 1, ignoring original column attribution (lines 272-291)
- `SerializedParagraph` has NO `ColumnId` property — column identity is lost after write

**Root cause:** The paragraph YAML files contain no column identity. During write, paragraphs from columns 2+ are written to the same folder without any column discriminator. On read-back, there is no data to reconstruct which paragraph belonged to which column.

**Fix approach — add ColumnId to SerializedParagraph:**

```csharp
// In SerializedParagraph.cs — add optional column property
public record SerializedParagraph
{
    // ... existing properties ...
    public int? ColumnId { get; init; }  // null = legacy files (default to column 1)
}
```

```csharp
// In FileSystemStore.WriteParagraph section (within WritePage, line 84-95)
// Pass column.Id to each paragraph when writing
foreach (var column in gridRow.Columns)
{
    var sortedParagraphs = column.Paragraphs.OrderBy(p => p.SortOrder);
    foreach (var paragraph in sortedParagraphs)
    {
        // Stamp column identity onto paragraph before writing
        var paragraphWithColumn = paragraph with { ColumnId = column.Id };
        var paragraphFileName = $"paragraph-{paragraph.SortOrder}.yml";
        // ...
    }
}
```

```csharp
// In FileSystemStore.ReconstructColumns — use ColumnId for distribution
private static List<SerializedGridColumn> ReconstructColumns(
    List<SerializedGridColumn> columnsWithoutParagraphs,
    List<SerializedParagraph> allParagraphs)
{
    if (columnsWithoutParagraphs.Count == 0)
        return new List<SerializedGridColumn>();

    // Build a lookup of column ID -> column index
    var columnById = columnsWithoutParagraphs.ToDictionary(c => c.Id);

    // Distribute paragraphs by their ColumnId
    var columnParagraphs = new Dictionary<int, List<SerializedParagraph>>();
    foreach (var col in columnsWithoutParagraphs)
        columnParagraphs[col.Id] = new List<SerializedParagraph>();

    foreach (var para in allParagraphs)
    {
        var targetColumnId = para.ColumnId ?? columnsWithoutParagraphs[0].Id;
        if (columnParagraphs.ContainsKey(targetColumnId))
            columnParagraphs[targetColumnId].Add(para);
        else
            columnParagraphs[columnsWithoutParagraphs[0].Id].Add(para); // fallback
    }

    return columnsWithoutParagraphs
        .Select(col => col with { Paragraphs = columnParagraphs[col.Id] })
        .ToList();
}
```

**Important edge case — paragraph SortOrder collision across columns:** Currently `paragraph-{SortOrder}.yml` is the filename. If column 1 has paragraph with SortOrder=1 and column 2 also has a paragraph with SortOrder=1, they would collide in the flat folder. Two options:
1. **Include column in filename:** `paragraph-c{ColumnId}-{SortOrder}.yml` (e.g., `paragraph-c1-1.yml`, `paragraph-c2-1.yml`)
2. **Subfolder per column:** `col-1/paragraph-1.yml`, `col-2/paragraph-1.yml`

**Recommendation:** Option 1 (column in filename) is simpler, avoids extra directory nesting, and the read logic already scans for `paragraph-*.yml` files. Update the glob to match the new naming pattern.

**Backward compatibility:** The read path must handle BOTH old (`paragraph-{N}.yml`) and new (`paragraph-c{C}-{N}.yml`) filenames. Old files without ColumnId in YAML default to column 1.

### Gap 2: Dry-Run PropertyFields Diff (DES-04)

**Current state:**
- `LogDryRunPageUpdate()` (lines 800-838 of ContentDeserializer.cs) diffs scalar properties (MenuText, UrlName, Active, Sort) and Item Fields
- It does NOT diff `PropertyFields` (Icon, SubmenuType) — these are completely invisible in dry-run output
- `SavePropertyItemFields()` (lines 620-654) handles the actual write path for PropertyFields
- PropertyFields are accessed via `page.PropertyItem` which is a DW `Item` object with `SerializeTo()` / `DeserializeFrom()`

**Fix approach — add PropertyFields diff block to LogDryRunPageUpdate:**

```csharp
// In LogDryRunPageUpdate, after the Fields diff block:
// PropertyFields diffs
if (existing.PropertyItem != null)
{
    var existingPropFields = new Dictionary<string, object?>();
    existing.PropertyItem.SerializeTo(existingPropFields);

    foreach (var kvp in dto.PropertyFields)
    {
        if (ItemSystemFields.Contains(kvp.Key)) continue;
        existingPropFields.TryGetValue(kvp.Key, out var currentVal);
        var currentStr = currentVal?.ToString();
        var newStr = kvp.Value?.ToString();
        if (currentStr != newStr)
            diffs.Add($"PropertyFields[{kvp.Key}]: '{currentStr}' -> '{newStr}'");
    }
}
```

**Key insight:** The `existing` parameter is a DW `Page` object loaded from DB — `existing.PropertyItem` is accessible for reading current values. No additional DW API calls needed.

### Gap 3: OutputDirectory Validation (CFG-01)

**Current state:**
- `ConfigLoader.Validate()` checks that `OutputDirectory` is non-empty (line 36-37) but does NOT check if the directory exists on disk
- The `OutputDirectory` value is an absolute path (e.g., `C:\inetpub\Dynamicweb.net\Files\Serialization`)
- Failure happens at runtime when `FileSystemStore.ReadTree()` or `Directory.GetDirectories()` throws

**Fix approach — validate directory existence with warning (not exception):**

The serializer can CREATE the output directory if it does not exist (first serialize). The deserializer NEEDS it to exist (it reads from it). The appropriate behavior is:
- **Warning** (not throw) in `ConfigLoader.Load()` — the directory might be created by a prior serialize run
- **Clear log message** indicating the directory does not exist yet
- The deserializer already throws `InvalidOperationException` from `FileSystemStore.ReadTree()` if no area directory is found

```csharp
// In ConfigLoader.Load(), after Validate():
if (!Directory.Exists(raw.OutputDirectory))
{
    // Log a warning — the serializer will create it, but the deserializer needs it
    Console.Error.WriteLine(
        $"[ContentSync] Warning: OutputDirectory '{raw.OutputDirectory}' does not exist. " +
        "Serialization will create it; deserialization requires it to exist.");
}
```

**Alternative: Add a `ValidateForDeserialization()` method** that throws if the directory is missing. This is more precise — the serializer path should not fail on a missing directory, but the deserializer should. Could be invoked in `ContentDeserializer.Deserialize()` before `ReadTree()`.

**Recommendation:** Do both — warning in ConfigLoader for general awareness, and a clear early-exit with error message in ContentDeserializer.Deserialize() if the directory does not exist. This gives operators actionable feedback at the earliest possible point.

### Gap 4: SerializedArea.AreaId Documentation (Informational)

**Current state:**
- `SerializedArea.AreaId` is a GUID (`Area.UniqueId`) set during serialization (ContentMapper.MapArea, line 27)
- During deserialization, the target area is resolved by `predicate.AreaId` (numeric) from config — the GUID is NOT used for identity resolution
- This is by design (documented in decisions) but not documented in code

**Fix:** Add XML doc comments to `SerializedArea.AreaId` and to the deserialization code explaining why the GUID is informational only.

```csharp
/// <summary>
/// The Area's UniqueId GUID, captured from the source environment during serialization.
/// Informational only — NOT used for identity resolution during deserialization.
/// The target area is resolved by the numeric AreaId from the predicate configuration.
/// This GUID is preserved for traceability and potential future cross-environment matching.
/// </summary>
public required Guid AreaId { get; init; }
```

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YAML serialization | Custom paragraph-to-column mapping in filename parsing | ColumnId property in YAML + simple dictionary lookup | Filename-based column encoding is fragile; a first-class property is cleaner |
| Directory validation | Custom path-exists retry loop | Single `Directory.Exists()` check with clear error message | Over-engineering validation adds complexity without value |

## Common Pitfalls

### Pitfall 1: Paragraph SortOrder Collision Across Columns
**What goes wrong:** Two paragraphs in different columns with the same SortOrder produce the same filename (`paragraph-1.yml`) and overwrite each other.
**Why it happens:** The current naming scheme uses SortOrder alone, which is unique within a column but not across columns in the same grid row.
**How to avoid:** Include the column ID in the filename (e.g., `paragraph-c1-1.yml`) or use subfolder-per-column layout.
**Warning signs:** Grid rows with multiple columns where paragraphs share sort positions.

### Pitfall 2: Backward Compatibility on Paragraph Read
**What goes wrong:** Existing serialized YAML files from v1.0 don't have `ColumnId` in paragraph YAML. The new read logic must handle both formats.
**Why it happens:** The YAML files on disk were written before ColumnId was added to the model.
**How to avoid:** Treat `ColumnId` as optional (`int?`) — default to column 1 when null/missing. Make the filename pattern matcher accept both old and new formats.
**Warning signs:** Unit tests that create paragraphs without ColumnId should still pass.

### Pitfall 3: PropertyItem Null Check in Dry-Run
**What goes wrong:** `existing.PropertyItem` may be null if the page has no property item type configured. Accessing `.SerializeTo()` on null throws NRE.
**Why it happens:** Not all pages have PropertyItem configured in DW.
**How to avoid:** Null-check `existing.PropertyItem` before attempting to diff. If null but dto.PropertyFields is non-empty, log all as new additions.

### Pitfall 4: ConfigLoader Warning vs Exception
**What goes wrong:** Throwing on missing OutputDirectory breaks the serializer (which creates it). Warning-only means deserializer fails later with an unclear error.
**Why it happens:** Different consumers of the config have different needs.
**How to avoid:** Warning in ConfigLoader, explicit early validation in ContentDeserializer.

## Code Examples

### Multi-Column Test Fixture (for ContentTreeBuilder)

```csharp
/// <summary>
/// Builds a page with a 2-column grid row:
/// Column 1: paragraphs at sort 1, 2
/// Column 2: paragraphs at sort 1, 3
/// Tests multi-column round-trip attribution.
/// </summary>
public static SerializedArea BuildMultiColumnTree()
{
    var page = BuildSinglePage("Multi-Column Page") with
    {
        SortOrder = 1,
        GridRows = new List<SerializedGridRow>
        {
            new SerializedGridRow
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                Columns = new List<SerializedGridColumn>
                {
                    new SerializedGridColumn
                    {
                        Id = 1,
                        Width = 6,
                        Paragraphs = new List<SerializedParagraph>
                        {
                            new SerializedParagraph
                            {
                                ParagraphUniqueId = Guid.NewGuid(),
                                SortOrder = 1,
                                ColumnId = 1,
                                ItemType = "ContentModule",
                                Fields = new() { ["text"] = "Col1 Para1" }
                            },
                            new SerializedParagraph
                            {
                                ParagraphUniqueId = Guid.NewGuid(),
                                SortOrder = 2,
                                ColumnId = 1,
                                ItemType = "ContentModule",
                                Fields = new() { ["text"] = "Col1 Para2" }
                            }
                        }
                    },
                    new SerializedGridColumn
                    {
                        Id = 2,
                        Width = 6,
                        Paragraphs = new List<SerializedParagraph>
                        {
                            new SerializedParagraph
                            {
                                ParagraphUniqueId = Guid.NewGuid(),
                                SortOrder = 1,
                                ColumnId = 2,
                                ItemType = "ImageModule",
                                Fields = new() { ["imageUrl"] = "/images/col2.jpg" }
                            }
                        }
                    }
                }
            }
        }
    };

    return new SerializedArea
    {
        AreaId = Guid.NewGuid(),
        Name = "Test Area",
        SortOrder = 1,
        Pages = new List<SerializedPage> { page }
    };
}
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (via Dynamicweb.ContentSync.Tests.csproj) |
| Config file | tests/Dynamicweb.ContentSync.Tests/Dynamicweb.ContentSync.Tests.csproj |
| Quick run command | `dotnet test tests/Dynamicweb.ContentSync.Tests -x --no-build` |
| Full suite command | `dotnet test tests/Dynamicweb.ContentSync.Tests` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SER-01 | Multi-column paragraphs survive write/read round-trip with correct column attribution | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "MultiColumn" -x` | No - Wave 0 |
| SER-01 | Backward compatibility: old paragraph files (no ColumnId) default to column 1 | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "BackwardCompat" -x` | No - Wave 0 |
| SER-01 | Paragraph SortOrder collision across columns handled correctly | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "SortOrderCollision" -x` | No - Wave 0 |
| DES-04 | PropertyFields appear in dry-run diff output | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "PropertyFieldsDryRun" -x` | No - Wave 0 (requires DW mock or integration test) |
| CFG-01 | ConfigLoader warns on missing OutputDirectory | unit | `dotnet test tests/Dynamicweb.ContentSync.Tests --filter "OutputDirectory" -x` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Dynamicweb.ContentSync.Tests -x --no-build`
- **Per wave merge:** `dotnet test tests/Dynamicweb.ContentSync.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Dynamicweb.ContentSync.Tests/Infrastructure/FileSystemStoreMultiColumnTests.cs` -- covers SER-01 multi-column round-trip
- [ ] `tests/Dynamicweb.ContentSync.Tests/Configuration/ConfigLoaderValidationTests.cs` -- covers CFG-01 OutputDirectory validation (or extend existing ConfigLoaderTests.cs)
- [ ] `tests/Dynamicweb.ContentSync.Tests/Fixtures/ContentTreeBuilder.cs` -- needs `BuildMultiColumnTree()` fixture method
- [ ] DES-04 PropertyFields dry-run test requires DW `Page` object with `PropertyItem` -- may be integration-test only or require mock. Flag as manual-only if mocking is infeasible.

## Open Questions

1. **Paragraph filename collision handling**
   - What we know: SortOrder is unique per column, not per grid row. Two paragraphs in different columns can share a SortOrder.
   - What's unclear: Does this actually occur in real DW content? The current write loop would silently overwrite `paragraph-1.yml`.
   - Recommendation: Assume it occurs; include column ID in filename to be safe. Use `paragraph-c{ColumnId}-{SortOrder}.yml` pattern.

2. **PropertyFields dry-run testing without DW runtime**
   - What we know: `LogDryRunPageUpdate` takes a live DW `Page` object. Unit tests cannot construct one without the DW runtime.
   - What's unclear: Whether the existing integration test infrastructure can cover this.
   - Recommendation: The PropertyFields diff logic is simple string comparison. If unit-testable (by extracting the diff logic into a testable helper), prefer that. Otherwise, rely on integration test coverage.

## Sources

### Primary (HIGH confidence)
- Direct code analysis of all source files in the project
- v1.0-MILESTONE-AUDIT.md — gap definitions
- STATE.md — project decisions history
- Existing test suite — FileSystemStoreTests.cs, ConfigLoaderTests.cs, ContentTreeBuilder.cs

### Secondary (MEDIUM confidence)
- DW API behavior for `Page.PropertyItem`, `Item.SerializeTo()` — based on existing working code patterns in the project (not independently verified against DW docs)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - no new libraries needed, all changes are to existing code
- Architecture: HIGH - all four gaps have clear root causes and straightforward fixes identified from code analysis
- Pitfalls: HIGH - paragraph filename collision is the only non-obvious risk, and the mitigation is simple

**Research date:** 2026-03-20
**Valid until:** 2026-04-20 (stable — internal project, no external dependency changes)

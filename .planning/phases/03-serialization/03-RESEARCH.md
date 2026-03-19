# Phase 3: Serialization - Research

**Researched:** 2026-03-19
**Domain:** DynamicWeb Content API surface, DW-to-DTO mapper, long-path handling, integration testing against live DW instance
**Confidence:** HIGH (DW API verified via official docs; service instantiation verified via official forum code; test instance structure verified by filesystem inspection)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Entry point: Area → Pages → recursive walk of children. Load area by predicate's `areaId`, find root page matching predicate path, recursively walk child pages. For each page: load grid rows via GridService, paragraphs via ParagraphService.
- Apply ContentPredicateSet filtering at each page node to check include/exclude rules.
- GUID-map known reference fields at serialize time. For known references (ParentPageId, page links, etc.), replace numeric ID with the target item's GUID. Requires a lookup per reference to resolve numeric ID → GUID.
- Unknown/undocumented reference fields: discover empirically against the test instance (known blocker from research).
- Swift2.2 (.NET 10) is the SOURCE — serialize from this instance. Swift2.1 (.NET 8) is the TARGET — deserialize into this instance (Phase 4). Both instances at `C:\Projects\Solutions\swift.test.forsync`.
- Live instance integration tests — connect to running Swift2.2, serialize Customer Center. Verify both structure (folder tree, file count) AND content (spot-check field values in YAML). No mocks for DW services — test against the real API for confidence.
- DLL copy approach — build ContentSync, copy DLL + dependencies to swift instance's bin folder. Quick and dirty, appropriate for development/testing phase.
- Target net8 only — net8 assemblies load fine in net10, keeps it simple.
- Source-wins conflict strategy is established by the serializer: files on disk ARE the source of truth. No merge logic — files always overwrite target DB on deserialize.
- YAML fidelity already proven in Phase 1 with ForceStringScalarEmitter. Phase 3 validates against real DW content (HTML fields, multiline descriptions, etc.). Any new edge cases from live content must be handled without breaking existing fidelity tests.
- Serializer must handle paths exceeding Windows MAX_PATH (260 chars). Warn and skip items that would overflow, do not crash.

### Claude's Discretion
- Mapping strategy: curated core properties or reflection-based, whatever produces best fidelity.
- Exact DW API method signatures and service resolution.
- How to resolve DW services (dependency injection, static access, or direct construction).
- Which properties count as "known reference fields" — discover during research/implementation.
- Integration test project structure and test runner configuration.

### Deferred Ideas (OUT OF SCOPE)
- None — discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SER-03 | Source-wins conflict resolution — serialized files always overwrite target DB on deserialize | Established by serializer producing authoritative YAML files. The mapper captures all DW state; FileSystemStore.WriteTree is the write contract. No merge logic needed in Phase 3 — the files produced here are what Phase 4 will unconditionally apply. |
| INF-02 | YAML round-trip fidelity — handle tildes, CRLFs, HTML content without corruption | ForceStringScalarEmitter already proven in Phase 1. Phase 3 must run real DW content through the existing serializer and verify no new edge cases appear. Integration test spot-checks field values from live YAML output. |
| INF-03 | Windows long-path handling for deep content hierarchies | FileSystemStore.SafeGetDirectory already truncates directory paths >247 chars with GUID suffix and Console.Error warning. WriteYamlFile already warns on full file paths >259 chars. Phase 3 validates both paths fire correctly on real content. |
</phase_requirements>

---

## Summary

Phase 3 builds the DW-to-DTO mapper — the component that reads live DynamicWeb content objects via DW APIs and maps them to the existing SerializedArea/SerializedPage/SerializedGridRow/SerializedParagraph DTOs. FileSystemStore and the YAML layer are already complete. The integration test must run against Swift2.2 (Customer Center, pageid=8385), deploy the DLL to the instance's bin folder, and verify the output YAML tree.

The DW content API surface is clear from official docs. Services are instantiated directly with `new PageService()`, `new GridService()`, etc. (confirmed via official DW forum code examples — no dependency injection framework is involved for content services). The static `Services.Pages`, `Services.Areas`, `Services.Paragraphs`, `Services.Grids`, `Services.Items` pattern is the canonical accessor in DW10. Either pattern works; `new XxxService()` is simpler for the mapper since no DI container is configured in ContentSync.

ItemType custom fields are accessed via `page.Item` (a `Dynamicweb.Content.Items.Item` object that implements `IDictionary<string, object?>`). Iterating `item.Names` yields all field keys; `item[name]` yields values. This maps naturally to the `Dictionary<string, object> Fields` on the existing DTOs.

**Primary recommendation:** Build a `ContentMapper` class that takes a DW `Page`/`Paragraph`/`GridRow`/`Area` and returns the corresponding DTO. Wire it into a `ContentSerializer` that uses PageService/GridService/ParagraphService to traverse the tree, filters via ContentPredicateSet, and passes the DTO tree to `FileSystemStore.WriteTree`. Deploy via DLL copy to Swift2.2 bin. Integration test connects to running Swift2.2, calls the serializer, and asserts on YAML output.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Dynamicweb.Core` | 10.23.9 | DW content services (PageService, GridService, ParagraphService, AreaService, ItemService) | Already referenced in Swift instances. The root package for all DW content APIs. |
| YamlDotNet | 16.3.0 | YAML output | Already in use; FileSystemStore and YamlConfiguration are complete. |
| xunit | 2.9.3 | Integration tests | Already the test framework in Tests.csproj. |

### DW Content Services Used in Phase 3

| Service | Namespace | Instantiation | Key Methods Used |
|---------|-----------|--------------|-----------------|
| `AreaService` | `Dynamicweb.Content` | `new AreaService()` or `Services.Areas` | `GetArea(int areaId)` → `Area?` |
| `PageService` | `Dynamicweb.Content` | `new PageService()` or `Services.Pages` | `GetPagesByParentID(int parentId)` → `IEnumerable<Page>`, `GetPage(int pageId)` → `Page?` |
| `GridService` | `Dynamicweb.Content` | `new GridService()` or `Services.Grids` | `GetGridRowsByPageId(int pageId)` → `IEnumerable<GridRow>` |
| `ParagraphService` | `Dynamicweb.Content` | `new ParagraphService()` or `Services.Paragraphs` | `GetParagraphsByPageId(int pageId)` → `IEnumerable<Paragraph>` |
| `ItemService` | `Dynamicweb.Content` | `new ItemService()` or `Services.Items` | `GetItem(string itemType, string itemId)` → `Item?` — only needed if `page.Item` is null |
| `Services` (static) | `Dynamicweb.Content` | Static class | Accessor for all the above; alternative to `new XxxService()` |

**Installation:** Dynamicweb.Core is a dependency of Dynamicweb.Suite (what both test instances reference). For ContentSync project to compile, reference the NuGet package:

```xml
<PackageReference Include="Dynamicweb.Core" Version="10.23.9" />
```

This is already missing from the current `Dynamicweb.ContentSync.csproj` — the project file currently only has `YamlDotNet` and `Microsoft.Extensions.Configuration.Json`. Adding `Dynamicweb.Core` is required before writing any DW API code.

---

## Architecture Patterns

### Recommended Project Structure (Phase 3 additions)

```
src/Dynamicweb.ContentSync/
├── Models/                          (existing — no changes)
│   ├── SerializedArea.cs
│   ├── SerializedPage.cs
│   ├── SerializedGridRow.cs
│   ├── SerializedGridColumn.cs
│   └── SerializedParagraph.cs
├── Configuration/                   (existing — no changes)
│   ├── ContentPredicate.cs          (ContentPredicate + ContentPredicateSet)
│   ├── ConfigLoader.cs
│   ├── SyncConfiguration.cs
│   └── PredicateDefinition.cs
├── Infrastructure/                  (existing — no changes)
│   ├── FileSystemStore.cs
│   ├── YamlConfiguration.cs
│   ├── ForceStringScalarEmitter.cs
│   └── IContentStore.cs
└── Serialization/                   (NEW — this phase)
    ├── ContentMapper.cs             # DW object → DTO mapping (pure functions)
    ├── ContentSerializer.cs         # Orchestrates traversal + mapping + FileSystemStore
    └── ReferenceResolver.cs         # Numeric ID → GUID lookups for reference fields

tests/Dynamicweb.ContentSync.IntegrationTests/  (NEW — separate project)
    ├── Dynamicweb.ContentSync.IntegrationTests.csproj
    └── Serialization/
        └── CustomerCenterSerializationTests.cs
```

**Rationale for separate integration test project:**
- Integration tests require a running DW instance and DLL copy deployment — they cannot run in CI without a live instance.
- Unit tests (existing) have no DW dependency and should remain fast. Mixing the two in one project would couple the fast test suite to the live instance requirement.
- The integration test project references both ContentSync and Dynamicweb.Core directly.

### Pattern 1: ContentMapper — Pure DW Object → DTO Mapping

**What:** A static or instance class with `Map(Area)`, `Map(Page)`, `Map(GridRow)`, `Map(Paragraph)` methods. No traversal, no I/O — pure conversion.

**When to use:** ContentSerializer calls these methods after fetching objects from DW services.

**Page property mapping (verified from official docs):**

| DW `Page` Property | DW Type | DTO Field | Notes |
|-------------------|---------|-----------|-------|
| `UniqueId` | `Guid` | `PageUniqueId` | The GUID identity — confirmed property name |
| `ID` | `int` | not serialized | Numeric ID is environment-specific — never serialize as identity |
| `ParentPageId` | `int` | needs GUID lookup | Reference field — resolve via PageService.GetPage(parentId).UniqueId |
| `AreaId` | `int` | not in DTO | Area captured at area level |
| `MenuText` | `string` | `MenuText` | Used in navigation |
| `UrlName` | `string` | `UrlName` | URL slug |
| `Sort` | `int` | `SortOrder` | DW property is `Sort`, DTO is `SortOrder` |
| `Active` | `bool` | `IsActive` | DW property is `Active`, DTO is `IsActive` |
| `ItemType` | `string` | used to load Item | Not in DTO directly — used to fetch custom fields |
| `ItemId` | `string` | used to load Item | Together with ItemType, keys the custom fields |
| `Item` | `Item?` | `Fields` dict | Lazy-loaded; enumerate `item.Names` for field keys |
| `Hidden` | `bool` | optional field | Could add to DTO if needed for page visibility |

**Note:** `Name` is NOT a documented property on `Page` in DW10 docs — the navigation title is `MenuText`. There may also be a `Title` or `Heading` property. Verify against live instance. The existing `SerializedPage.Name` must map to whichever DW property carries the page's display name.

**Paragraph property mapping (verified from official docs):**

| DW `Paragraph` Property | DW Type | DTO Field | Notes |
|------------------------|---------|-----------|-------|
| `UniqueId` | `Guid` | `ParagraphUniqueId` | The GUID identity |
| `ID` | `int` | not serialized | Numeric ID only |
| `PageID` | `int` | not serialized | Resolved via tree walk |
| `GridRowId` | `int` | not serialized | Resolved via tree walk |
| `GridRowColumn` | `int` | used for column grouping | Which column the paragraph lives in |
| `Sort` | `int` | `SortOrder` | DW property is `Sort` |
| `ItemType` | `string` | `ItemType` | Serialized directly |
| `ItemId` | `string` | used to load Item | Custom fields |
| `Item` | `Item?` | `Fields` dict | Enumerate `item.Names` |
| `Header` | `string` | `Header` | Direct mapping |
| `Text` | `string` | may be in Fields | Body text; include as `Fields["text"]` if present |
| `ShowParagraph` | `bool` | optional | Paragraph visibility |
| `MasterParagraphID` | `int` | reference field | Numeric — needs GUID lookup if non-zero |
| `GlobalRecordPageID` | `int` | reference field | Numeric cross-reference |

**GridRow property mapping:**

| DW `GridRow` Property | DW Type | DTO Field | Notes |
|----------------------|---------|-----------|-------|
| `UniqueId` | `Guid` | `Id` (SerializedGridRow) | The GUID identity |
| `Sort` | `int` | `SortOrder` | DW property is `Sort` |
| `Container` | `string` | optional | Grid container identifier |
| `DefinitionId` | `string` | optional | Grid layout definition |

**GridRow columns:** The official docs do not expose a `Columns` property on `GridRow`. Columns are implicit — paragraphs are assigned to columns via `Paragraph.GridRowColumn` (an int column index). To reconstruct columns: group paragraphs by `GridRowColumn` value after fetching via ParagraphService.

**Area property mapping:**

| DW `Area` Property | DW Type | DTO Field | Notes |
|-------------------|---------|-----------|-------|
| `UniqueId` | `Guid` | `AreaId` | The GUID identity |
| `Sort` | `int` | `SortOrder` | |
| `Name` | `string` | `Name` | |

### Pattern 2: ContentSerializer — Traversal Orchestrator

**What:** Accepts a `SyncConfiguration`, iterates predicates, fetches the area, walks pages recursively, applies ContentPredicateSet, maps each object, assembles the DTO tree, calls FileSystemStore.WriteTree.

**Traversal algorithm:**

```csharp
// Source: DW PageService API (GetPagesByParentID is the recursive hook)
SerializedArea Serialize(PredicateDefinition predicate)
{
    var area = new AreaService().GetArea(predicate.AreaId);  // or Services.Areas.GetArea(...)
    var rootPages = GetRootPagesForPredicate(predicate, area);
    var serializedPages = rootPages
        .OrderBy(p => p.Sort)
        .Select(p => SerializePage(p, predicate, "/" + p.MenuText))
        .Where(p => p != null)
        .ToList();
    return ContentMapper.Map(area, serializedPages);
}

SerializedPage SerializePage(Page page, PredicateDefinition predicate, string contentPath)
{
    // Check predicate inclusion BEFORE loading children
    if (!predicateSet.ShouldInclude(contentPath, predicate.AreaId))
        return null;

    var gridRows = SerializeGridRows(page);
    var dto = ContentMapper.Map(page, gridRows);

    // Recursively process children
    var children = new PageService().GetPagesByParentID(page.ID)
        .OrderBy(child => child.Sort)
        .Select(child => SerializePage(child, predicate, contentPath + "/" + child.MenuText))
        .Where(child => child != null)
        .ToList();

    // Note: SerializedPage has no Children collection yet — this may need adding to support multi-level trees
    return dto;
}
```

**IMPORTANT gap:** `SerializedPage` has no `Children` property. The existing `FileSystemStore.WriteTree` only handles flat pages in an area (one level deep). For the Customer Center tree (pageid=8385 with child pages), the DTO model and FileSystemStore must be extended to support recursive page children. This is a core schema gap that must be addressed in Phase 3.

### Pattern 3: Service Resolution (confirmed)

DW services are instantiated directly with `new`:

```csharp
// Confirmed from official DW forum code examples
var areaService = new AreaService();     // or Services.Areas
var pageService = new PageService();     // or Services.Pages
var gridService = new GridService();     // or Services.Grids
var paragraphService = new ParagraphService();  // or Services.Paragraphs

// Both work. new XxxService() is simpler when no DI container is configured.
// Services.Xxx is the static accessor pattern — both route to the same implementation.
```

No dependency injection framework is needed. DW services are self-contained.

### Pattern 4: Item Field Extraction

```csharp
// Confirmed from DW10 API docs: Item implements IDictionary<string, object?>
// Item.Names = ICollection<string> — all field names
// Item[name] = object? — field value

var fields = new Dictionary<string, object>();

if (page.Item != null)
{
    foreach (var fieldName in page.Item.Names)
    {
        var value = page.Item[fieldName];
        if (value != null)
            fields[fieldName] = value;
    }
}
```

### Pattern 5: Column Reconstruction from Paragraph.GridRowColumn

GridRow does not have a Columns property. Columns are derived from paragraph grouping:

```csharp
var paragraphs = new ParagraphService().GetParagraphsByPageId(pageId)
    .OrderBy(p => p.Sort);

// Group by GridRowColumn to reconstruct columns
var columnGroups = paragraphs
    .GroupBy(p => p.GridRowColumn)
    .OrderBy(g => g.Key);

var columns = columnGroups.Select(g => new SerializedGridColumn
{
    Id = g.Key,
    Width = 0,  // Column width not available from Paragraph; GridRow definition has this
    Paragraphs = g.Select(p => ContentMapper.Map(p)).ToList()
}).ToList();
```

### Anti-Patterns to Avoid

- **Serializing `page.ID` (int) as canonical identity:** Use `page.UniqueId` (Guid). The int ID is environment-specific.
- **Fetching all pages at once with `GetPagesByAreaID`:** This returns a flat `PageCollection`. For recursive tree building, use `GetPagesByParentID(parentId)` which returns direct children only.
- **Calling `GetPage()` to find the root by pageid=8385:** For integration tests, `GetPage(8385)` is fine as a direct lookup. For general predicate-based traversal, find root by matching the predicate path against page names.
- **Accessing `page.Item` without null check:** ItemType is optional on pages — `page.Item` can be null when no ItemType is assigned.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YAML serialization with special char safety | Custom YAML writer | `YamlConfiguration.BuildSerializer()` (existing) | ForceStringScalarEmitter already handles tildes, CRLFs, HTML — proven in Phase 1 |
| File tree layout with path dedup | Custom path manager | `FileSystemStore.WriteTree()` (existing) | Already handles sanitization, dedup with GUID suffix, long-path truncation with warning |
| Predicate filtering | Custom include/exclude logic | `ContentPredicateSet.ShouldInclude()` (existing) | Already handles OR-logic across multiple predicates with OrdinalIgnoreCase and path boundary |
| Config loading | Custom JSON parser | `ConfigLoader.Load()` (existing) | Raw nullable model + validation with clear error messages |

---

## Common Pitfalls

### Pitfall 1: SerializedPage Has No Children Collection

**What goes wrong:** The existing `SerializedPage` DTO only holds `GridRows`. The existing `FileSystemStore.WriteTree` only iterates `area.Pages` (flat list). A multi-level content tree like Customer Center (pageid=8385 with child pages) cannot be represented or written.

**Why it happens:** Phase 1 built the foundation with a simplified model assuming one level of pages under an area. Real DW content trees are recursive.

**How to avoid:** Before writing the mapper, extend `SerializedPage` with `List<SerializedPage> Children { get; init; } = new()` and extend `FileSystemStore.WriteTree` to recursively write children into subfolders of the page folder.

**Warning signs:** Output YAML only contains the top-level Customer Center page with no sub-pages.

### Pitfall 2: Page.Name vs Page.MenuText

**What goes wrong:** The existing `SerializedPage` has a `Name` property. The DW `Page` object's documented navigation title property is `MenuText`. If `Name` is set from `MenuText`, the YAML is consistent but folder naming may differ from what users expect. If DW also has a `Name` or `Title` property distinct from `MenuText`, the wrong field may be used.

**Why it happens:** The DW API docs describe `MenuText` as "The name of the page and is used in the navigation." Whether there is a separate `Name` property must be verified at runtime.

**How to avoid:** In the integration test, print out all available property names on the first fetched page object. Verify which property carries the page's display name.

**Warning signs:** Folder names in YAML output don't match what's shown in DW admin.

### Pitfall 3: ParagraphService Returns Only Active Paragraphs

**What goes wrong:** `GetParagraphsByPageId(pageId)` may only return active/visible paragraphs. Inactive paragraphs (draft, disabled) are silently omitted from the serialized output.

**Why it happens:** Per earlier research (MEDIUM confidence from forum docs), the method filters by active state. The DW10 official docs show a `ParagraphSort` overload but no explicit `includeInactive` parameter on this method.

**How to avoid:** After serializing Customer Center, compare the paragraph count in YAML output against the paragraph count visible in DW admin (including inactive paragraphs). If counts differ, investigate whether `GetParagraphs()` (all paragraphs) filtered by `PageID` is needed. `GetDeletedParagraphs(areaId)` only gets permanently deleted — not draft/inactive.

**Warning signs:** Fewer paragraphs in YAML than shown in DW admin edit view.

### Pitfall 4: Numeric Cross-Reference Fields in Paragraph Properties

**What goes wrong:** Paragraph properties like `MasterParagraphID`, `GlobalRecordPageID`, `GlobalRecordParagraphID`, `CopyOf` store numeric IDs referencing other items. If serialized as integers, they become invalid in any target environment.

**Why it happens:** These are known reference fields from official docs. Their values are environment-specific numeric IDs.

**How to avoid:** In the mapper, for each known reference field:
- If value is 0 or negative: omit (no reference).
- If value is a valid numeric ID: look up the referenced item's GUID and store the GUID string instead.

**Known reference fields (from docs research):**
- `Paragraph.MasterParagraphID` — references another paragraph
- `Paragraph.GlobalRecordPageID` — references a page
- `Paragraph.GlobalRecordParagraphID` — references a paragraph
- `Paragraph.CopyOf` — references a paragraph (the copy source)
- `Page.ParentPageId` — references parent page (but this is implicit in the tree structure, so may not need GUID lookup — the parent GUID is known from tree traversal)

**Warning signs:** Integer values in serialized YAML that match DW content IDs.

### Pitfall 5: Long-Path Warning is Console.Error, Not Structured Log

**What goes wrong:** The existing `FileSystemStore.SafeGetDirectory` and `WriteYamlFile` write warnings to `Console.Error`. In a DW scheduled task context, `Console.Error` output is not routed to DW logs.

**Why it happens:** Console.Error was appropriate for standalone CLI testing but is not the right output for production DW addin code.

**How to avoid:** For Phase 3, this is acceptable — the serializer runs as a direct call from integration tests, not from a scheduled task. In Phase 5 (scheduled tasks), wire the warnings to DW's `LogManager`. Document this as a known gap for Phase 5.

### Pitfall 6: Dynamicweb.Core Not in ContentSync.csproj

**What goes wrong:** The current `Dynamicweb.ContentSync.csproj` does not reference `Dynamicweb.Core`. Building the mapper class that imports `Dynamicweb.Content` will fail to compile without this reference.

**Why it happens:** Phases 1 and 2 built DW-API-free infrastructure (DTOs, FileSystemStore, ConfigLoader). No DW APIs were needed.

**How to avoid:** First task of Phase 3: add `<PackageReference Include="Dynamicweb.Core" Version="10.23.9" />` to the main project.

---

## Code Examples

Verified patterns from official sources and API docs:

### Service Resolution (confirmed — DW forum code examples)

```csharp
using Dynamicweb.Content;

// Pattern A: static factory (preferred for DW10 — no instantiation)
var area = Services.Areas.GetArea(areaId);
var pages = Services.Pages.GetPagesByParentID(parentPageId);
var gridRows = Services.Grids.GetGridRowsByPageId(pageId);
var paragraphs = Services.Paragraphs.GetParagraphsByPageId(pageId);

// Pattern B: direct instantiation (confirmed from DW forum)
var area = new AreaService().GetArea(areaId);
var pages = new PageService().GetPagesByParentID(parentPageId);
```

### Item Field Extraction (confirmed — DW10 Item API docs)

```csharp
using Dynamicweb.Content.Items;

// Item implements IDictionary<string, object?>
// item.Names = ICollection<string> — all field keys
// item[fieldName] = object? — field value

var fields = new Dictionary<string, object>();
if (page.Item != null)
{
    foreach (var fieldName in page.Item.Names)
    {
        var value = page.Item[fieldName];
        if (value != null)
            fields[fieldName] = value;
    }
}
```

### Page Traversal (confirmed — DW PageService API docs)

```csharp
// GetPagesByParentID: returns direct children only — correct for recursive walk
// GetPagesByAreaID: returns all pages flat — NOT for tree building
IEnumerable<Page> children = Services.Pages.GetPagesByParentID(parentPage.ID);

// Get root pages for an area (top level, no parent)
IEnumerable<Page> roots = Services.Pages.GetRootPagesForArea(areaId);
```

### GridRow → Column → Paragraph Hierarchy

```csharp
// GridRow has no Columns property — columns are derived from Paragraph.GridRowColumn
IEnumerable<GridRow> rows = Services.Grids.GetGridRowsByPageId(pageId);
IEnumerable<Paragraph> paragraphs = Services.Paragraphs.GetParagraphsByPageId(pageId);

// Group paragraphs by (GridRowId, GridRowColumn) for column reconstruction
var paragraphsByRow = paragraphs
    .GroupBy(p => p.GridRowId)
    .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Sort).GroupBy(p => p.GridRowColumn).ToList());
```

### DLL Deployment to Swift2.2

```bash
# Build ContentSync targeting net8.0
dotnet build src/Dynamicweb.ContentSync/Dynamicweb.ContentSync.csproj -c Debug

# Copy to Swift2.2 bin (net10 loads net8 assemblies)
# Source: build output
# Target: C:\Projects\Solutions\swift.test.forsync\Swift2.2\Dynamicweb.Host.Suite\bin\Debug\net10.0\
cp bin/Debug/net8.0/Dynamicweb.ContentSync.dll [target-bin]/
cp bin/Debug/net8.0/YamlDotNet.dll [target-bin]/
# Restart Swift2.2 after DLL copy (dotnet process must reload)
```

### Integration Test Structure

```csharp
// Integration test — requires Swift2.2 running and DLL deployed to its bin
// Marked with [Trait("Category", "Integration")] to exclude from unit test runs

[Trait("Category", "Integration")]
public class CustomerCenterSerializationTests
{
    [Fact]
    public void Serialize_CustomerCenter_ProducesYamlTree()
    {
        // Arrange: point to running Swift2.2 instance
        var outputDir = Path.Combine(Path.GetTempPath(), "ContentSyncTest_" + Guid.NewGuid().ToString("N")[..8]);
        var config = new SyncConfiguration
        {
            OutputDirectory = outputDir,
            Predicates = new List<PredicateDefinition>
            {
                new PredicateDefinition
                {
                    Name = "CustomerCenter",
                    AreaId = [verify against live instance],
                    Path = "/Customer Center",
                    Excludes = new List<string>()
                }
            }
        };

        // Act
        var serializer = new ContentSerializer(config);
        serializer.Serialize();

        // Assert: structure
        Assert.True(Directory.Exists(outputDir));
        var yamlFiles = Directory.EnumerateFiles(outputDir, "*.yml", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(yamlFiles);

        // Assert: no numeric IDs in YAML that look like page references
        foreach (var file in yamlFiles)
        {
            var content = File.ReadAllText(file);
            // Spot-check: no raw pageid=8385 in output
            Assert.DoesNotContain("8385", content);
        }
    }
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Dynamicweb.Content.Services.Paragraphs` (static class) | `ParagraphService` in `Dynamicweb.Content` namespace via `Services.Paragraphs` | DW10 | The `Paragraphs` static class referenced in STACK.md was a DW9 pattern. In DW10, `ParagraphService` is the class; `Services.Paragraphs` is the accessor property. |
| `new XxxService()` | `Services.Xxx` static accessor | DW10 | Both still work in DW10 (confirmed via forum + API docs). `Services.Xxx` is the documented canonical way. |
| GridRow.Columns collection | `Paragraph.GridRowColumn` grouping | DW10 | No `Columns` collection exists on GridRow. Columns are implicit — derived by grouping paragraphs by their `GridRowColumn` int property. |

**Deprecated/outdated:**
- `Paragraphs.GetParagraphsByPage()` (DW9 static `Paragraphs` class): Not the DW10 API. Use `Services.Paragraphs.GetParagraphsByPageId(int)` or `new ParagraphService().GetParagraphsByPageId(int)`.

---

## Test Environment: Swift Instances

### Structure (verified by filesystem inspection)

```
C:\Projects\Solutions\swift.test.forsync\
├── Swift2.2\                         # SOURCE — serialize FROM here
│   └── Dynamicweb.Host.Suite\
│       ├── Dynamicweb.Host.Suite.csproj   (TargetFramework: net10.0)
│       ├── Program.cs                      (builder.Services.AddDynamicweb(...))
│       └── bin\Debug\net10.0\             # DLL COPY DESTINATION
│           ├── Dynamicweb.dll
│           ├── Dynamicweb.Core.dll
│           └── [other DW DLLs]
│
└── Swift2.1\                         # TARGET — deserialize INTO here (Phase 4)
    └── Dynamicweb.Host.Suite\
        ├── Dynamicweb.Host.Suite.csproj   (TargetFramework: net8.0)
        └── bin\Debug\net8.0\             # Phase 4 DLL copy destination
            ├── Dynamicweb.dll
            └── [other DW DLLs]
```

### Deployment Steps (DLL copy approach)

1. Build ContentSync: `dotnet build -c Debug` targeting `net8.0`
2. Copy `Dynamicweb.ContentSync.dll` and `YamlDotNet.dll` to Swift2.2's `bin/Debug/net10.0/`
3. net10 loads net8 assemblies (confirmed — DW instances are ASP.NET Core apps that probe for addins in the bin folder)
4. Restart Swift2.2 if running (the DLL is loaded by the web host at startup or via plugin discovery)

**Swift2.2 is dotnet run, not IIS** — per CONTEXT.md "Start with `dotnet run` in the respective folders." This means:
- DLL copy does NOT require an IIS reset
- Must stop and restart the `dotnet run` process after DLL copy for the new assembly to load
- The integration test may run in-process OR invoke the running instance via HTTP — decide during implementation

### Finding the Area ID for Customer Center

The predicate needs `areaId` (an int). The test specifies `pageid=8385`. To find the `areaId` for the predicate:
```csharp
// At integration test setup — fetch the page and read its AreaId
var page = Services.Pages.GetPage(8385);
int areaId = page.AreaId;  // Use this in the predicate definition
```

---

## Open Questions

1. **Page.Name vs Page.MenuText**
   - What we know: DW10 docs document `MenuText` as the navigation label. `Name` is not listed in the official Page docs.
   - What's unclear: Whether `Page` has a separate `Name` property (perhaps for the page heading/title distinct from the nav label) or if `MenuText` is the only name-like property.
   - Recommendation: At integration test time, reflectively inspect the first fetched `Page` object's public properties (or check the DW source/API docs more thoroughly). Map `SerializedPage.Name` to whichever DW property is the display name.

2. **Whether `GetParagraphsByPageId` returns inactive paragraphs**
   - What we know: Earlier research flagged this as MEDIUM confidence (forum-sourced). Official DW10 docs show no `includeInactive` parameter on `GetParagraphsByPageId`.
   - What's unclear: Whether inactive/draft paragraphs are included.
   - Recommendation: After first serialization run, manually count paragraphs in DW admin for Customer Center and compare to YAML output count. If counts differ, use `GetParagraphs()` filtered by `PageID` as a fallback.

3. **Recursive page structure in DTO + FileSystemStore**
   - What we know: `SerializedPage` has no `Children` property. `FileSystemStore.WriteTree` iterates `area.Pages` flat.
   - What's unclear: The scope of this gap — does the Customer Center tree have sub-pages, or is it a flat list under the area?
   - Recommendation: This is a known schema gap. Phase 3 must add `List<SerializedPage> Children` to `SerializedPage` and update `FileSystemStore.WriteTree` to recurse into children, writing them as subfolders within the page folder.

4. **Integration test runner — in-process vs deployed**
   - What we know: ContentSync DLL must be loaded in the context of a running DW instance for services like `Services.Pages` to work (they require DW's internal initialization).
   - What's unclear: Can the integration test invoke DW services directly from the test process if `Dynamicweb.Core` is referenced, or must the serializer logic run as a DW scheduled task addin?
   - Recommendation: Try running the integration test in-process with `Dynamicweb.Core` referenced and `new PageService()`. If DW requires full host initialization (web host, SQL connection, etc.), the test must instead trigger the serializer via the DW scheduled task API or a simple console app deployed alongside the instance. This is the biggest implementation-time uncertainty for this phase.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.9.3 |
| Config file | none — standard dotnet test discovery |
| Quick run command | `dotnet test tests/Dynamicweb.ContentSync.Tests/ -x` |
| Full suite command | `dotnet test -x` |
| Integration test command | `dotnet test tests/Dynamicweb.ContentSync.IntegrationTests/ --filter "Category=Integration" -x` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SER-03 | Serialized YAML files are source of truth (files contain GUIDs only, no numeric cross-refs) | Integration | `dotnet test ...IntegrationTests --filter Method=Serialize_CustomerCenter_GuidOnly` | ❌ Wave 0 |
| INF-02 | YAML fidelity for HTML, CRLF, tilde from live DW content | Integration | `dotnet test ...IntegrationTests --filter Method=Serialize_CustomerCenter_FieldFidelity` | ❌ Wave 0 |
| INF-03 | Long-path: warn and skip, not crash | Unit | `dotnet test ...Tests --filter Method=WriteTree_LongPath_WarnsAndSkips` | ❌ Wave 0 |
| INF-03 | Long-path: confirmed existing SafeGetDirectory truncation test | Unit | `dotnet test tests/Dynamicweb.ContentSync.Tests/` | Partial — SafeGetDirectory exists, no explicit long-path test |

### Sampling Rate

- **Per task commit:** `dotnet test tests/Dynamicweb.ContentSync.Tests/ -x` (unit tests only, ~5s)
- **Per wave merge:** `dotnet test tests/Dynamicweb.ContentSync.Tests/ -x` (full unit suite)
- **Phase gate:** Integration tests pass against Swift2.2 before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/Dynamicweb.ContentSync.IntegrationTests/Dynamicweb.ContentSync.IntegrationTests.csproj` — integration test project; references ContentSync + Dynamicweb.Core
- [ ] `tests/Dynamicweb.ContentSync.IntegrationTests/Serialization/CustomerCenterSerializationTests.cs` — covers SER-03 (GUID-only output) and INF-02 (field fidelity on live content)
- [ ] `tests/Dynamicweb.ContentSync.Tests/Infrastructure/FileSystemStoreTests.cs` extension — add explicit long-path unit test for INF-03 (currently SafeGetDirectory exists but no dedicated test asserts on warning + skip behavior)

---

## Sources

### Primary (HIGH confidence)
- [DW API: AreaService](https://doc.dynamicweb.dev/api/Dynamicweb.Content.AreaService.html) — GetArea, GetAreas method signatures
- [DW API: PageService](https://doc.dynamicweb.dev/api/Dynamicweb.Content.PageService.html) — GetPage, GetPagesByParentID, GetRootPagesForArea signatures
- [DW API: GridService](https://doc.dynamicweb.dev/api/Dynamicweb.Content.GridService.html) — GetGridRowsByPageId signatures
- [DW API: ParagraphService](https://doc.dynamicweb.dev/api/Dynamicweb.Content.ParagraphService.html) — GetParagraphsByPageId, SaveParagraph signatures
- [DW API: Page class](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Page.html) — UniqueId, MenuText, UrlName, Sort, Active, ParentPageId, AreaId, ItemType, ItemId, Item properties
- [DW API: Paragraph class](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Paragraph.html) — UniqueId, PageID, GridRowId, GridRowColumn, Sort, ItemType, ItemId, Item, Header, Text, MasterParagraphID, GlobalRecordPageID
- [DW API: GridRow class](https://doc.dynamicweb.dev/api/Dynamicweb.Content.GridRow.html) — UniqueId, Sort, PageId, DefinitionId (no Columns property confirmed)
- [DW API: Area class](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Area.html) — UniqueId, Name, Sort
- [DW API: Item class](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Items.Item.html) — Names, indexer, IDictionary<string, object?> implementation
- [DW API: Services class](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Services.html) — static accessor pattern, all service properties
- [DW Forum: Creating pages and paragraphs with the API](https://doc.dynamicweb.com/forum/development/creating-pages-and-paragraph-with-the-api?PID=1605) — service instantiation with `new XxxService()` confirmed

### Secondary (MEDIUM confidence)
- [DW10 Item class research via WebSearch](https://doc.dynamicweb.dev/api/Dynamicweb.Content.Items.Item.html) — Names property, IDictionary implementation for field iteration
- Filesystem inspection of `C:\Projects\Solutions\swift.test.forsync` — both instances confirmed at net10.0 (Swift2.2) and net8.0 (Swift2.1); bin folder DLL layout verified

### Tertiary (LOW confidence)
- Forum-documented behavior that `GetParagraphsByPageId` returns only active paragraphs — not verified from official DW10 docs, must confirm empirically

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — service APIs verified from official DW10 docs
- Architecture: HIGH — DW service APIs confirmed; column/row structure confirmed via GridRow and Paragraph property docs
- Pitfalls: HIGH — reference field names confirmed from official Paragraph class docs; schema gap (Children) identified from existing codebase
- Integration test deployment: MEDIUM — net8 loading in net10 expected but not explicitly tested; DW host initialization requirements for `new PageService()` in-process are unverified

**Research date:** 2026-03-19
**Valid until:** 2026-04-19 (DW API is stable; service signatures unlikely to change in 30 days)

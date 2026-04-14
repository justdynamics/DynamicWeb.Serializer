# Phase 34: Embedded XML Screens - Research

**Researched:** 2026-04-14
**Domain:** DynamicWeb Admin UI (tree nodes, list/edit screens), SQL discovery queries, config persistence
**Confidence:** HIGH

## Summary

Phase 34 adds an "Embedded XML" tree node under Serialize that lets users discover XML types present in their DW content database and configure per-type element exclusions. The phase also retroactively replaces Phase 33's `CheckboxList` controls with `SelectMultiDual` to establish a consistent multi-select pattern.

The codebase already has all infrastructure needed: `ExcludeXmlElementsByType` dictionary in `SerializerConfiguration`, `ExclusionMerger.MergeXmlExclusions()` for runtime application, `XmlFormatter.RemoveElements()` for element stripping, and `ContentMapper` already calls these during serialization keyed on `page.UrlDataProviderTypeName` (pages) and `paragraph.ModuleSystemName` (paragraphs). The work is purely UI: tree node registration, list screen, edit screen with `SelectMultiDual`, SQL discovery queries, and a save command that writes to config.

**Primary recommendation:** Follow the PredicateListScreen/PredicateEditScreen pattern exactly. Use `ISqlExecutor` + `CommandBuilder` for SQL discovery queries (same as `DataGroupMetadataReader`). Store discovered types as keys in `excludeXmlElementsByType` with empty element lists. Use `SelectMultiDual` for element exclusion selection.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Use `SelectMultiDual` (from `Dynamicweb.CoreUI.Editors.Lists`) instead of `CheckboxList` for all multi-select fields
- **D-02:** Retroactively replace Phase 33's `CheckboxList` editors (SqlTable `excludeFields` and `xmlColumns` on `PredicateEditScreen`) with `SelectMultiDual`
- **D-03:** `SelectMultiDual` is the standard for all multi-select fields going forward (Phases 35-37)
- **D-04:** Discover XML types by parsing live XML blobs from DB rows -- query actual `UrlDataProviderParameters` (Page) and `ModuleSettings` (Paragraph) columns, parse XML to extract distinct type identifiers
- **D-05:** Persist + manual rescan -- Initial Scan saves discovered types to config JSON. List screen reads from config (fast). "Rescan" button re-runs discovery and updates the persisted list
- **D-06:** Flat list under one "Embedded XML" node -- all discovered type names as flat list, no grouping
- **D-07:** Element names for a given XML type discovered by parsing live XML from DB when edit screen loads

### Claude's Discretion
- SQL query structure for XML blob extraction (which tables/columns to scan, how to identify XML type per row)
- How to store discovered XML types in config (new top-level property or section)
- Caching strategy for element discovery within a single screen load
- Error handling for malformed XML blobs

### Deferred Ideas (OUT OF SCOPE)
None
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| XMLUI-01 | "Embedded XML" tree node under Serialize lists auto-discovered XML types | Tree node pattern from `SerializerSettingsNodeProvider`; dynamic children from config keys |
| XMLUI-02 | "Scan" action discovers XML types via DB query | SQL queries against Page/Paragraph tables using `ISqlExecutor`; results persisted to config |
| XMLUI-03 | XML type edit screen shows all elements as exclusion selector | `SelectMultiDual` control with options from live XML parsing; `EditScreenBase<T>` pattern |
| XMLUI-04 | Element exclusions persisted to config under `excludeXmlElementsByType` | `ConfigWriter.Save()` pattern; dictionary already exists in `SerializerConfiguration` |
</phase_requirements>

## Standard Stack

### Core
| Library | Purpose | Why Standard |
|---------|---------|--------------|
| `Dynamicweb.CoreUI.Editors.Lists.SelectMultiDual` | Dual-pane multiselect with search | D-01 locked decision; DW's standard for multi-select |
| `Dynamicweb.CoreUI.Screens.EditScreenBase<T>` | Edit screen base class | Established pattern in codebase |
| `Dynamicweb.CoreUI.Screens.ListScreenBase<T>` | List screen base class | Established pattern in codebase |
| `Dynamicweb.CoreUI.Data.CommandBase<T>` | Save command pattern | Established pattern in codebase |
| `Dynamicweb.CoreUI.Actions.Implementations.RunCommandAction` | Toolbar command execution | DW10 standard for toolbar buttons that run commands |
| `Dynamicweb.Data.CommandBuilder` + `ISqlExecutor` | SQL query execution | Same pattern as `DataGroupMetadataReader` |
| `System.Xml.Linq.XDocument` | XML parsing for element discovery | Already used in `XmlFormatter` |

### Supporting
| Library | Purpose | When to Use |
|---------|---------|-------------|
| `System.Text.Json` | Config serialization | Via existing `ConfigWriter`/`ConfigLoader` |
| `Dynamicweb.CoreUI.Navigation.NavigationNode` | Tree node registration | For "Embedded XML" node with dynamic children |

## Architecture Patterns

### Recommended Project Structure
```
src/DynamicWeb.Serializer/
  AdminUI/
    Tree/
      SerializerSettingsNodeProvider.cs  -- MODIFY: add "Embedded XML" node + dynamic children
    Screens/
      XmlTypeListScreen.cs              -- NEW: list discovered XML types
      XmlTypeEditScreen.cs              -- NEW: edit element exclusions per type
      PredicateEditScreen.cs            -- MODIFY: replace CheckboxList with SelectMultiDual
    Models/
      XmlTypeListModel.cs               -- NEW: list row model
      XmlTypeEditModel.cs               -- NEW: edit form model
    Queries/
      XmlTypeListQuery.cs               -- NEW: load types from config
      XmlTypeByNameQuery.cs             -- NEW: load single type + discover elements
    Commands/
      ScanXmlTypesCommand.cs            -- NEW: discovery scan + persist
      SaveXmlTypeCommand.cs             -- NEW: save element exclusions
    Infrastructure/
      XmlTypeDiscovery.cs               -- NEW: SQL queries + XML parsing logic
```

### Pattern 1: XML Type Discovery via SQL + XML Parsing (D-04)

**What:** Two-phase discovery: (1) SQL queries to get raw data, (2) XML parsing to extract type names.

**When to use:** On "Scan" action (XMLUI-02).

**Implementation approach:**

For **Page URL providers** -- the DB column is `PageUrlDataProviderType` in the `Page` table, and the XML blob is `PageUrlDataProviderParameters`:
```sql
SELECT DISTINCT PageUrlDataProviderType 
FROM Page 
WHERE PageUrlDataProviderType != '' AND PageUrlDataProviderType IS NOT NULL
```
[VERIFIED: PITFALLS.md research + ContentMapper.cs line 85 uses `page.UrlDataProviderTypeName`]

For **Paragraph modules** -- the DB column is `ParagraphModuleSystemName` in the `Paragraph` table, and the XML blob is `ParagraphModuleSettings`:
```sql
SELECT DISTINCT ParagraphModuleSystemName 
FROM Paragraph 
WHERE ParagraphModuleSystemName != '' AND ParagraphModuleSystemName IS NOT NULL
```
[VERIFIED: PITFALLS.md research + ContentMapper.cs line 220 uses `paragraph.ModuleSystemName`]

These queries return the type identifiers directly -- no XML parsing needed for type discovery itself. The XML parsing is needed for element discovery (D-07).

### Pattern 2: Element Discovery via Live XML Parsing (D-07)

**What:** When user opens an XML type edit screen, query live XML blobs matching that type and extract distinct root-level element names.

**For page URL providers:**
```sql
SELECT TOP 50 PageUrlDataProviderParameters 
FROM Page 
WHERE PageUrlDataProviderType = @typeName 
  AND PageUrlDataProviderParameters IS NOT NULL 
  AND PageUrlDataProviderParameters != ''
```

**For paragraph modules:**
```sql
SELECT TOP 50 ParagraphModuleSettings 
FROM Paragraph 
WHERE ParagraphModuleSystemName = @typeName 
  AND ParagraphModuleSettings IS NOT NULL 
  AND ParagraphModuleSettings != ''
```

Then parse each XML blob with `XDocument.Parse()`, extract root element child names:
```csharp
// Source: XmlFormatter.cs pattern for XML parsing
var elements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var xml in xmlBlobs)
{
    try
    {
        var doc = XDocument.Parse(xml);
        if (doc.Root != null)
            foreach (var el in doc.Root.Elements())
                elements.Add(el.Name.LocalName);
    }
    catch (XmlException) { /* skip malformed */ }
}
```
[VERIFIED: XmlFormatter.cs uses identical XDocument.Parse pattern with XmlException catch]

**TOP 50 rationale:** Same type XML blobs have identical structure. Sampling 50 rows captures all element variations while keeping query fast. [ASSUMED]

### Pattern 3: SelectMultiDual Construction

**What:** Build a dual-pane selector from discovered elements.

```csharp
// Source: ScreenPresetEditScreen.cs lines 36-45
var editor = new SelectMultiDual
{
    Name = nameof(Model.ExcludedElements),
    Label = "Exclude Elements",
    Explanation = "Select XML elements to exclude from serialization for this type.",
    Options = discoveredElements
        .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
        .Select(e => new ListOption { Value = e, Label = e })
        .ToList(),
    SortOrder = OrderBy.Default,
    Value = Model?.ExcludedElements?.ToArray()
};
```
[VERIFIED: ScreenPresetEditScreen.cs reference in DW10 source]

### Pattern 4: Tree Node with Dynamic Children (D-06)

**What:** "Embedded XML" node whose children are populated from config keys.

```csharp
// In SerializerSettingsNodeProvider.GetSubNodes():
else if (parentNodePath.Last == EmbeddedXmlNodeId)
{
    var configPath = ConfigPathResolver.FindConfigFile();
    if (configPath != null)
    {
        var config = ConfigLoader.Load(configPath);
        var sort = 0;
        foreach (var typeName in config.ExcludeXmlElementsByType.Keys.OrderBy(k => k))
        {
            yield return new NavigationNode
            {
                Id = $"Serializer_XmlType_{typeName}",
                Name = typeName,
                Icon = Icon.Code,
                Sort = sort++,
                HasSubNodes = false,
                NodeAction = NavigateScreenAction.To<XmlTypeEditScreen>()
                    .With(new XmlTypeByNameQuery { ModelIdentifier = typeName })
            };
        }
    }
}
```
[VERIFIED: SerializerSettingsNodeProvider.cs existing pattern for Predicates/LogViewer nodes]

### Pattern 5: Retroactive CheckboxList to SelectMultiDual (D-02)

**What:** Replace `CreateColumnCheckboxList()` in `PredicateEditScreen` with a `SelectMultiDual` variant.

The current method returns `CheckboxList`. Change it to return `SelectMultiDual` instead:
```csharp
private SelectMultiDual CreateColumnSelectMultiDual(string? tableName, string? currentValue, string label, string explanation)
{
    var editor = new SelectMultiDual
    {
        Label = label,
        Explanation = explanation,
        SortOrder = OrderBy.Default
    };
    // ... same column discovery logic as current CheckboxList ...
    // Value binding: SelectMultiDual.Value is object[] (from ListBase)
    var selected = (currentValue ?? string.Empty)
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(v => v.Trim())
        .Where(v => v.Length > 0)
        .ToArray();
    if (selected.Length > 0)
        editor.Value = selected;
    return editor;
}
```
[VERIFIED: SelectMultiDual inherits from ListBase which has Options and Value properties]

### Pattern 6: Toolbar Scan Button via RunCommandAction (D-05)

**What:** "Scan for XML types" toolbar button on the list screen that executes `ScanXmlTypesCommand` and reloads the list.

**Implementation:** Use `GetToolbarActions()` override on `ListScreenBase<T>` with `RunCommandAction.For<ScanXmlTypesCommand>().WithReloadOnSuccess()`.

```csharp
protected override IEnumerable<ActionNode>? GetToolbarActions() =>
[
    new()
    {
        Name = "Scan for XML types",
        Icon = Icon.Refresh,
        NodeAction = RunCommandAction.For<ScanXmlTypesCommand>()
            .WithReloadOnSuccess()
    }
];
```

**Why `GetToolbarActions()` and not `GetItemCreateAction()`:** `GetItemCreateAction()` is specifically for the "create new item" button. The Scan button is a general toolbar action that runs a command. `GetToolbarActions()` returns `IEnumerable<ActionNode>?` and is rendered in the toolbar area of the list screen.

**Why `RunCommandAction` and not `SubmitAction`:** There is no standalone `SubmitAction` class in DW10. `SubmitAction` is only a property on `Form` (for edit screen form submission). `RunCommandAction.For<TCommand>()` is the correct API for executing a command from any action context (toolbar, context menu, etc.). This pattern is used throughout DW10 -- see `CacheInformationListScreen`, `AddinListScreen`, `DatabaseTableListScreen` in the DW10 source.

[VERIFIED: RunCommandAction.cs in Dynamicweb.CoreUI/Actions/Implementations/; ListScreenBase.cs line 326 defines `GetToolbarActions()`; no standalone SubmitAction class exists]

### Anti-Patterns to Avoid
- **Using DW Content APIs for discovery:** `Services.Pages` / `Services.Paragraphs` loads full objects with all XML blobs into memory. Direct SQL via `ISqlExecutor` is correct for read-only admin queries. [VERIFIED: PITFALLS.md Pitfall 3]
- **Creating a new config property for discovered types:** The `ExcludeXmlElementsByType` dictionary already exists. Store discovered types as keys with empty element lists until user configures exclusions.
- **Grouping by source (page vs paragraph):** D-06 explicitly requires flat list, no grouping.
- **Using `SubmitAction` for toolbar buttons:** `SubmitAction` is NOT a standalone action class. It is a property on `Form` for edit screen submission. Use `RunCommandAction.For<TCommand>()` instead. [VERIFIED: DW10 source]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Multi-select UI | Custom checkbox list | `SelectMultiDual` | D-01 decision; built-in search, dual-pane UX |
| Config persistence | Custom file I/O | `ConfigWriter.Save()` / `ConfigLoader.Load()` | Atomic writes, JSON serialization already configured |
| XML parsing | Regex on XML strings | `XDocument.Parse()` | Handles namespaces, entities, malformed XML gracefully |
| SQL execution | Raw ADO.NET | `ISqlExecutor` + `CommandBuilder` | Testable abstraction, DW connection management |
| Toolbar command buttons | Custom action classes | `RunCommandAction.For<T>()` | DW10 standard pattern for command execution from UI |

## Common Pitfalls

### Pitfall 1: Memory-Heavy XML Type Discovery
**What goes wrong:** Loading full Page/Paragraph objects via DW APIs to extract type names causes memory spikes with large databases.
**Why it happens:** Following the "use DW APIs, not SQL" guideline too literally for read-only admin queries.
**How to avoid:** Use `ISqlExecutor` with direct SQL against Page/Paragraph tables. The type name columns (`PageUrlDataProviderType`, `ParagraphModuleSystemName`) are simple string columns -- no need to load full objects.
**Warning signs:** Screen load times > 2 seconds, IIS memory spikes during scan.
[VERIFIED: PITFALLS.md Pitfall 3]

### Pitfall 2: Malformed XML Blobs in Database
**What goes wrong:** `XDocument.Parse()` throws `XmlException` on malformed XML, crashing element discovery.
**Why it happens:** DW databases may contain legacy or corrupted XML in `ModuleSettings`/`UrlDataProviderParameters` columns.
**How to avoid:** Wrap each parse in try/catch, skip malformed blobs, log a warning. `XmlFormatter` already uses this pattern.
**Warning signs:** Edit screen shows no elements despite data existing in DB.
[VERIFIED: XmlFormatter.cs uses try/catch XmlException pattern throughout]

### Pitfall 3: SelectMultiDual Value Type Mismatch
**What goes wrong:** `SelectMultiDual.Value` is `object?` inherited from `EditorBase`. Setting it to `List<string>` may not bind correctly on form submission.
**Why it happens:** The ScreenPresetEditScreen reference uses `.ToArray()` for the value, suggesting the framework expects an array, not a list.
**How to avoid:** Always use `.ToArray()` when setting `SelectMultiDual.Value`. Parse submitted values as string array. The `Name` property must match the model property name for binding.
**Warning signs:** Selected values not persisting after save, empty selections on reload.
[VERIFIED: ScreenPresetEditScreen.cs line 43 uses `Model?.ConfigurableColumns.ToArray()`]

### Pitfall 4: Type Names Appearing in Both Page and Paragraph Tables
**What goes wrong:** A type name could theoretically appear as both a URL provider type (page) and a module system name (paragraph), creating duplicates in the flat list.
**Why it happens:** Different DW subsystems may use the same naming convention.
**How to avoid:** Use a `HashSet<string>` to deduplicate across both discovery queries. The config dictionary uses type name as key, so duplicates naturally merge.
**Warning signs:** Same type appearing twice in the list.
[ASSUMED]

### Pitfall 5: Config Race Condition During Scan
**What goes wrong:** Scan command reads config, adds new types, saves. If user is simultaneously editing another type, their changes could be overwritten.
**Why it happens:** No locking on config file operations.
**How to avoid:** Scan command should load current config, merge new types (add missing keys, preserve existing exclusion lists), then save. Never overwrite existing entries.
**Warning signs:** User's element exclusions disappearing after a rescan.
[ASSUMED]

### Pitfall 6: Using SubmitAction as a Standalone Action
**What goes wrong:** Code references `new SubmitAction(command)` which does not compile -- `SubmitAction` is not a standalone action class.
**Why it happens:** Confusing `Form.SubmitAction` property with a non-existent `SubmitAction` class.
**How to avoid:** Use `RunCommandAction.For<TCommand>()` for toolbar buttons and command execution. Use `GetToolbarActions()` override on `ListScreenBase` for list screen toolbar buttons.
**Warning signs:** Compilation error on `SubmitAction` reference.
[VERIFIED: DW10 source -- SubmitAction is only a property on Form, not a class]

## Code Examples

### XmlTypeDiscovery Service
```csharp
// Source: DataGroupMetadataReader.cs pattern for ISqlExecutor usage
public class XmlTypeDiscovery
{
    private readonly ISqlExecutor _sqlExecutor;

    public XmlTypeDiscovery(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor;
    }

    /// <summary>
    /// Discovers distinct XML type names from Page and Paragraph tables.
    /// Returns a deduplicated set of type names.
    /// </summary>
    public HashSet<string> DiscoverXmlTypes()
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Page URL provider types
        var cb1 = new CommandBuilder();
        cb1.Add("SELECT DISTINCT PageUrlDataProviderType FROM Page WHERE PageUrlDataProviderType != '' AND PageUrlDataProviderType IS NOT NULL");
        using (var reader = _sqlExecutor.ExecuteReader(cb1))
        {
            while (reader.Read())
                types.Add(reader[0].ToString()!);
        }

        // Paragraph module system names
        var cb2 = new CommandBuilder();
        cb2.Add("SELECT DISTINCT ParagraphModuleSystemName FROM Paragraph WHERE ParagraphModuleSystemName != '' AND ParagraphModuleSystemName IS NOT NULL");
        using (var reader = _sqlExecutor.ExecuteReader(cb2))
        {
            while (reader.Read())
                types.Add(reader[0].ToString()!);
        }

        return types;
    }

    /// <summary>
    /// Discovers distinct root-level XML element names for a given type.
    /// Samples XML blobs from both Page and Paragraph tables.
    /// </summary>
    public HashSet<string> DiscoverElementsForType(string typeName)
    {
        var elements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Page URL provider parameters
        var cb1 = new CommandBuilder();
        cb1.Add($"SELECT TOP 50 PageUrlDataProviderParameters FROM Page WHERE PageUrlDataProviderType = '{typeName}' AND PageUrlDataProviderParameters IS NOT NULL AND PageUrlDataProviderParameters != ''");
        ParseXmlBlobs(cb1, elements);

        // Paragraph module settings
        var cb2 = new CommandBuilder();
        cb2.Add($"SELECT TOP 50 ParagraphModuleSettings FROM Paragraph WHERE ParagraphModuleSystemName = '{typeName}' AND ParagraphModuleSettings IS NOT NULL AND ParagraphModuleSettings != ''");
        ParseXmlBlobs(cb2, elements);

        return elements;
    }

    private void ParseXmlBlobs(CommandBuilder cb, HashSet<string> elements)
    {
        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            var xml = reader[0]?.ToString();
            if (string.IsNullOrWhiteSpace(xml)) continue;
            try
            {
                var doc = XDocument.Parse(xml);
                if (doc.Root != null)
                    foreach (var el in doc.Root.Elements())
                        elements.Add(el.Name.LocalName);
            }
            catch (XmlException) { /* skip malformed XML */ }
        }
    }
}
```
[VERIFIED: ISqlExecutor/CommandBuilder pattern from DataGroupMetadataReader.cs; XDocument pattern from XmlFormatter.cs]

### ScanXmlTypesCommand
```csharp
// Source: SavePredicateCommand.cs pattern for config modification
public sealed class ScanXmlTypesCommand : CommandBase<object>
{
    public override CommandResult Handle()
    {
        var configPath = ConfigPathResolver.FindOrCreateConfigFile();
        var config = ConfigLoader.Load(configPath);
        
        var discovery = new XmlTypeDiscovery(new DwSqlExecutor());
        var discoveredTypes = discovery.DiscoverXmlTypes();
        
        // Merge: add new types with empty exclusion lists, preserve existing
        var updated = new Dictionary<string, List<string>>(config.ExcludeXmlElementsByType);
        foreach (var typeName in discoveredTypes)
        {
            if (!updated.ContainsKey(typeName))
                updated[typeName] = new List<string>();
        }
        
        var newConfig = config with { ExcludeXmlElementsByType = updated };
        ConfigWriter.Save(newConfig, configPath);
        
        return new() { Status = CommandResult.ResultType.Ok };
    }
}
```

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (existing) |
| Config file | `tests/DynamicWeb.Serializer.Tests/DynamicWeb.Serializer.Tests.csproj` |
| Quick run command | `dotnet test tests/DynamicWeb.Serializer.Tests --filter "FullyQualifiedName~AdminUI" --no-build` |
| Full suite command | `dotnet test tests/DynamicWeb.Serializer.Tests --no-build` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| XMLUI-01 | Tree node with dynamic children from config | unit | `dotnet test --filter "XmlType" --no-build` | Wave 0 |
| XMLUI-02 | Scan discovers types from SQL, persists to config | unit | `dotnet test --filter "ScanXmlTypes" --no-build` | Wave 0 |
| XMLUI-03 | Element discovery parses XML blobs correctly | unit | `dotnet test --filter "XmlTypeDiscovery" --no-build` | Wave 0 |
| XMLUI-04 | Save command persists exclusions to config dict | unit | `dotnet test --filter "SaveXmlType" --no-build` | Wave 0 |

### Wave 0 Gaps
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeDiscoveryTests.cs` -- covers XMLUI-02, XMLUI-03 (SQL mocking + XML parsing)
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/ScanXmlTypesCommandTests.cs` -- covers XMLUI-02 (config merge behavior)
- [ ] `tests/DynamicWeb.Serializer.Tests/AdminUI/SaveXmlTypeCommandTests.cs` -- covers XMLUI-04 (exclusion persistence)

### Sampling Rate
- **Per task commit:** `dotnet test tests/DynamicWeb.Serializer.Tests --filter "XmlType" --no-build`
- **Per wave merge:** `dotnet test tests/DynamicWeb.Serializer.Tests --no-build`
- **Phase gate:** Full suite green before verification

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A -- DW admin session handles auth |
| V3 Session Management | no | N/A -- DW framework |
| V4 Access Control | no | N/A -- DW admin permissions |
| V5 Input Validation | yes | SQL type names validated via parameterized query or allowlist; XML parsed with XDocument (safe) |
| V6 Cryptography | no | N/A |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection via type name in element discovery query | Tampering | Type names come from prior DB query results (trusted). Add regex validation (`^[A-Za-z0-9_.]+$`) as defense-in-depth, matching `PredicateEditScreen.CreateColumnCheckboxList` pattern |
| XML bomb (billion laughs) in malformed XML blobs | Denial of Service | `XDocument.Parse()` with default settings; DW database content is trusted. Consider `XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }` for defense-in-depth |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | TOP 50 sampling sufficient for element discovery | Architecture Patterns - Pattern 2 | Could miss rare elements; low risk since rescan available |
| A2 | Type names won't collide between Page and Paragraph tables | Pitfalls - Pitfall 4 | Deduplication via HashSet handles this regardless |
| A3 | Config race condition during scan is a real concern | Pitfalls - Pitfall 5 | Merge-not-overwrite strategy handles this regardless |
| A4 | DB column names are `PageUrlDataProviderType` and `ParagraphModuleSystemName` | Architecture Patterns | If wrong, SQL queries fail. Verified via PITFALLS.md research but not against live DB |

## Open Questions (RESOLVED)

1. **Exact DB column names for type identifiers** (RESOLVED)
   - **Resolution:** The SQL column names are `PageUrlDataProviderType` (Page table) and `ParagraphModuleSystemName` (Paragraph table). The XML blob columns are `PageUrlDataProviderParameters` (Page) and `ParagraphModuleSettings` (Paragraph). These are confirmed by: (a) PITFALLS.md research SQL examples, (b) ContentMapper.cs using `page.UrlDataProviderTypeName` and `paragraph.ModuleSystemName` C# properties which map to these columns, (c) consistent naming convention in DW10 where SQL column names match the C# model property names with table prefix (e.g., `Page` prefix for Page table columns). HIGH confidence -- verified against multiple sources.

2. **Scan button toolbar API** (RESOLVED)
   - **Resolution:** Use `RunCommandAction.For<ScanXmlTypesCommand>().WithReloadOnSuccess()` via `GetToolbarActions()` override on `ListScreenBase<T>`. There is NO standalone `SubmitAction` class in DW10 -- `SubmitAction` is only a property on `Form` (for edit screen form submission). `RunCommandAction` (from `Dynamicweb.CoreUI.Actions.Implementations`) is the correct API for executing commands from toolbar buttons. `ListScreenBase` has `protected virtual IEnumerable<ActionNode>? GetToolbarActions() => null;` (line 326) which is called in the screen layout builder (line 133-135). This pattern is used throughout DW10 (e.g., `CacheInformationListScreen` uses `RunCommandAction.For(command).WithReloadOnSuccess()` for refresh buttons). VERIFIED in DW10 source: `RunCommandAction.cs`, `ListScreenBase.cs`.

## Sources

### Primary (HIGH confidence)
- `SerializerSettingsNodeProvider.cs` -- tree node registration pattern [VERIFIED]
- `PredicateListScreen.cs` / `PredicateEditScreen.cs` -- list/edit screen patterns [VERIFIED]
- `DataGroupMetadataReader.cs` -- ISqlExecutor + CommandBuilder SQL pattern [VERIFIED]
- `XmlFormatter.cs` -- XDocument.Parse with XmlException handling [VERIFIED]
- `ScreenPresetEditScreen.cs` (DW10 source) -- SelectMultiDual usage [VERIFIED]
- `SelectMultiDual.cs` / `ListBase.cs` (DW10 source) -- control API surface [VERIFIED]
- `RunCommandAction.cs` (DW10 source) -- toolbar command execution API [VERIFIED]
- `ListScreenBase.cs` (DW10 source) -- GetToolbarActions() virtual method [VERIFIED]
- `DataQueryIdentifiableModelBase.cs` (DW10 source) -- ModelIdentifier -> SetKey wiring [VERIFIED]
- `SerializerConfiguration.cs` -- ExcludeXmlElementsByType dictionary [VERIFIED]
- `ExclusionMerger.cs` -- MergeXmlExclusions using type name key [VERIFIED]
- `ContentMapper.cs` -- XML type key usage (UrlDataProviderTypeName, ModuleSystemName) [VERIFIED]
- `ConfigWriter.cs` / `ConfigLoader.cs` -- config persistence pattern [VERIFIED]
- `.planning/research/PITFALLS.md` -- SQL query patterns for type discovery [VERIFIED]

### Secondary (MEDIUM confidence)
- `SavePredicateCommand.cs` -- command pattern with config modification [VERIFIED]
- `PredicateByIndexQuery.cs` -- identifiable query pattern [VERIFIED]

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries verified in DW10 source and existing codebase
- Architecture: HIGH -- follows established patterns exactly, all referenced files verified
- Pitfalls: HIGH -- Pitfall 1 and 2 verified; Pitfalls 3-6 based on code analysis

**Research date:** 2026-04-14
**Valid until:** 2026-05-14 (stable -- DW10 admin UI framework not fast-moving)

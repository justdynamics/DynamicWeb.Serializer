# Phase 41: Admin-UI polish + cross-page consistency — Pattern Map

**Mapped:** 2026-05-01
**Files analyzed:** 11 (9 modified, 2 created)
**Analogs found:** 11 / 11 (every modified file is its own best analog; both new test scaffolds map to existing AdminUI test classes)

> **Note on analog confidence:** Phase 41 is a polish phase against admin-UI surface that already exists. Every fix has a working precedent **inside the file being modified**, **inside a sibling file in the same folder**, or **inside an existing test class with the same name suffix**. There is nothing to invent. The "closest analog" for most modified files is the file itself plus a related sibling that demonstrates the canonical shape.

---

## File Classification

| File | Status | Role | Data Flow | Closest Analog | Match Quality |
|------|--------|------|-----------|----------------|---------------|
| `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` | modified | tree node-provider | navigation | self (line 75 string change) + sibling lines 64, 86, 97 (other tree-node `Name` strings) | exact (in-file) |
| `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeListScreen.cs` | modified | list-screen | request-response | self (line 13) | exact (in-file) |
| `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs` | modified | edit-screen | request-response | self (D-01: line 130) + `XmlTypeEditScreen.CreateElementSelector` (D-06: same short-circuit shape) | exact + sibling |
| `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs` | modified | edit-screen | request-response | self (D-05: lines 90-135 `CreateElementSelector`; D-08: lines 60-66 Sample XML Textarea) + `LogViewerScreen.cs:97-101` (Textarea with Readonly) | exact (in-file) |
| `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` | modified | edit-screen | request-response | self (D-11: lines 88-89 option labels) + `SerializerSettingsEditScreen.CreateLogLevelSelect` (string-Value Select) | exact + sibling |
| `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` | modified | model | data-binding | `SerializerSettingsModel.LogLevel` and `.ConflictStrategy` (string-typed `[ConfigurableProperty]` on Select-bound field) | exact (sibling) |
| `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` | modified | command | persist | self (lines 161, 183 enum assignment) + `ConfigLoader` Enum.TryParse pathway | role-match |
| `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs` | modified | query | hydrate | self (line 32 `Mode = pred.Mode`) | exact (in-file) |
| `docs/baselines/Swift2.2-baseline.md` | modified | docs | n/a | self (existing markdown structure) | exact (in-file) |
| `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeEditScreenTests.cs` | created | test (unit) | factory + assertion | `XmlTypeCommandTests.cs` (config-tempdir scaffold) + `SerializerSettingsNodeProviderModeTreeTests.cs` (provider-instance + public-method-call pattern) | role-match (no direct screen-test precedent) |
| `tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeEditScreenTests.cs` | created | test (unit) | factory + assertion | `ItemTypeCommandTests.cs` (sibling Item-Type tempdir scaffold) + same screen-test pattern as above | role-match |

---

## Pattern Assignments

### `src/DynamicWeb.Serializer/AdminUI/Tree/SerializerSettingsNodeProvider.cs` (tree node-provider, navigation)

**Analog:** self — every other `yield return new NavigationNode { ... Name = "..." }` block in the same `GetSubNodes` method demonstrates the surface being changed. The rename is a one-string edit.

**Existing pattern at lines 72-81 (the rename anchor):**
```csharp
yield return new NavigationNode
{
    Id = ItemTypesNodeId,           // const stays "Serializer_ItemTypes" per D-02
    Name = "Item Types",            // <-- D-01: rename to "Item Type Excludes"
    Icon = Icon.ListUl,
    Sort = 20,
    HasSubNodes = true,
    NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
        .With(new ItemTypeListQuery())
};
```

**Sibling pattern at lines 83-92 (`Name = "Embedded XML"` — already a friendly UX label, not a developer term — confirms the convention):**
```csharp
yield return new NavigationNode
{
    Id = XmlTypesNodeId,
    Name = "Embedded XML",          // friendly label != const id "Serializer_XmlTypes"
    Icon = Icon.BracketsCurly,
    Sort = 30,
    HasSubNodes = HasXmlTypes(),
    NodeAction = NavigateScreenAction.To<XmlTypeListScreen>()
        .With(new XmlTypeListQuery())
};
```

**Apply:** Change line 75 only. Const at line 27 (`ItemTypesNodeId = "Serializer_ItemTypes"`) stays per D-02.

---

### `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeListScreen.cs` (list-screen, request-response)

**Analog:** self (line 13 is the only string to change).

**Existing pattern (lines 11-13):**
```csharp
public sealed class ItemTypeListScreen : ListScreenBase<ItemTypeListModel>
{
    protected override string GetScreenName() => "Item Types";   // <-- D-01: "Item Type Excludes"
```

**Sibling pattern from `XmlTypeListScreen` confirms `GetScreenName() => const string` is the universal shape across list screens.**

**Apply:** `=> "Item Type Excludes"`.

---

### `src/DynamicWeb.Serializer/AdminUI/Screens/ItemTypeEditScreen.cs` (edit-screen, request-response)

**Analog:** self (D-01 anchor at line 130) + sibling `XmlTypeEditScreen.CreateElementSelector` for D-06 same-shape merge fix.

**D-01 GetScreenName pattern (lines 130-131):**
```csharp
protected override string GetScreenName() =>
    !string.IsNullOrWhiteSpace(Model?.SystemName) ? $"Item Type: {Model.SystemName}" : "Item Type";
```
Apply: `$"Item Type Excludes - {Model.SystemName}" : "Item Type Excludes"`.

**D-06 short-circuit bug pattern (lines 82-128 of the same file — `CreateFieldSelector`):**
```csharp
private SelectMultiDual CreateFieldSelector()
{
    var editor = new SelectMultiDual
    {
        Label = "Exclude Fields",
        Explanation = "Select fields to exclude from serialization for this item type.",
        SortOrder = OrderBy.Default
    };

    if (string.IsNullOrWhiteSpace(Model?.SystemName))
        return editor;

    try
    {
        var itemType = ItemManager.Metadata.GetItemType(Model.SystemName);
        if (itemType == null)
        {
            editor.Explanation = "Item type not found. Fields cannot be loaded.";
            return editor;                                    // <-- BUG: early-return drops Model.ExcludedFields
        }

        var allFields = ItemManager.Metadata.GetItemFields(itemType);

        editor.Options = allFields
            .Where(f => !string.IsNullOrEmpty(f.SystemName))
            .OrderBy(f => f.SystemName, StringComparer.OrdinalIgnoreCase)
            .Select(f => new ListOption { Value = f.SystemName, Label = $"{f.Name} ({f.SystemName})" })
            .ToList();

        // Pre-select currently excluded fields
        var selected = (Model.ExcludedFields ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .ToArray();

        if (selected.Length > 0)
            editor.Value = selected;
    }
    catch (Exception ex)
    {
        editor.Explanation = $"Could not load fields: {ex.Message}";
    }

    return editor;
}
```

**Apply (merge-saved-into-discovered shape — see RESEARCH.md lines 256-292 and 443-482):**
- Build `var allFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);` up front
- Wrap `ItemManager.Metadata.GetItemFields(itemType)` in try/catch and add field SystemNames into the HashSet
- Always parse `Model.ExcludedFields` and add saved entries into the same HashSet
- After the merge, render `editor.Options` from the HashSet and pre-select from `Model.ExcludedFields`
- Empty-explanation fallback only when both discovered and saved are empty

**The Label preservation pattern is at line 109** — `Label = $"{f.Name} ({f.SystemName})"` — proves parens-in-labels work correctly in CoreUI Select renderers (per RESEARCH.md D-13 confirmation that parens are NOT the screen-error cause).

---

### `src/DynamicWeb.Serializer/AdminUI/Screens/XmlTypeEditScreen.cs` (edit-screen, request-response)

**Analog:** self for both D-05 and D-08; `LogViewerScreen.cs:97-101` for Textarea-with-Readonly canon.

**D-05 short-circuit bug pattern (lines 90-135 of the same file — `CreateElementSelector`):**
```csharp
private SelectMultiDual CreateElementSelector()
{
    var editor = new SelectMultiDual
    {
        Label = "Exclude Elements",
        Explanation = "Select XML elements to exclude from serialization for this type.",
        SortOrder = OrderBy.Default
    };

    if (string.IsNullOrWhiteSpace(Model?.TypeName))
        return editor;

    try
    {
        var discovery = new XmlTypeDiscovery(new DwSqlExecutor());
        var allElements = discovery.DiscoverElementsForType(Model.TypeName);

        if (allElements.Count == 0)
        {
            editor.Explanation = "No XML data found in database for this type. Elements will appear after data is available.";
            return editor;                                     // <-- BUG: early-return drops Model.ExcludedElements (21 saved)
        }

        editor.Options = allElements
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .Select(e => new ListOption { Value = e, Label = e })
            .ToList();

        var selected = (Model.ExcludedElements ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .ToArray();

        if (selected.Length > 0)
            editor.Value = selected;
    }
    catch (Exception ex)
    {
        editor.Explanation = $"Could not discover elements: {ex.Message}";
    }

    return editor;
}
```

**D-08 Sample XML Textarea pattern (lines 60-66):**
```csharp
new Dynamicweb.CoreUI.Editors.Inputs.Textarea
{
    Label = "Sample XML from database",
    Explanation = "This is a sample of the raw XML found in the database for this type. The element or parameter names shown in the exclusion list above correspond to the structure you see here.",
    Value = sample,
    Readonly = true                                            // <-- D-10 already wired; add Rows = 30 for D-08
}
```

**Sibling Textarea pattern from `LogViewerScreen.cs:97-101` (the established `Textarea` editor shape):**
```csharp
nameof(LogViewerModel.RawLogText) => new Textarea
{
    Label = "Log Output",
    Explanation = "Full log text from the selected file"
},
```
**Note:** `LogViewerScreen` doesn't set `Rows` either — Phase 41 sets `Rows = 30` here as a new (but in-DLL-supported) knob. RESEARCH.md A4 confirms `Rows` is on the `Textarea` type per CoreUI 10.23.9 strings dump. Fallback knob is `Height = "60vh"` (CSS string) per RESEARCH.md line 537.

**Apply (D-05): merge-saved-into-discovered identical to ItemTypeEditScreen.CreateFieldSelector; see RESEARCH.md lines 429-482 for the full target shape.**
**Apply (D-08): add `Rows = 30,` to the Textarea initializer at line 65; D-10 `Readonly = true` already in place at line 65.**

---

### `src/DynamicWeb.Serializer/AdminUI/Screens/PredicateEditScreen.cs` (edit-screen, request-response)

**Analog:** self for D-11 (lines 83-91); `SerializerSettingsEditScreen.CreateLogLevelSelect` (lines 96-109) for the canonical "string-typed Select with string-Value ListOptions" pattern that D-13 aligns Mode with.

**D-11 option-label pattern (lines 83-91, current — buggy in label, will continue to work after string-Mode migration):**
```csharp
nameof(PredicateEditModel.Mode) => new Select
{
    SortOrder = OrderBy.Default,
    Options = new List<ListOption>
    {
        new() { Value = nameof(DeploymentMode.Deploy), Label = "Deploy (source-wins)" },   // <-- D-11: drop suffix
        new() { Value = nameof(DeploymentMode.Seed), Label = "Seed (field-level merge)" }   // <-- D-11: drop suffix
    }
},
```

**Sibling string-Value Select pattern from `SerializerSettingsEditScreen.cs:96-109` (the canonical working shape):**
```csharp
private static Select CreateLogLevelSelect()
{
    return new Select
    {
        SortOrder = OrderBy.Default,
        Options = new List<ListOption>
        {
            new() { Value = "info",  Label = "Info" },
            new() { Value = "debug", Label = "Debug" },
            new() { Value = "warn",  Label = "Warn" },
            new() { Value = "error", Label = "Error" }
        }
    };
}
```
**This Select binds to `SerializerSettingsModel.LogLevel`, which is `public string LogLevel { get; set; } = "info";` (string-typed). That is the working precedent the Mode editor must converge on (D-13 fix).**

**Apply (D-11):**
```csharp
new() { Value = nameof(DeploymentMode.Deploy), Label = "Deploy" },
new() { Value = nameof(DeploymentMode.Seed),   Label = "Seed" }
```

**Apply (D-13 surface — minimal screen change):** the editor body stays as-is; only the model property type changes (next file). The `Value = nameof(DeploymentMode.Deploy)` produces the correct string `"Deploy"` for the now-string-typed `Model.Mode`.

---

### `src/DynamicWeb.Serializer/AdminUI/Models/PredicateEditModel.cs` (model, data-binding)

**Analog:** `SerializerSettingsModel.LogLevel` and `SerializerSettingsModel.ConflictStrategy` (sibling file) — both are string-typed model properties bound to string-Value Selects via `[ConfigurableProperty(...)]`.

**Sibling pattern from `SerializerSettingsModel.cs:12-13` (LogLevel — string-typed, default `"info"`):**
```csharp
[ConfigurableProperty("Log Level", explanation: "Logging verbosity")]
public string LogLevel { get; set; } = "info";
```

**Sibling pattern from `SerializerSettingsModel.cs:21-22` (ConflictStrategy — string-typed, default `"source-wins"`, similar token shape to enum-name):**
```csharp
[ConfigurableProperty("Conflict Strategy", explanation: "Read-only — Deploy uses source-wins, Seed uses field-level merge (Phase 39). This setting is ignored on save in v0.5.x; kept for backward UI compatibility only.")]
public string ConflictStrategy { get; set; } = "source-wins";
```

**Current buggy code at `PredicateEditModel.cs:11-17`:**
```csharp
/// <summary>
/// Phase 40 D-01: the predicate's own deployment mode. Persists into ProviderPredicateDefinition.Mode.
/// Read by SavePredicateCommand when constructing the saved predicate, and by PredicateByIndexQuery
/// when populating the edit screen for an existing predicate. Defaults to Deploy on the new-predicate path.
/// </summary>
[ConfigurableProperty("Mode", explanation: "Deploy = source-wins (overwrite target). Seed = field-level merge (preserve customer-edited fields).")]
public DeploymentMode Mode { get; set; } = DeploymentMode.Deploy;
```

**`hint:` parameter precedent (single existing usage in source — `SelectorBuilder.CreatePageSelector`):** `PredicateEditScreen.cs:99` ships `hint: "Select root page for this predicate"`. This is the only `hint:` named-arg in production source, confirming the keyword exists on at least one CoreUI builder; per RESEARCH.md A1, `[ConfigurableProperty(... hint: ...)]` is the closest CoreUI primitive for the D-12 "tooltip on the select's label" intent (no `Tooltip` type exists in CoreUI 10.23.9).

**Apply (D-12 + D-13):**
```csharp
/// <summary>
/// Phase 41 D-13: string-typed for DW Select binding (matches LogLevel / ConflictStrategy precedent
/// in SerializerSettingsModel). Persists into ProviderPredicateDefinition.Mode (enum) via
/// Enum.Parse&lt;DeploymentMode&gt; on save in SavePredicateCommand. DW Select dropdowns bind by
/// string Value (project memory feedback_dw_patterns.md: enum/int model-property type breaks
/// the value-match-on-render).
/// </summary>
[ConfigurableProperty("Mode", hint: "Deploy = source-wins (YAML overwrites destination). Seed = destination-wins field-level merge (only fills empty destination fields).")]
public string Mode { get; set; } = nameof(DeploymentMode.Deploy);
```

**Caveat (RESEARCH.md A1, OQ1):** if live-host smoke shows `hint:` rendering as inline placeholder text rather than a hover tooltip on the label, fall back to setting `Hint = "..."` directly on the Select instance in `PredicateEditScreen.GetEditor`. Either knob ships in the same plan.

---

### `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` (command, persist)

**Analog:** self (lines 161 and 183 are the two enum-assignment sites).

**Current pattern at `SavePredicateCommand.cs:158-170` (Content branch):**
```csharp
predicate = new ProviderPredicateDefinition
{
    Name = Model.Name.Trim(),
    Mode = Model.Mode,                      // <-- D-13: was DeploymentMode (assignable); becomes string -> Enum.Parse
    ProviderType = "Content",
    Path = path,
    AreaId = Model.AreaId,
    PageId = Model.PageId,
    Excludes = excludes,
    ExcludeFields = excludeFields,
    ExcludeXmlElements = excludeXmlElements,
    ExcludeAreaColumns = excludeAreaColumns
};
```

**Current pattern at `SavePredicateCommand.cs:180-197` (SqlTable branch — same enum site at line 183):**
```csharp
predicate = new ProviderPredicateDefinition
{
    Name = Model.Name.Trim(),
    Mode = Model.Mode,                      // <-- D-13: same change as line 161
    ProviderType = "SqlTable",
    Table = Model.Table?.Trim(),
    // ...
};
```

**Existing string-validation precedent in the same file (lines 70-72) — the shape D-13 invalid-Mode handling should mirror:**
```csharp
else
{
    return new() { Status = CommandResult.ResultType.Invalid, Message = $"Unknown provider type: {providerType}" };
}
```

**Apply (D-13):**
1. Replace `Mode = Model.Mode,` at line 161 and line 183 with `Mode = Enum.Parse<DeploymentMode>(Model.Mode, ignoreCase: true),`.
2. Add a guard near the top (around lines 34-36, alongside the existing Name / ProviderType validation) that catches `ArgumentException` from `Enum.Parse` (or pre-validates with `Enum.TryParse<DeploymentMode>`) and returns:
   ```csharp
   return new() { Status = CommandResult.ResultType.Invalid,
       Message = $"Mode must be 'Deploy' or 'Seed' (case-insensitive); got '{Model.Mode}'" };
   ```
   This mirrors the ConfigLoader ValidateIdentifiers message style cited in RESEARCH.md security section.

---

### `src/DynamicWeb.Serializer/AdminUI/Queries/PredicateByIndexQuery.cs` (query, hydrate)

**Analog:** self (line 32 is the single enum-to-string conversion site).

**Current pattern at `PredicateByIndexQuery.cs:28-49`:**
```csharp
var pred = config.Predicates[Index];
return new PredicateEditModel
{
    Index = Index,
    Mode = pred.Mode,                       // <-- D-13: enum -> string for editor binding
    Name = pred.Name,
    ProviderType = pred.ProviderType,
    AreaId = pred.AreaId,
    PageId = pred.PageId,
    Excludes = string.Join("\n", pred.Excludes),
    Table = pred.Table ?? string.Empty,
    NameColumn = pred.NameColumn ?? string.Empty,
    CompareColumns = pred.CompareColumns ?? string.Empty,
    ServiceCaches = string.Join("\n", pred.ServiceCaches),
    ExcludeFields = string.Join("\n", pred.ExcludeFields),
    XmlColumns = string.Join("\n", pred.XmlColumns),
    ExcludeXmlElements = string.Join("\n", pred.ExcludeXmlElements),
    ExcludeAreaColumns = string.Join("\n", pred.ExcludeAreaColumns),
    WhereClause = pred.Where ?? string.Empty,
    IncludeFields = string.Join("\n", pred.IncludeFields),
    ResolveLinksInColumns = string.Join("\n", pred.ResolveLinksInColumns)
};
```

**Apply (D-13):** `Mode = pred.Mode.ToString(),` — `DeploymentMode` is `enum DeploymentMode { Deploy, Seed }` per `src/DynamicWeb.Serializer/Configuration/DeploymentMode.cs`, so `.ToString()` produces exactly `"Deploy"` / `"Seed"`, matching the `Value = nameof(DeploymentMode.Deploy)` in the Select editor.

**Note: line 20 (the new-predicate path `return new PredicateEditModel();`) does NOT need a change** — the model's new default (`= nameof(DeploymentMode.Deploy)`) already produces the correct string for the new-predicate flow.

---

### `docs/baselines/Swift2.2-baseline.md` (docs)

**Analog:** self — append a short section explaining the intentional emptiness of `excludeFieldsByItemType`.

**Existing structure (lines 1-50 of the file are header + "Purpose" + "The three-bucket split"):** the file already has prose with `## Purpose` / `## The three-bucket split` headings; D-03 documentation slots in as a new `## Exclusion sections` (or `## Empty `excludeFieldsByItemType`` ) section near the top.

**Apply (D-03):** add a section that says:
- `excludeFieldsByItemType` is **empty by design** in the post-Phase-40 baseline — per-ItemType field exclusions can be added via the admin UI, but Swift 2.2 baseline ships without any.
- `ConfigWriter.Save` omits empty dicts (`WhenWritingNull` mapping at `ConfigWriter.cs:35-36`); the JSON file therefore does not contain the key at all. This is an intentional shape change from the pre-Phase-40 explicit `{}` (deleted at commit `c5d9a8c`).
- `excludeXmlElementsByType` was **expanded** during Phase 40 (commit `d57d474`) with 21 elements for `eCom_CartV2` and richer per-type lists for UserCreate / UserAuthentication / etc. — no restoration needed.

---

### `tests/DynamicWeb.Serializer.Tests/AdminUI/XmlTypeEditScreenTests.cs` (NEW — test, factory + assertion)

**Analog:** combination of two existing test classes — neither is an exact match (no per-screen rendering test exists in the AdminUI test suite today) but together they cover the full scaffold:

**Scaffold pattern from `XmlTypeCommandTests.cs:13-46` (tempdir + config-fixture, sibling type):**
```csharp
public class XmlTypeCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public XmlTypeCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "XmlTypeCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private void CreateSeedConfig(Dictionary<string, List<string>>? excludeXmlElementsByType = null)
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/", AreaId = 1, PageId = 10 }
            },
            ExcludeXmlElementsByType = excludeXmlElementsByType ?? new()
        };
        ConfigWriter.Save(config, _configPath);
    }

    private SerializerConfiguration LoadConfig() => ConfigLoader.Load(_configPath);
}
```

**Direct-public-method test pattern from `SerializerSettingsNodeProviderModeTreeTests.cs:62-77` (instantiate the production class and call its public methods, asserting on the returned model):**
```csharp
[Fact]
public void GetSubNodes_UnderSerializeNode_Returns_Predicates_ItemTypes_XmlTypes_LogViewer()
{
    WriteConfig();
    var provider = new SerializerSettingsNodeProvider();

    var rootChildren = provider
        .GetSubNodes(PathTo(SerializerSettingsNodeProvider.DeveloperRootId, SerializerSettingsNodeProvider.SerializeNodeId))
        .Select(n => n.Id)
        .ToList();

    Assert.Equal(4, rootChildren.Count);
    Assert.Contains(SerializerSettingsNodeProvider.PredicatesNodeId, rootChildren);
    // ...
}
```

**FakeSqlExecutor injection pattern from `XmlTypeDiscoveryTests.cs:13-27` (the `XmlTypeDiscovery` class already accepts `ISqlExecutor` in its ctor — testable):**
```csharp
[Fact]
public void DiscoverXmlTypes_ReturnsPageUrlDataProviders()
{
    var executor = new FakeSqlExecutor();
    executor.AddMapping("PageUrlDataProvider",
        TestTableHelper.CreateSingleColumnTable("PageUrlDataProvider", "TypeName"));
    executor.AddMapping("ParagraphModuleSystemName",
        TestTableHelper.CreateSingleColumnTable("ParagraphModuleSystemName"));

    var discovery = new XmlTypeDiscovery(executor);
    var types = discovery.DiscoverXmlTypes();

    Assert.Contains("TypeName", types);
}
```

**Apply (Wave 0 test scaffolds for D-05):** the new file should:
1. Inherit `IDisposable` (or `ConfigLoaderValidatorFixtureBase` if SqlTable predicates are involved — none are for D-05 tests, so plain `IDisposable` is sufficient — see `XmlTypeCommandTests` precedent).
2. Use the same tempdir + `_configPath` scaffold; rename the prefix to `XmlTypeEditScreenTests_`.
3. Assert on `XmlTypeEditScreen` outputs by **either** (a) refactoring `XmlTypeEditScreen` to take an `XmlTypeDiscovery` injected via property/constructor (matching the `ScanXmlTypesCommand.Discovery` pattern from `XmlTypeCommandTests.cs:69-73`), or (b) testing `CreateElementSelector` indirectly via reflection. RESEARCH.md line 660 calls out this trade-off and explicitly recommends extracting `XmlTypeDiscovery` as a screen-level injectable field for testability — the planner should land on (a).
4. Three tests: (i) discovery-empty + saved-non-empty → editor shows saved as Options, (ii) discovery-subset + saved-extra → union as Options, (iii) discovery-only → existing behavior unchanged.

---

### `tests/DynamicWeb.Serializer.Tests/AdminUI/ItemTypeEditScreenTests.cs` (NEW — test, factory + assertion)

**Analog:** `ItemTypeCommandTests.cs:10-46` for the tempdir + config-fixture scaffold (sibling Item-Type test class with identical shape to `XmlTypeCommandTests`):

```csharp
public class ItemTypeCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ItemTypeCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ItemTypeCmdTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Serializer.config.json");
    }
    // ...
    private void CreateSeedConfig(Dictionary<string, List<string>>? excludeFieldsByItemType = null)
    {
        var config = new SerializerConfiguration
        {
            OutputDirectory = @"\System\Serializer",
            LogLevel = "info",
            DryRun = false,
            Predicates = new List<ProviderPredicateDefinition>
            {
                new() { Name = "Default", Mode = DeploymentMode.Deploy, ProviderType = "Content", Path = "/", AreaId = 1 }
            },
            ExcludeFieldsByItemType = excludeFieldsByItemType ?? new()
        };
        ConfigWriter.Save(config, _configPath);
    }
}
```

**Apply (Wave 0 test scaffolds for D-06):** mirror the `XmlTypeEditScreenTests` shape but for `ItemTypeEditScreen.CreateFieldSelector`. The challenge here is that `ItemTypeEditScreen.CreateFieldSelector` calls `ItemManager.Metadata.GetItemFields(itemType)` directly with no injection seam — the planner must either (a) extract a metadata-source seam (preferred per RESEARCH.md line 660), or (b) test the merge-saved-into-discovered logic via a unit-tested helper extracted from the screen. The planner makes this call when planning the wave.

**Note:** D-06 may not need a separate test file if the planner extracts the merge logic into a shared helper — in which case both XmlTypeEditScreenTests and ItemTypeEditScreenTests collapse into `MergeSavedAndDiscoveredHelperTests.cs`. That's a planner-level decision.

---

## Shared Patterns

### Tree-node display label vs. internal const
**Source:** `SerializerSettingsNodeProvider.cs:23-29` (the const block) + `:62-103` (the `yield return new NavigationNode { Id, Name, ... }` pattern).
**Apply to:** D-01 rename — `Name` (display) and `Id` (const) are independent. Per D-02, only `Name` changes; `Id = ItemTypesNodeId = "Serializer_ItemTypes"` const is preserved.

### String-typed model property + string-Value Select binding
**Source:** `SerializerSettingsModel.LogLevel` (`= "info"`) + `SerializerSettingsModel.ConflictStrategy` (`= "source-wins"`) bound to `SerializerSettingsEditScreen.CreateLogLevelSelect` / `CreateConflictStrategySelect` (string-Value `ListOption`s).
**Apply to:** D-13 `PredicateEditModel.Mode` migration. The model property changes from `DeploymentMode` (enum) to `string`, defaulting to `nameof(DeploymentMode.Deploy)` so the runtime value remains a valid `Enum.Parse<DeploymentMode>` input.
```csharp
// Pattern (current LogLevel — the working precedent):
[ConfigurableProperty("Log Level", explanation: "Logging verbosity")]
public string LogLevel { get; set; } = "info";
```

### Enum.Parse on the persist side, .ToString() on the hydrate side
**Source:** `ConfigLoader` already uses `Enum.TryParse<DeploymentMode>(... ignoreCase: true)` to convert the JSON string back to an enum (per RESEARCH.md line 333 reference and security section). The admin-UI Save / Query pair must mirror this:
- **Save (`SavePredicateCommand` lines 161, 183):** `Mode = Enum.Parse<DeploymentMode>(Model.Mode, ignoreCase: true)`.
- **Hydrate (`PredicateByIndexQuery` line 32):** `Mode = pred.Mode.ToString()`.
Both ends respect the round-trip rule.

### SelectMultiDual merge-saved-into-discovered shape
**Source:** RESEARCH.md lines 256-292 and 429-482 — the proposed fix shape that replaces the early-return short-circuit in both `XmlTypeEditScreen.CreateElementSelector` and `ItemTypeEditScreen.CreateFieldSelector`.
**Apply to:** D-05 (XmlType) and D-06 (ItemType). Both use the **same** merge shape:
```
1. Build allElements/allFields HashSet up front
2. Try discovery, add into HashSet (catch -> editor.Explanation)
3. Parse Model.Excluded* into selected[]
4. foreach (selected) allElements.Add(...)  // union saved into discovered
5. If HashSet still empty -> set fallback Explanation, return editor
6. editor.Options = HashSet ordered + projected to ListOption
7. if (selected.Length > 0) editor.Value = selected
```

### Test-class scaffold (tempdir + ConfigWriter seed + Dispose-cleanup)
**Source:** `XmlTypeCommandTests` and `ItemTypeCommandTests` (both AdminUI test classes that round-trip configs through `ConfigLoader` / `ConfigWriter`).
**Apply to:** New `XmlTypeEditScreenTests.cs` and `ItemTypeEditScreenTests.cs`. Use plain `IDisposable` (the precedent for non-SqlTable AdminUI tests); only inherit `ConfigLoaderValidatorFixtureBase` if SqlTable predicates appear in the seed config (they don't for D-05/D-06 tests).

### `[ConfigurableProperty]` attribute conventions
**Source:** every model in `src/DynamicWeb.Serializer/AdminUI/Models/*.cs` uses `[ConfigurableProperty(label, explanation: "...")]` as the universal shape.
**Apply to:** D-12 `Mode` field — switch from `explanation:` to `hint:` per RESEARCH.md A1; the named-arg syntax is preserved. This is a one-line attribute change inside `PredicateEditModel.cs`.
**Existing single `hint:` precedent in production source:** `PredicateEditScreen.cs:99` — `SelectorBuilder.CreatePageSelector(... hint: "Select root page for this predicate")`. This confirms the `hint:` keyword is wired into at least one CoreUI builder; whether `[ConfigurableProperty(... hint: ...)]` produces a hover-tooltip vs. always-visible inline copy is the only piece RESEARCH.md flags as live-host-confirmation-only (A1).

---

## No Analog Found

None. Every Phase 41 modified file has a working precedent in itself or in a sibling. The two new test files (`XmlTypeEditScreenTests.cs`, `ItemTypeEditScreenTests.cs`) have no exact-shape precedent (no per-screen rendering test exists in the AdminUI test suite), but they compose two existing patterns — the `XmlTypeCommandTests` / `ItemTypeCommandTests` tempdir scaffold and the `SerializerSettingsNodeProviderModeTreeTests` direct-public-method-call shape. Per RESEARCH.md line 660, the planner should consider extracting `XmlTypeDiscovery` (and an analogous `ItemManager.Metadata` seam) to a screen-level injectable field as the cleanest path to making `CreateElementSelector` / `CreateFieldSelector` directly unit-testable.

---

## Metadata

**Analog search scope:**
- `src/DynamicWeb.Serializer/AdminUI/Tree/`
- `src/DynamicWeb.Serializer/AdminUI/Screens/`
- `src/DynamicWeb.Serializer/AdminUI/Models/`
- `src/DynamicWeb.Serializer/AdminUI/Commands/`
- `src/DynamicWeb.Serializer/AdminUI/Queries/`
- `src/DynamicWeb.Serializer/AdminUI/Infrastructure/`
- `src/DynamicWeb.Serializer/Configuration/DeploymentMode.cs`
- `tests/DynamicWeb.Serializer.Tests/AdminUI/`
- `tests/DynamicWeb.Serializer.Tests/TestHelpers/`
- `docs/baselines/Swift2.2-baseline.md`

**Files scanned (read in full or referenced):** 17 source files + 6 test files + 1 docs file = 24.

**Pattern extraction date:** 2026-05-01

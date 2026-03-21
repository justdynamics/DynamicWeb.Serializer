# Architecture Patterns

**Domain:** DynamicWeb 10 admin UI integration for ContentSync v1.2
**Researched:** 2026-03-21
**Confidence:** MEDIUM (DW10 CoreUI patterns verified via ExpressDelivery sample; content tree injection points unverified)

## Current Architecture (v1.0/v1.1)

```
ContentSync.config.json (JSON file on disk)
        |
        v
  ConfigLoader.Load()
        |
        v
  SyncConfiguration { OutputDirectory, LogLevel, Predicates[] }
        |
        +---> ContentSerializer(config) ---> FileSystemStore.WriteTree() ---> YAML on disk
        |
        +---> ContentDeserializer(config) ---> FileSystemStore.ReadTree() ---> DW database
        |
  Entry points:
        +---> SerializeScheduledTask.Run() ---> FindConfigFile() + ContentSerializer
        +---> DeserializeScheduledTask.Run() ---> FindConfigFile() + ContentDeserializer
```

### Existing Component Inventory

| Component | File | Responsibility |
|-----------|------|---------------|
| `SyncConfiguration` | Configuration/SyncConfiguration.cs | Immutable config record: OutputDirectory, LogLevel, Predicates |
| `PredicateDefinition` | Configuration/PredicateDefinition.cs | Single predicate: Name, Path, AreaId, Excludes |
| `ConfigLoader` | Configuration/ConfigLoader.cs | Reads JSON from disk, validates, returns SyncConfiguration |
| `ContentPredicate` | Configuration/ContentPredicate.cs | Evaluates include/exclude logic for a content path |
| `ContentPredicateSet` | Configuration/ContentPredicate.cs | OR-aggregation of multiple ContentPredicates |
| `ContentSerializer` | Serialization/ContentSerializer.cs | DW-to-disk pipeline: traverse, filter, map, write |
| `ContentDeserializer` | Serialization/ContentDeserializer.cs | Disk-to-DW pipeline: read, resolve GUIDs, write to DB |
| `ContentMapper` | Serialization/ContentMapper.cs | Maps DW entities to serialized DTOs |
| `ReferenceResolver` | Serialization/ReferenceResolver.cs | Resolves cross-references between entities |
| `FileSystemStore` | Infrastructure/FileSystemStore.cs | YAML I/O with mirror-tree layout |
| `IContentStore` | Infrastructure/IContentStore.cs | Abstraction over file I/O |
| `SerializeScheduledTask` | ScheduledTasks/SerializeScheduledTask.cs | DW scheduled task entry point for serialization |
| `DeserializeScheduledTask` | ScheduledTasks/DeserializeScheduledTask.cs | DW scheduled task entry point for deserialization |

### Key Architectural Properties

1. **Config is immutable records** -- `SyncConfiguration` and `PredicateDefinition` use `required init` properties. ConfigLoader creates them; nothing mutates them after construction.
2. **ContentSerializer/ContentDeserializer accept config via constructor** -- they do not find/load config themselves. This is the property that enables reuse from context menu actions.
3. **Scheduled tasks own config discovery** -- `FindConfigFile()` is duplicated in both tasks with identical 4-path search logic.
4. **No shared config path resolution** -- each entry point finds config independently.

## Recommended Architecture (v1.2)

### Design Principle: Config File Stays Source of Truth

The admin UI is a management layer over `ContentSync.config.json`. The UI reads and writes the same JSON file. Manual edits to the file remain valid. This means:

- No database tables for configuration (unlike the ExpressDelivery sample which uses SQL tables).
- No ORM, no UpdateProvider, no SQL migrations.
- Commands read from disk, mutate in memory, write back to disk.
- Queries read from disk and map to data models.

### New Component Map

```
DW Admin UI (Settings > Content > Sync)
  |
  +--- Tree/ContentSyncSettingsNodeProvider     [NEW]
  |      NavigationNodeProvider<AreasSection> under Settings
  |      Root node: "Sync" -> ContentSyncSettingsScreen
  |      Sub-node: "Predicates" -> ContentSyncPredicateListScreen
  |
  +--- Screens/ContentSyncSettingsScreen        [NEW]
  |      EditScreenBase<ContentSyncSettingsDataModel>
  |      Fields: OutputDirectory, LogLevel
  |      Save -> SaveContentSyncSettingsCommand
  |
  +--- Screens/ContentSyncPredicateListScreen   [NEW]
  |      ListScreenBase<ContentSyncPredicateDataModel>
  |      Lists predicates from config file
  |      Context actions: Edit, Delete
  |      Create action -> ContentSyncPredicateEditScreen
  |
  +--- Screens/ContentSyncPredicateEditScreen   [NEW]
  |      EditScreenBase<ContentSyncPredicateDataModel>
  |      Fields: Name, Path, AreaId, Excludes
  |      Save -> SaveContentSyncPredicateCommand
  |
  +--- Models/ContentSyncSettingsDataModel      [NEW]
  |      DataViewModelBase with config fields
  |
  +--- Models/ContentSyncPredicateDataModel     [NEW]
  |      DataViewModelBase, IIdentifiable with predicate fields
  |
  +--- Queries/ContentSyncSettingsQuery         [NEW]
  |      DataQueryModelBase -> reads config file -> maps to settings model
  |
  +--- Queries/ContentSyncPredicatesQuery       [NEW]
  |      DataQueryListBase -> reads config file -> maps to predicate model list
  |
  +--- Queries/ContentSyncPredicateByIndexQuery [NEW]
  |      DataQueryModelBase -> reads config file -> returns single predicate
  |
  +--- Commands/SaveContentSyncSettingsCommand  [NEW]
  |      Reads config, updates settings fields, writes back
  |
  +--- Commands/SaveContentSyncPredicateCommand [NEW]
  |      Reads config, updates/adds predicate, writes back
  |
  +--- Commands/DeleteContentSyncPredicateCommand [NEW]
  |      Reads config, removes predicate by index, writes back
  |
  +--- Configuration/ConfigWriter               [NEW]
  |      Writes SyncConfiguration back to JSON file
  |      Counterpart to ConfigLoader (read vs write)
  |
  +--- Configuration/ConfigPathResolver          [NEW]
  |      Extracts FindConfigFile() logic from scheduled tasks
  |      Single source of truth for config file location

Content Tree Context Menus
  |
  +--- Injectors/PageOverviewInjector           [NEW]
  |      ScreenInjector for page overview/edit screen
  |      Adds "Serialize to Zip" and "Deserialize from Zip" context actions
  |
  +--- Commands/SerializeToZipCommand           [NEW]
  |      Takes page ID, creates temp SyncConfiguration with single predicate,
  |      runs ContentSerializer to temp dir, zips, returns download
  |
  +--- Commands/DeserializeFromZipCommand       [NEW]
  |      Takes uploaded zip + target page ID, extracts to temp dir,
  |      runs ContentDeserializer with temp config, cleans up
  |
  +--- Screens/DeserializeUploadScreen          [NEW]
  |      Modal/dialog for zip upload + target selection
```

### Component Boundaries

| Component | Responsibility | Communicates With |
|-----------|---------------|-------------------|
| ConfigPathResolver | Finds ContentSync.config.json on disk | ConfigLoader, ConfigWriter, all commands/queries |
| ConfigWriter | Serializes SyncConfiguration back to JSON | Commands that modify config |
| ContentSyncSettingsNodeProvider | Registers tree node under Settings | DW navigation system |
| Settings/Predicate Screens | UI rendering | Queries (read), Commands (save) |
| Settings/Predicate Queries | Read config file, map to data models | ConfigPathResolver, ConfigLoader |
| Settings/Predicate Commands | Read config, mutate, write back | ConfigPathResolver, ConfigLoader, ConfigWriter |
| PageOverviewInjector | Adds context menu actions to page tree | DW page overview screen |
| SerializeToZipCommand | Ad-hoc serialize + zip | ContentSerializer (existing), System.IO.Compression |
| DeserializeFromZipCommand | Unzip + ad-hoc deserialize | ContentDeserializer (existing), System.IO.Compression |

### Data Flow

#### Settings Screen Save Flow

```
User edits settings in admin UI
  -> SaveContentSyncSettingsCommand.Handle()
    -> ConfigPathResolver.FindConfigFile()
    -> ConfigLoader.Load(path)            // read current state
    -> Merge model changes into config    // mutate in memory
    -> ConfigWriter.Write(path, config)   // write back to disk
    -> return CommandResult.Ok
```

#### Predicate List Flow

```
User navigates to Settings > Content > Sync > Predicates
  -> ContentSyncPredicatesQuery.GetListItems()
    -> ConfigPathResolver.FindConfigFile()
    -> ConfigLoader.Load(path)
    -> Map each PredicateDefinition to ContentSyncPredicateDataModel
    -> Return list
```

#### Context Menu Serialize Flow (Key Integration Answer)

```
User right-clicks page in content tree -> "Serialize to Zip"
  -> SerializeToZipCommand.Handle()
    -> Get page by ID from DW
    -> Build page's content path (walk parent chain)
    -> Create temp SyncConfiguration:
         OutputDirectory = Path.GetTempPath() + guid
         Predicates = [{ Name="Ad-hoc", Path=contentPath, AreaId=page.AreaId }]
    -> new ContentSerializer(tempConfig).Serialize()
    -> ZipFile.CreateFromDirectory(tempOutputDir, zipPath)
    -> Return zip as file download
    -> Clean up temp dir
```

**This is the critical design insight:** context menu actions reuse ContentSerializer/ContentDeserializer by constructing a temporary SyncConfiguration. No code duplication needed. The serializer/deserializer already accept config via constructor and are agnostic to how that config was created.

#### Context Menu Deserialize Flow

```
User right-clicks page in content tree -> "Deserialize from Zip"
  -> Opens DeserializeUploadScreen (modal dialog)
  -> User uploads zip, confirms target
  -> DeserializeFromZipCommand.Handle()
    -> Extract zip to temp dir
    -> Create temp SyncConfiguration:
         OutputDirectory = tempExtractDir
         Predicates = [{ Name="Ad-hoc", Path="/", AreaId=targetAreaId }]
    -> new ContentDeserializer(tempConfig).Deserialize()
    -> Return DeserializeResult summary as toast/notification
    -> Clean up temp dir
```

## Existing Components That Change

### ConfigLoader (NO STRUCTURAL CHANGE)

No structural changes needed. A separate `ConfigWriter` class handles the write path (single responsibility). The `RawSyncConfiguration` inner class stays private -- ConfigWriter can use `System.Text.Json` directly since it controls the output format.

### SyncConfiguration (MINOR CHANGE)

Consider whether to add fields that the settings UI manages but that are not currently in the config:
- `DryRun` -- currently passed as constructor arg to ContentDeserializer. This is a runtime flag, NOT a config file setting. Do NOT add it to SyncConfiguration.
- `ConflictStrategy` -- future extensibility. Not needed for v1.2 since source-wins is the only strategy.

**Recommendation:** Keep SyncConfiguration unchanged for v1.2. The UI shows all editable config fields; runtime-only flags like DryRun are handled at invocation time by the scheduled tasks.

### SerializeScheduledTask / DeserializeScheduledTask (MINOR CHANGE)

Extract `FindConfigFile()` into shared `ConfigPathResolver`. Both tasks currently duplicate this 4-path search logic identically. The new UI queries and commands also need the same path resolution.

```csharp
// Before (duplicated in both tasks):
private string? FindConfigFile() { ... }

// After:
var configPath = ConfigPathResolver.FindConfigFile();
```

### ContentSerializer / ContentDeserializer (NO CHANGE)

These components are already designed correctly for reuse:
- Accept `SyncConfiguration` via constructor (no hard dependency on file discovery)
- Accept optional `Action<string>? log` callback
- ContentDeserializer accepts `isDryRun` and `filesRoot` as constructor parameters

No modifications needed. Context menu actions construct temp configs and pass them in.

## New Components Detail

### Must Build (Core Infrastructure)

| Component | Type | Purpose | Dependencies |
|-----------|------|---------|--------------|
| `ConfigPathResolver` | Static class | Shared config file path discovery | None (pure file system) |
| `ConfigWriter` | Static class | Write SyncConfiguration to JSON | System.Text.Json |

### Must Build (Settings UI)

| Component | Type | Purpose | Dependencies |
|-----------|------|---------|--------------|
| `ContentSyncSettingsNodeProvider` | NavigationNodeProvider | Tree node registration | Dynamicweb.Application.UI, Dynamicweb.CoreUI |
| `ContentSyncNavigationNodePathProvider` | NavigationNodePathProvider | Breadcrumb path | Dynamicweb.Application.UI |
| `ContentSyncSettingsDataModel` | DataViewModelBase | Settings form model | Dynamicweb.CoreUI.Data |
| `ContentSyncPredicateDataModel` | DataViewModelBase, IIdentifiable | Predicate list/edit model | Dynamicweb.CoreUI.Data |
| `ContentSyncSettingsQuery` | DataQueryModelBase | Read settings from config | ConfigPathResolver, ConfigLoader |
| `ContentSyncPredicatesQuery` | DataQueryListBase | List predicates from config | ConfigPathResolver, ConfigLoader |
| `ContentSyncPredicateByIndexQuery` | DataQueryModelBase | Single predicate for edit | ConfigPathResolver, ConfigLoader |
| `ContentSyncSettingsScreen` | EditScreenBase | Settings edit form | Query + Command |
| `ContentSyncPredicateListScreen` | ListScreenBase | Predicate list with CRUD | Query + Commands |
| `ContentSyncPredicateEditScreen` | EditScreenBase | Predicate edit form | Query + Command |
| `SaveContentSyncSettingsCommand` | CommandBase | Persist settings changes | ConfigPathResolver, ConfigLoader, ConfigWriter |
| `SaveContentSyncPredicateCommand` | CommandBase | Add/update predicate | ConfigPathResolver, ConfigLoader, ConfigWriter |
| `DeleteContentSyncPredicateCommand` | CommandBase | Remove predicate | ConfigPathResolver, ConfigLoader, ConfigWriter |

### Must Build (Context Menu Actions)

| Component | Type | Purpose | Dependencies |
|-----------|------|---------|--------------|
| `PageOverviewInjector` | ScreenInjector | Add context menu to page tree | Dynamicweb.CoreUI, DW page screen type |
| `SerializeToZipCommand` | CommandBase | Serialize page subtree to zip | ContentSerializer, System.IO.Compression |
| `DeserializeFromZipCommand` | CommandBase | Upload zip and deserialize | ContentDeserializer, System.IO.Compression |
| `DeserializeUploadScreen` | Screen (modal) | Upload dialog with target picker | CoreUI Screens |

### NuGet Dependencies to Add

The current project references `Dynamicweb Version="10.23.9"`. The admin UI features require additional packages:

| Package | Purpose | Confidence |
|---------|---------|------------|
| `Dynamicweb.CoreUI` | Screens, Commands, Queries, Actions | HIGH -- used by ExpressDelivery sample |
| `Dynamicweb.Application.UI` | NavigationNodeProvider, AreasSection, section types | HIGH -- used by ExpressDelivery sample |

**Alternative:** Use `Dynamicweb.Suite.Ring1 Version="*"` like the ExpressDelivery sample (meta-package including all DW10 UI packages). This avoids version mismatch issues but pulls in unnecessary dependencies.

**Recommendation:** Start with `Dynamicweb.Suite.Ring1 Version="*"` for development to avoid hunting for individual packages. Narrow down to specific packages once the build works. The project SDK may need to change from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Razor` if any Razor views are needed for custom widgets (unlikely for pure CoreUI screens).

**Confidence: MEDIUM** -- the exact set of individual packages needs runtime verification.

## Patterns to Follow

### Pattern 1: File-Backed Commands (Read-Modify-Write)

All config-modifying commands follow the same pattern because config lives in a JSON file, not a database.

**What:** Read the current config file, apply changes in memory, write back.
**When:** Any command that modifies ContentSync settings or predicates.

```csharp
public class SaveContentSyncSettingsCommand : CommandBase<ContentSyncSettingsDataModel>
{
    public override CommandResult Handle()
    {
        if (Model is null) return new() { Status = CommandResult.ResultType.Invalid };

        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath is null) return new()
        {
            Status = CommandResult.ResultType.Error,
            Message = "ContentSync.config.json not found"
        };

        var config = ConfigLoader.Load(configPath);

        var updated = config with
        {
            OutputDirectory = Model.OutputDirectory ?? config.OutputDirectory,
            LogLevel = Model.LogLevel ?? config.LogLevel
        };

        ConfigWriter.Write(configPath, updated);

        return new() { Status = CommandResult.ResultType.Ok, Model = Model };
    }
}
```

### Pattern 2: Temp Config for Ad-Hoc Operations

**What:** Create a throwaway SyncConfiguration to reuse ContentSerializer/ContentDeserializer for one-off operations.
**When:** Context menu serialize/deserialize actions.

```csharp
var tempDir = Path.Combine(Path.GetTempPath(), $"ContentSync_{Guid.NewGuid():N}");
var tempConfig = new SyncConfiguration
{
    OutputDirectory = tempDir,
    Predicates = new List<PredicateDefinition>
    {
        new() { Name = "Ad-hoc", Path = contentPath, AreaId = areaId }
    }
};

try
{
    var serializer = new ContentSerializer(tempConfig);
    serializer.Serialize();
    ZipFile.CreateFromDirectory(tempDir, zipPath);
    // Return zip to browser...
}
finally
{
    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
}
```

### Pattern 3: Index-Based Predicate Identity

Predicates in the config file have no unique ID (no database, no auto-increment). Use array index as the identifier for edit/delete operations.

**What:** Use the predicate's position in the `Predicates[]` array as its temporary identity.
**When:** Predicate list/edit/delete operations.

```csharp
public sealed class ContentSyncPredicateDataModel : DataViewModelBase, IIdentifiable
{
    public int Index { get; set; }  // Position in config array (0-based)

    [ConfigurableProperty("Name")]
    public string? Name { get; set; }

    [ConfigurableProperty("Content Path")]
    public string? Path { get; set; }

    [ConfigurableProperty("Area ID")]
    public int AreaId { get; set; }

    public string GetId() => $"{Index}";
}
```

### Pattern 4: NavigationNodeProvider for Settings Tree

Following the ExpressDelivery sample pattern.

**What:** Register a node in the Settings tree with sub-nodes.
**When:** Building the admin UI tree node.

```csharp
public sealed class ContentSyncSettingsNodeProvider : NavigationNodeProvider<AreasSection>
{
    public override IEnumerable<NavigationNode> GetRootNodes()
    {
        yield return new()
        {
            Id = "ContentSync",
            Name = "Content Sync",
            NodeAction = NavigateScreenAction.To<ContentSyncSettingsScreen>()
                .With(new ContentSyncSettingsQuery()),
            HasSubNodes = true
        };
    }

    public override IEnumerable<NavigationNode> GetSubNodes(NavigationNodePath parentNodePath)
    {
        if (parentNodePath.Last == "ContentSync")
        {
            yield return new()
            {
                Id = "ContentSync_Predicates",
                Name = "Predicates",
                NodeAction = NavigateScreenAction.To<ContentSyncPredicateListScreen>()
                    .With(new ContentSyncPredicatesQuery())
            };
        }
    }
}
```

**Confidence: MEDIUM** -- `AreasSection` places the node under Settings. The requirement says "Settings > Content > Sync" which may need a more specific section type. The exact parent node for the Content sub-section needs runtime discovery. The ExpressDelivery sample uses `AreasSection` which maps to a top-level Settings section node.

### Pattern 5: ScreenInjector for Context Menu Actions

Based on the ExpressDelivery `OrderOverviewInjector` pattern.

**What:** Inject context actions into an existing DW screen.
**When:** Adding serialize/deserialize actions to the page tree.

```csharp
public sealed class PageOverviewInjector : ScreenInjector</* DW page screen type */>
{
    public override void OnAfter(/* screen */, UiComponentBase content)
    {
        if (content.Get<ScreenLayout>() is not ScreenLayout layout) return;
        // Extract page ID from screen model

        layout.ContextActionGroups.Add(new()
        {
            Nodes = new List<ActionNode>
            {
                new()
                {
                    Name = "Serialize to Zip",
                    Icon = Icon.Download,
                    NodeAction = RunCommandAction
                        .For(new SerializeToZipCommand { PageId = pageId })
                },
                new()
                {
                    Name = "Deserialize from Zip",
                    Icon = Icon.Upload,
                    NodeAction = OpenDialogAction
                        .To<DeserializeUploadScreen>()
                        .With(new DeserializeTargetQuery { PageId = pageId })
                }
            }
        });
    }
}
```

**Confidence: LOW** -- the DW page screen type to inject into is unknown. The exact type name needs discovery by inspecting loaded DW assemblies at runtime. The pattern is sound (proven by ExpressDelivery) but the target screen type is a guess.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Database Tables for Config

**What:** Creating SQL tables and UpdateProvider for ContentSync configuration.
**Why bad:** Config file is explicitly the source of truth. Adding a database layer creates two sources of truth, breaks manual-edit compatibility, and complicates deployment.
**Instead:** Read/write the JSON file directly. Commands do read-modify-write on the file.

### Anti-Pattern 2: Duplicating Serialize/Deserialize Logic in Context Menu Commands

**What:** Writing separate serialization code in context menu commands.
**Why bad:** Creates divergence from scheduled task behavior. Bug fixes in one path don't apply to the other.
**Instead:** Construct a temporary `SyncConfiguration` and pass it to the existing `ContentSerializer`/`ContentDeserializer`. They already accept config via constructor injection.

### Anti-Pattern 3: Guessing DW UI Class Names Without Verification

**What:** Assuming `AreasSection`, `PageOverviewScreen`, or other DW internal types exist and work as expected.
**Why bad:** DW10 API documentation is sparse. Class names may differ from assumptions.
**Instead:** Build the settings node provider first and verify it appears in the DW admin tree before building the full screen stack. Inspect loaded DW assemblies at runtime to discover correct types.

### Anti-Pattern 4: Concurrent Config File Access Without Awareness

**What:** Multiple admin users editing config simultaneously causing read-modify-write conflicts.
**Why bad:** File-based config has no locking mechanism. Two simultaneous saves could lose changes.
**Instead:** Accept this limitation for v1.2 (config is rarely edited concurrently). Document it. If it becomes a problem, add file locking in ConfigWriter.

## Suggested Build Order

Build order is driven by dependency chains and risk reduction.

### Phase 1: Infrastructure + Settings Node (Lowest Risk, Unblocks Everything)

**Build:**
1. `ConfigPathResolver` -- extract from scheduled tasks, shared utility
2. `ConfigWriter` -- counterpart to ConfigLoader
3. `ContentSyncSettingsNodeProvider` -- verify tree node appears in DW admin
4. `ContentSyncNavigationNodePathProvider` -- breadcrumb support

**Rationale:** ConfigPathResolver and ConfigWriter are pure infrastructure with no DW UI dependencies. The node provider is the riskiest unknown (correct section type, correct parent node ID). Building it first surfaces integration issues early.

**Modifies:** `SerializeScheduledTask`, `DeserializeScheduledTask` (replace inline FindConfigFile with ConfigPathResolver)

**Validates:** DW admin tree registration works, correct NuGet packages identified.

### Phase 2: Settings Screen (Foundation for Predicate UI)

**Build:**
1. `ContentSyncSettingsDataModel` -- data model for settings form
2. `ContentSyncSettingsQuery` -- reads config, maps to model
3. `SaveContentSyncSettingsCommand` -- read-modify-write pattern
4. `ContentSyncSettingsScreen` -- edit screen with OutputDirectory and LogLevel fields

**Rationale:** Settings screen is simpler than predicate list (single object, not a collection). Establishes the full screen/query/command pattern that predicate screens will follow.

### Phase 3: Predicate Management (CRUD List)

**Build:**
1. `ContentSyncPredicateDataModel` -- with index-based identity
2. `ContentSyncPredicatesQuery` -- list from config
3. `ContentSyncPredicateByIndexQuery` -- single predicate for edit
4. `SaveContentSyncPredicateCommand` -- add/update predicate in array
5. `DeleteContentSyncPredicateCommand` -- remove by index
6. `ContentSyncPredicateListScreen` -- list with context actions (edit, delete)
7. `ContentSyncPredicateEditScreen` -- edit form for name, path, areaId, excludes

**Rationale:** Depends on Phase 2 patterns being validated. More complex because it manages a collection with index-based identity rather than a single settings object.

### Phase 4: Context Menu Actions (Highest Complexity, Most Risk)

**Build:**
1. `PageOverviewInjector` -- inject context menu into page tree
2. `SerializeToZipCommand` -- temp config + ContentSerializer + zip
3. `DeserializeFromZipCommand` -- unzip + temp config + ContentDeserializer
4. `DeserializeUploadScreen` -- file upload modal

**Rationale:** Highest DW API risk. Three open questions that can only be answered at runtime:
- What DW screen type represents the page overview for injection?
- How does a DW command return a file download to the browser?
- How does a DW CoreUI modal accept file uploads?

Built last because it depends on nothing from Phases 2-3 but benefits from accumulated DW UI knowledge.

### Phase Dependency Graph

```
Phase 1: Infrastructure + Node
    |
    +---> Phase 2: Settings Screen
    |         |
    |         +---> Phase 3: Predicate CRUD
    |
    +---> Phase 4: Context Menu Actions
              (independent of 2/3 but benefits from experience)
```

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Config read-modify-write pattern | HIGH | Pure file I/O, no DW dependencies, well understood |
| NavigationNodeProvider registration | MEDIUM | ExpressDelivery sample shows pattern; exact section type for Settings > Content needs runtime check |
| Screen/Query/Command pattern | HIGH | ExpressDelivery sample provides complete working examples |
| Context menu injection on pages | LOW | No sample code for page tree injection. ScreenInjector works for OrderOverview but page tree may differ |
| File download from commands | LOW | No examples found. DW may require endpoint handler or custom action type |
| File upload in modals | LOW | No examples found. May need custom Razor view or DW file picker integration |
| Correct NuGet package set | MEDIUM | Suite.Ring1 meta-package works; individual packages need verification |

## Sources

- ExpressDelivery sample app: `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\` -- verified NavigationNodeProvider, Commands, Queries, Screens, ScreenInjector patterns
- [DW10 Developer Documentation - App Store App Guide](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) -- NavigationNodeProvider, EditScreen, Query, Command examples
- [DW10 Screen Types Documentation](https://doc.dynamicweb.dev/documentation/extending/administration-ui/screentypes.html) -- screen types overview
- [Create custom setting node in Settings section](https://doc.dynamicweb.com/Default.aspx?ID=1057&PID=1605&ThreadID=69395) -- community reference
- Existing ContentSync codebase: `C:\VibeCode\Dynamicweb.ContentSync\src\Dynamicweb.ContentSync\` -- verified component signatures and constructor patterns

---
*Architecture research for: DynamicWeb 10 admin UI integration for ContentSync v1.2*
*Researched: 2026-03-21*

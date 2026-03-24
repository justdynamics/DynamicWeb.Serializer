# Phase 16: Admin UX - Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Full project rename from Dynamicweb.ContentSync to DynamicWeb.Serializer (Wave 1), then Admin UX improvements: log viewer with guided advice, deserialize-from-zip in asset management, tree node relocation, and scheduled task deprecation (Wave 2). REN-01 pulled forward from Phase 17 to avoid double-touching new code.

</domain>

<decisions>
## Implementation Decisions

### Project Rename (Wave 1 — do first)
- **D-01:** Full rename: namespace `Dynamicweb.ContentSync` → `DynamicWeb.Serializer`, assembly `DynamicWeb.Serializer.dll`, NuGet package `DynamicWeb.Serializer`
- **D-02:** Note the casing: `DynamicWeb` (capital W) — matches the PROJECT.md milestone name, NOT DW's own lowercase convention
- **D-03:** Rename covers: namespaces, assembly name, csproj, NuGet metadata, test project, all `using` statements, config file references
- **D-04:** API command names change: `ContentSyncSerialize` → `SerializerSerialize`, `ContentSyncDeserialize` → `SerializerDeserialize` (no backward-compat aliases needed)
- **D-05:** This is Wave 1 — lands and stabilizes before any UX work begins in Wave 2

### Tree Node Relocation (Wave 2)
- **D-06:** Move admin tree from Settings > Content > Sync to Settings > Database > Serialize
- **D-07:** User-facing label: "Serialize" (not "Serializer" or "Database Sync")
- **D-08:** Change `SyncSettingsNodeProvider` parent from `Content_Settings` to `Settings_Database`
- **D-09:** Update `SyncNavigationNodePathProvider` and `PredicateNavigationNodePathProvider` breadcrumbs to route through `SystemSection` / `Settings_Database`
- **D-10:** Screen title changes from "Content Sync Settings" to "Serialize Settings"

### Log Viewer (Wave 2)
- **D-11:** Separate log files per run, named with operation type + timestamp: `Serialize_2026-03-24_143052.log` / `Deserialize_2026-03-24_143052.log`
- **D-12:** Log file content: existing timestamped text format with a structured JSON summary block prepended at the top — viewer parses the header, humans can read the rest
- **D-13:** Log viewer lives as a sub-node under Serialize: Settings > Database > Serialize > Log Viewer
- **D-14:** Viewer shows a dropdown/list of available log files, always starts with the latest run selected
- **D-15:** Guided advice = error-specific suggestions, not just summaries. E.g., "FK constraint failed on EcomOrderStates — run OrderFlows predicate first" or "Missing group: Webshop1 — create it in Settings > Ecommerce"
- **D-16:** No auto-cleanup — log files accumulate, user can delete from disk if needed
- **D-17:** Data source: parse log files from disk (no in-memory OrchestratorResult capture needed)

### Deserialize from Asset Management (Wave 2)
- **D-18:** Inject "Import to database" action on `FileOverviewScreen` for .zip files — gated to zips in the configured output directory only
- **D-19:** Flow: click action → auto dry-run → show per-table breakdown (each predicate: N new, N updated, N skipped) → user confirms → execute → redirect to log viewer
- **D-20:** Zip extracted to temp directory, cleaned up after deserialization completes
- **D-21:** If zip contains no valid YAML files matching configured predicates, fail fast: "This zip doesn't contain valid serialization data. Expected YAML files matching configured predicates."
- **D-22:** Injector class: `SerializerFileOverviewInjector : EditScreenInjector<FileOverviewScreen, FileDataModel>`, gated on `Model?.Extension == ".zip"` AND file is in output directory

### Scheduled Task Deprecation (Wave 2)
- **D-23:** Scheduled tasks already deleted (commit `a32703f`). Only remaining work: update README to document API commands as the replacement, formally close UX-04.

### Claude's Discretion
- Log viewer screen layout and styling within DW admin conventions
- Advice rule catalog — determine based on common error patterns during implementation
- Exact JSON structure for the log file summary header
- How to detect "output directory" for the zip file path check in the injector
- Test strategy for the new screens and injector

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing Admin UI (pattern reference)
- `src/Dynamicweb.ContentSync/AdminUI/Tree/SyncSettingsNodeProvider.cs` — Current tree node registration, parent ID to change
- `src/Dynamicweb.ContentSync/AdminUI/Tree/SyncNavigationNodePathProvider.cs` — Breadcrumb paths to update
- `src/Dynamicweb.ContentSync/AdminUI/Tree/PredicateNavigationNodePathProvider.cs` — Predicate breadcrumbs to update
- `src/Dynamicweb.ContentSync/AdminUI/Injectors/ContentSyncPageListInjector.cs` — EditScreenInjector pattern to reuse for UX-02
- `src/Dynamicweb.ContentSync/AdminUI/Screens/SyncSettingsEditScreen.cs` — Settings screen to retitle
- `src/Dynamicweb.ContentSync/AdminUI/Commands/ContentSyncSerializeCommand.cs` — API command to rename
- `src/Dynamicweb.ContentSync/AdminUI/Commands/ContentSyncDeserializeCommand.cs` — API command to rename

### DW10 Source (asset management integration)
- `C:\Projects\temp\dw10source\Dynamicweb.Files.UI\Screens\Files\FileOverviewScreen.cs` — Target screen for injector
- `C:\Projects\temp\dw10source\Dynamicweb.Files.UI\Models\FileDataModel.cs` — Model with Extension, FilePath properties
- `C:\Projects\temp\dw10source\Dynamicweb.CoreUI\Screens\EditScreenInjector.cs` — Base class for screen action injection

### DW10 Source (tree node relocation)
- `C:\Projects\temp\dw10source\src\Dynamicweb.UI\Screens\Settings\SystemSection\SystemNodeProvider.cs` — `Settings_Database` node ID

### Structured Results (log viewer data)
- `src/Dynamicweb.ContentSync/Providers/SerializerOrchestrator.cs` — OrchestratorResult with per-provider counts
- `src/Dynamicweb.ContentSync/Providers/ProviderDeserializeResult.cs` — Created/Updated/Skipped/Failed per table

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **ContentSyncPageListInjector**: Exact pattern for the new FileOverviewScreen injector — override `GetScreenActions()`, return `ActionGroup` with action node
- **OrchestratorResult / ProviderDeserializeResult**: Structured data for the JSON log header — already has per-table counts and errors
- **SyncConfiguration.EnsureDirectories()**: Resolves output directory path — reuse for zip path validation
- **SerializerOrchestrator**: Already handles serialize/deserialize dispatch — can be called from the new zip deserialize action

### Established Patterns
- **DW Admin tree**: `NavigationTreeNodeProvider` + `NavigationNodePathProvider` pair for tree nodes and breadcrumbs
- **DW EditScreenInjector**: Generic `EditScreenInjector<TScreen, TModel>` for injecting actions into detail screens
- **API Commands**: `CommandBase<TModel>` pattern with `Execute()` method — rename is mechanical

### Integration Points
- **Log files**: Currently written in API commands via `File.AppendAllText` — needs refactoring to per-run file creation
- **Zip extraction**: `System.IO.Compression.ZipFile.ExtractToDirectory()` — already referenced in existing `ContentSyncDeserializeCommand`
- **Output directory config**: `SyncConfiguration.OutputDirectory` — gates which zips show the action

</code_context>

<deferred>
## Deferred Ideas

- Settings & Schema providers — future milestone scope
- Users, Marketing, PIM, Apps tables — future milestone scope
- DataGroup auto-discovery — future, enumerate available tables from DW metadata
- Batch predicate config (one predicate = all tables in a DataGroup) — future UX improvement

</deferred>

---

*Phase: 16-admin-ux*
*Context gathered: 2026-03-24*

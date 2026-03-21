# Phase 7: Config Infrastructure + Settings Tree Node - Context

**Gathered:** 2026-03-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Concurrency-safe config file read/write and Sync navigation node visible in DW admin tree at Settings > Content > Sync. This phase builds the infrastructure that Phases 8-10 depend on. The settings edit screen fields are Phase 8 scope — this phase registers the node and provides a placeholder or minimal screen.

</domain>

<decisions>
## Implementation Decisions

### Config Writer
- **D-01:** Add a ConfigWriter companion to ConfigLoader — writes SyncConfiguration back to JSON with same format/casing as the original
- **D-02:** Simple file-level read/write — no ReaderWriterLockSlim or heavy concurrency machinery. This is not a high-contention scenario.
- **D-03:** Atomic write via temp-file-then-rename to prevent corruption on crash mid-write
- **D-04:** ConfigLoader already validates on read — writer should produce valid JSON that passes the same validation

### Config File Location
- **D-05:** UI uses the same FindConfigFile() discovery logic as scheduled tasks — single source of truth for config path
- **D-06:** If no config file exists yet, create at the first candidate path (BaseDirectory/ContentSync.config.json) with sensible defaults

### Tree Node Registration
- **D-07:** Sync node appears under Settings > Content using NavigationNodeProvider pattern from ExpressDelivery sample
- **D-08:** Clicking Sync node navigates directly to the settings edit screen (Phase 8 populates the actual fields; this phase provides the skeleton screen)
- **D-09:** Predicates sub-node registered under Sync with HasSubNodes=true on the parent (Phase 9 builds the predicate screens)

### UI Refresh
- **D-10:** Settings screen always reads fresh from config file on load — no caching. Manual edits reflected immediately on next screen open.

### Claude's Discretion
- Exact JSON serialization options (indentation, property naming)
- Whether to use System.Text.Json or Newtonsoft for writing (ConfigLoader already uses System.Text.Json)
- NavigationNodePathProvider breadcrumb implementation
- Placeholder screen design before Phase 8 fills it in

</decisions>

<specifics>
## Specific Ideas

- DW10 source at `C:\Projects\temp\dw10source\` is available for understanding how existing DW UI features work (popups, toast, uploads, downloads, etc.)
- `C:\VibeCode\DynamicWeb.AIDiagnoser\` contains pitfall learnings from building UI within the DW admin — consult before making UI integration decisions
- ExpressDelivery sample at `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\` is the primary reference for NavigationNodeProvider, screens, CQRS patterns

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DW Extension Patterns
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Tree\SettingsNodeProvider.cs` — NavigationNodeProvider<AreasSection> pattern for tree registration
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Tree\ExpressDeliveryNavigationNodePathProvider.cs` — Breadcrumb/path provider
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\Screens\ExpressDeliveryPresetEditScreen.cs` — EditScreenBase pattern
- `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\ExpressDelivery\ExpressDelivery.csproj` — Project structure and NuGet references

### Existing Config System
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` — Current read-only config loading with validation
- `src/Dynamicweb.ContentSync/Configuration/SyncConfiguration.cs` — Config record model (OutputDirectory, LogLevel, Predicates)
- `src/Dynamicweb.ContentSync/Configuration/PredicateDefinition.cs` — Predicate record model
- `src/Dynamicweb.ContentSync/ScheduledTasks/SerializeScheduledTask.cs` — FindConfigFile() path discovery logic

### DW Admin UI Pitfalls
- `C:\VibeCode\DynamicWeb.AIDiagnoser\` — Prior project with DW admin UI integration learnings

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ConfigLoader.Load()`: JSON deserialization + validation pipeline — writer should mirror this format
- `FindConfigFile()` in scheduled tasks: 4-path config discovery — reuse for UI config location
- `SyncConfiguration` record: already the correct shape for round-trip serialization

### Established Patterns
- System.Text.Json with case-insensitive parsing (ConfigLoader) — writer should match
- Raw nullable model → validated record pattern (ConfigLoader) — writer reverses this
- Static class pattern for config utilities (ConfigLoader is static)

### Integration Points
- ConfigWriter will be called by Phase 8 SaveCommand and Phase 9 predicate commands
- Tree node registration happens via assembly scanning — just implement the provider class
- NavigationNodeProvider<AreasSection> — may need to verify if ContentSection or similar is more appropriate for Settings > Content placement

</code_context>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 07-config-infrastructure-settings-tree-node*
*Context gathered: 2026-03-21*

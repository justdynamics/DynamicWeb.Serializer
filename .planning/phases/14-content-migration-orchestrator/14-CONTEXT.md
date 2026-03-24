# Phase 14: Content Migration + Orchestrator - Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Wrap existing ContentSerializer/ContentDeserializer as a ContentProvider adapter behind ISerializationProvider, build an orchestrator that dispatches predicates to providers by type, update ConfigLoader to output ProviderPredicateDefinition for all predicates, and update Management API commands to use the orchestrator with an optional provider filter parameter. Content YAML moves to `_content/` subdirectory.

</domain>

<decisions>
## Implementation Decisions

### Orchestrator Entry Points
- **D-01:** Single unified Management API command pair (ContentSyncSerialize / ContentSyncDeserialize) that uses the orchestrator to dispatch ALL predicates
- **D-02:** Optional `provider` parameter filters to a single provider type (e.g., `?provider=Content` or `?provider=SqlTable`). No parameter = serialize/deserialize all providers.
- **D-03:** Remove the separate `SqlTableSerializeCommand` and `SqlTableDeserializeCommand` — the unified commands replace them
- **D-04:** Scheduled tasks also route through the orchestrator (or are deprecated — Phase 16 removes them, but for now they still work)

### ContentProvider Output Path
- **D-05:** Content YAML files move to `_content/` subdirectory under SerializeRoot — clean separation between providers from day one
- **D-06:** ContentProvider adapter passes `{outputRoot}/_content/` as the output directory to ContentSerializer, not the bare SerializeRoot
- **D-07:** ContentDeserializer reads from `{inputRoot}/_content/` — the adapter handles the path translation

### Config & Type Unification
- **D-08:** ConfigLoader converts ALL predicates to `ProviderPredicateDefinition` at load time — one type flows through the entire system
- **D-09:** Predicates without `providerType` get `providerType: "Content"` added automatically
- **D-10:** No backward compatibility concerns — the app is not widely used yet, clean break is fine
- **D-11:** The old `PredicateDefinition` type can be removed or kept internally for ContentSerializer compatibility — Claude's discretion on the cleanest approach

### ContentProvider Adapter
- **D-12:** ContentProvider wraps ContentSerializer/ContentDeserializer without ANY internal changes to those classes
- **D-13:** ContentProvider constructs a `SyncConfiguration` from the `ProviderPredicateDefinition` and delegates — adapter pattern
- **D-14:** ContentProvider implements ISerializationProvider (Serialize, Deserialize with isDryRun, ValidatePredicate)

### Claude's Discretion
- Orchestrator class name and location (SerializerOrchestrator, Orchestrator, etc.)
- How to handle aggregate results across providers (new AggregateResult type or reuse existing)
- Whether ConfigLoader returns a new config type or extends SyncConfiguration
- ContentProvider constructor injection vs direct instantiation
- How scheduled tasks interact with orchestrator (thin wrapper or direct)

</decisions>

<specifics>
## Specific Ideas

- The orchestrator should be simple — iterate predicates, look up provider in registry, call Serialize/Deserialize, aggregate results
- ContentProvider is the lowest-risk validation of the provider architecture — it wraps existing battle-tested code
- The `_content/` path change means existing serialized YAML at SerializeRoot/ needs to be re-serialized (one-time migration, not a concern for the app itself)

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Provider Architecture (Phase 13 — already built)
- `src\Dynamicweb.ContentSync\Providers\ISerializationProvider.cs` — Interface contract
- `src\Dynamicweb.ContentSync\Providers\SerializationProviderBase.cs` — Base class with YAML helpers
- `src\Dynamicweb.ContentSync\Providers\ProviderRegistry.cs` — Provider lookup by type string
- `src\Dynamicweb.ContentSync\Models\ProviderPredicateDefinition.cs` — Extended predicate with Table/NameColumn/CompareColumns

### Existing Content Serialization (must NOT be modified internally)
- `src\Dynamicweb.ContentSync\Serialization\ContentSerializer.cs` — Content serialization orchestrator
- `src\Dynamicweb.ContentSync\Serialization\ContentDeserializer.cs` — Content deserialization with GUID resolution
- `src\Dynamicweb.ContentSync\Serialization\DeserializeResult.cs` — Structured result object

### Config System (to be updated)
- `src\Dynamicweb.ContentSync\Configuration\ConfigLoader.cs` — Currently validates path/areaId, already partially provider-aware
- `src\Dynamicweb.ContentSync\Configuration\SyncConfiguration.cs` — Current config record with PredicateDefinition list

### Management API Commands (to be unified)
- `src\Dynamicweb.ContentSync\AdminUI\Commands\ContentSyncSerializeCommand.cs` — Current content-only serialize
- `src\Dynamicweb.ContentSync\AdminUI\Commands\ContentSyncDeserializeCommand.cs` — Current content-only deserialize
- `src\Dynamicweb.ContentSync\AdminUI\Commands\SqlTableSerializeCommand.cs` — To be removed (merged into unified command)
- `src\Dynamicweb.ContentSync\AdminUI\Commands\SqlTableDeserializeCommand.cs` — To be removed (merged into unified command)

### SqlTableProvider (Phase 13 — reference for provider pattern)
- `src\Dynamicweb.ContentSync\Providers\SqlTable\SqlTableProvider.cs` — Working provider implementation to model ContentProvider after

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **ISerializationProvider + ProviderRegistry**: Already built in Phase 13 — ContentProvider registers as "Content" type
- **SqlTableProvider**: Working reference implementation of the provider interface
- **ConfigLoader**: Already partially provider-aware (skips path/areaId for SqlTable predicates)
- **DeserializeResult**: Structured result from ContentDeserializer — ContentProvider can return this directly

### Established Patterns
- **Log callback**: `Action<string>? log` — all providers and orchestrator should follow this
- **CommandBase**: DW management command pattern — Handle() returns CommandResult
- **Record types**: Config and predicates are records

### Integration Points
- **Orchestrator replaces direct ContentSerializer usage** in commands and scheduled tasks
- **ConfigLoader outputs ProviderPredicateDefinition[]** instead of PredicateDefinition[]
- **ProviderRegistry.GetProvider("Content")** returns ContentProvider instance
- **ContentProvider passes `_content/` subpath** to existing ContentSerializer

</code_context>

<deferred>
## Deferred Ideas

- FK dependency ordering across tables — Phase 15
- ServiceCache invalidation — Phase 15
- Log viewer with guided advice — Phase 16
- Admin UI menu relocation — Phase 16
- Scheduled task deprecation — Phase 16
- Project rename — Phase 17

</deferred>

---

*Phase: 14-content-migration-orchestrator*
*Context gathered: 2026-03-24*

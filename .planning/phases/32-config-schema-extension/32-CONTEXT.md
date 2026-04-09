# Phase 32: Config Schema Extension - Context

**Gathered:** 2026-04-09
**Status:** Ready for planning

<domain>
## Phase Boundary

Extend the JSON config model with two new top-level dictionary properties (`excludeFieldsByItemType`, `excludeXmlElementsByType`) so that downstream UI phases (33-37) can persist per-type exclusion settings. Existing flat per-predicate arrays must continue working. This is a pure data model + serialization logic change with no UI work.

</domain>

<decisions>
## Implementation Decisions

### Config Placement
- **D-01:** New dictionaries are **top-level properties on `SerializerConfiguration`**, not per-predicate. Item type and XML type exclusions are system-wide concerns that apply globally across all predicates.

### Merge Semantics
- **D-02:** **Union merge** — per-predicate flat arrays (`excludeFields`, `excludeXmlElements`) exclude broadly across all types. Global typed dictionaries (`excludeFieldsByItemType`, `excludeXmlElementsByType`) exclude narrowly by specific type name. Both are additive; final exclusion set = union of flat + typed for the relevant type.

### Config Save/Load
- **D-03:** Same `ConfigWriter.Save()` / `ConfigLoader.Load()` round-trip path. Add dictionary properties to `SerializerConfiguration` and `RawSerializerConfiguration`. System.Text.Json handles serialization/deserialization automatically. Same atomic write pattern (tmp file + move).

### Claude's Discretion
- Test coverage approach for backward compatibility (CFG-02)
- Internal implementation of the merge logic (where in the serialize/deserialize pipeline the union happens)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Config Infrastructure
- `src/DynamicWeb.Serializer/Configuration/ConfigLoader.cs` — Current config loading with RawSerializerConfiguration and RawPredicateDefinition models
- `src/DynamicWeb.Serializer/Configuration/ConfigWriter.cs` — Atomic JSON save (tmp + move pattern)
- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` — Top-level config record where new dictionaries will be added

### Models
- `src/DynamicWeb.Serializer/Models/ProviderPredicateDefinition.cs` — Per-predicate model with existing flat ExcludeFields/ExcludeXmlElements arrays

### Serialization Pipeline (merge points)
- `src/DynamicWeb.Serializer/Serialization/ContentSerializer.cs` — Where ExcludeFields/ExcludeXmlElements are currently applied during content serialization
- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` — Where exclusions apply during deserialization
- `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs` — Field mapping logic that uses exclusion lists
- `src/DynamicWeb.Serializer/Infrastructure/XmlFormatter.cs` — XML element exclusion during pretty-print
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableProvider.cs` — SqlTable exclusion application

### Tests
- `tests/DynamicWeb.Serializer.Tests/Configuration/ConfigLoaderTests.cs` — Existing config loading tests (CFG-02 backward compat baseline)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ConfigLoader` already uses a raw-model-then-validate pattern — new dictionary properties follow the same approach
- `ConfigWriter.Save()` serializes full `SerializerConfiguration` via System.Text.Json — dictionaries serialize automatically
- `RawSerializerConfiguration` / `RawPredicateDefinition` provide the nullable deserialization layer

### Established Patterns
- All config properties use `init`-only setters on records
- Validation happens in `ConfigLoader.Validate()` after raw deserialization
- `JsonNamingPolicy.CamelCase` is used for serialization, `PropertyNameCaseInsensitive = true` for deserialization

### Integration Points
- Serialization pipeline reads `ExcludeFields`/`ExcludeXmlElements` from the predicate — merge logic needs to combine these with the new global dictionaries before passing to serialization methods
- `XmlFormatter` receives exclusion lists — needs to receive the merged set
- Admin UI commands (`SavePredicateCommand`) call `ConfigWriter.Save()` — will now also save top-level dictionaries

</code_context>

<specifics>
## Specific Ideas

No specific requirements — standard dictionary property addition following existing config patterns.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 32-config-schema-extension*
*Context gathered: 2026-04-09*

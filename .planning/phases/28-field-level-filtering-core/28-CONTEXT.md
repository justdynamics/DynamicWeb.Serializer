# Phase 28: Field-Level Filtering Core - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning
**Mode:** Auto-generated (autonomous mode)

<domain>
## Phase Boundary

Content predicates can exclude specific fields from serialization, strip specific XML elements from blobs, and excluded fields are safely skipped during deserialization (no null-out destruction). This is the foundation for all field-level filtering — Phase 29 (SqlTable) and Phase 30 (Area) build on this.

</domain>

<decisions>
## Implementation Decisions

### Field Filtering Architecture
- Add `ExcludeFields` (List<string>) to ProviderPredicateDefinition — blacklist of field names to omit during serialization
- Add `ExcludeXmlElements` (List<string>) to ProviderPredicateDefinition — XML element names to strip from embedded XML blobs
- Both follow three-class config mapping pattern (ProviderPredicateDefinition + RawPredicateDefinition + BuildPredicate) — same as xmlColumns in Phase 27
- Filtering applies at the ContentMapper level (serialize side) and is guarded at ContentDeserializer level (deserialize side)

### Deserialize Skip Guard (CRITICAL — FILT-03)
- MUST ship atomically with serialize-side filtering
- Current source-wins behavior: SaveItemFields nulls out fields absent from YAML
- With excludeFields, absent fields are INTENTIONALLY absent — must NOT be nulled out
- Skip guard: pass the excludeFields list to ContentDeserializer, and skip null-out for any field in the exclude list
- Same guard needed for SavePropertyItemFields and any other null-out loops

### XML Element Filtering (FILT-04)
- Add XmlFormatter.RemoveElements(string xml, IEnumerable<string> elementNames) method
- Strips matching elements from the XML DOM before pretty-printing
- Applied AFTER PrettyPrint in ContentMapper (or integrated into PrettyPrint with optional parameter)
- Case-insensitive element name matching for usability

### Claude's Discretion
- Exact location of filter application in ContentMapper (per-field vs centralized)
- Whether excludeFields applies to page properties, paragraph fields, area properties, or all three
- Test strategy for the skip guard

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- XmlFormatter from Phase 26 — extend with RemoveElements method
- Config three-class pattern from Phase 27 — proven for xmlColumns, reuse for excludeFields/excludeXmlElements
- ForceStringScalarEmitter — already in both content and SQL pipelines

### Critical Integration Points (from Pitfalls research)
- ContentDeserializer.SaveItemFields (lines ~842-850) — null-out loop that MUST be guarded
- ContentDeserializer.SavePropertyItemFields — similar null-out pattern
- ContentMapper.MapPage/MapParagraph/MapArea — serialize-side filter points
- ConfigLoader — three-class mapping extension

</code_context>

<specifics>
## Specific Ideas

The skip guard is the highest-risk change in this milestone. The existing null-out behavior is:
- For each field in the DB that is NOT in the YAML: set it to null/empty
- This is correct for source-wins (files are truth)
- But with excludeFields, the field was INTENTIONALLY omitted — nulling it destroys the target's value

Prevention: pass excludeFields to the deserializer and check each field against the list before nulling.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

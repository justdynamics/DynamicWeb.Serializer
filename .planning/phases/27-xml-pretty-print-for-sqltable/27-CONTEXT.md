# Phase 27: XML Pretty-Print for SqlTable - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning
**Mode:** Auto-generated (infrastructure phase — discuss skipped)

<domain>
## Phase Boundary

SQL table YAML files with XML columns show readable indented XML, controlled by a per-predicate xmlColumns config list. Reuses XmlFormatter from Phase 26. Adds xmlColumns config to PredicateConfig and integrates pretty-print into SqlTableProvider/FlatFileStore pipeline.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
All implementation choices are at Claude's discretion — pure infrastructure phase.

Key research findings to incorporate:
- Reuse XmlFormatter.PrettyPrint/Compact from Phase 26 (Infrastructure/XmlFormatter.cs)
- Add `xmlColumns` list property to ProviderPredicateDefinition (and RawPredicateDefinition + BuildPredicate mapping)
- Config three-class mapping: ProviderPredicateDefinition, RawPredicateDefinition, AND BuildPredicate() must ALL be updated (pitfall P7 from research)
- FlatFileStore serializer needs ForceStringScalarEmitter added to its SerializerBuilder (currently missing — SQL YAML emits XML as single-line)
- Pretty-print only columns listed in xmlColumns (config-driven, not heuristic)
- On deserialize (SqlTableWriter), Compact XML columns before writing to DB
- Real XML-containing SQL tables: DashboardWidget, EcomFeed, EcomShippings, PersonalSettings, ScheduledTask, ContentModuleConfiguration, AiGeneratorConfiguration, etc.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- XmlFormatter.PrettyPrint/Compact from Phase 26
- ForceStringScalarEmitter — needs to be added to FlatFileStore's serializer

### Integration Points
- FlatFileStore — serialize path for SQL YAML (add ForceStringScalarEmitter + pretty-print xmlColumns)
- SqlTableProvider — reads rows, serializes via FlatFileStore
- SqlTableWriter/SqlTableProviderDeserialize — deserialize path (Compact xmlColumns before DB write)
- ConfigLoader/ProviderPredicateDefinition — add xmlColumns config field
- RawPredicateDefinition + BuildPredicate — config mapping (three-class pattern)

</code_context>

<specifics>
## Specific Ideas

No specific requirements — infrastructure phase.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

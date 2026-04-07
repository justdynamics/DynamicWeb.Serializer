# Phase 29: SqlTable Field Filtering - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning
**Mode:** Auto-generated (infrastructure phase — discuss skipped)

<domain>
## Phase Boundary

SqlTable predicates can exclude specific columns from serialization with the same skip-guard protection on deserialize. Reuses excludeFields config from Phase 28 (already in ProviderPredicateDefinition). Applies filtering in SqlTableProvider serialize and deserialize paths.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
All implementation choices are at Claude's discretion — infrastructure phase extending Phase 28 patterns to SqlTable pipeline.

Key points:
- ExcludeFields already exists on ProviderPredicateDefinition (added in Phase 28)
- SqlTableProvider.Serialize() needs to skip excluded columns when writing YAML rows
- SqlTableProvider deserialize path needs skip guard — excluded columns must NOT be nulled/deleted
- ExcludeXmlElements also already exists — apply to SQL XML columns after pretty-print (reuse XmlFormatter.RemoveElements)
- Follow same patterns established in Phase 28 for Content pipeline

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- ProviderPredicateDefinition.ExcludeFields (from Phase 28)
- ProviderPredicateDefinition.ExcludeXmlElements (from Phase 28)
- XmlFormatter.RemoveElements (from Phase 28)

### Integration Points
- SqlTableProvider.Serialize() — filter excluded columns from row dict before WriteRow
- SqlTableProvider deserialize path (SqlTableWriter/BuildMergeCommand) — skip guard for excluded columns
- FlatFileStore.WriteRow — may need filtering before write, or filter in SqlTableProvider before calling FlatFileStore

</code_context>

<specifics>
## Specific Ideas

No specific requirements — extends Phase 28 patterns to SqlTable pipeline.

</specifics>

<deferred>
## Deferred Ideas

None.

</deferred>

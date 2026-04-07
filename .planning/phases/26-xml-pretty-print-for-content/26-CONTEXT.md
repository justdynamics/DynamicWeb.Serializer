# Phase 26: XML Pretty-Print for Content - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning
**Mode:** Auto-generated (infrastructure phase — discuss skipped)

<domain>
## Phase Boundary

Embedded XML blobs in content YAML (moduleSettings, urlDataProviderParameters) become readable indented multi-line XML, and round-trip back to compact single-line on deserialize. This phase creates the shared XmlFormatter utility and integrates it into the ContentProvider pipeline.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion
All implementation choices are at Claude's discretion — pure infrastructure phase. Use ROADMAP phase goal, success criteria, and codebase conventions to guide decisions.

Key research findings to incorporate:
- Use XDocument.Parse() + ToString(SaveOptions.None) for pretty-printing
- MUST normalize CRLF to LF before storing in model (ForceStringScalarEmitter emits DoubleQuoted for \r, defeating pretty-print)
- ForceStringScalarEmitter already handles literal block scalars for LF-containing strings
- XML declaration (<?xml ...?>) should be preserved through round-trip
- Compact XML on deserialize before writing to DB (single-line, matching original format)
- Handle malformed/non-XML strings gracefully (pass through unchanged)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- ForceStringScalarEmitter — already selects ScalarStyle.Literal for strings with \n
- ContentMapper — maps DW content to DTOs (integration point for pretty-print on serialize)
- ContentDeserializer — writes DTOs back to DB (integration point for compact on deserialize)

### Established Patterns
- Infrastructure classes go in Infrastructure/ namespace
- DTOs in Models/ namespace
- Mappers in Mapping/ namespace

### Integration Points
- ContentMapper.MapParagraph() — moduleSettings field
- ContentMapper.MapPage() — urlDataProviderParameters field
- ContentDeserializer — reverse: compact XML before DB write

</code_context>

<specifics>
## Specific Ideas

No specific requirements — infrastructure phase. Refer to ROADMAP phase description and success criteria.

Real data patterns from YAML analysis:
- moduleSettings: `<?xml version="1.0" encoding="utf-8"?><Settings>...</Settings>` (45 files)
- urlDataProviderParameters: `<?xml version="1.0" encoding="utf-8"?><Parameters addin="...">...</Parameters>` (2 files)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

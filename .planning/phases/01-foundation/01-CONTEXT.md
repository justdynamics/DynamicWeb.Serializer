# Phase 1: Foundation - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Create the shared data contract: plain C# DTO types for all DynamicWeb content nodes, YAML serialization/deserialization with proven round-trip fidelity, and mirror-tree file I/O that reads/writes the content hierarchy to disk. No DynamicWeb API integration in this phase — that's Phase 3.

</domain>

<decisions>
## Implementation Decisions

### DTO Field Design
- ItemType custom fields represented as `Dictionary<string, object>` — flexible key-value bag, works for any ItemType without schema discovery
- Parent-child relationships expressed via children collections: Page has `List<GridRow>`, GridRow has `List<Paragraph>`, etc. — tree structure in the DTO itself
- Capture ALL fields including system/audit fields (CreatedDate, ModifiedDate, CreatedBy, etc.) — full fidelity for exact replica on deserialize
- DTOs must have NO DynamicWeb dependencies — plain C# records/classes only

### YAML File Structure
- One .yml file per content item (page, grid row, paragraph) — granular files for clean git diffs
- Page .yml contains only page metadata and fields, NOT nested children
- Grid rows and paragraphs get their own files in subfolders within the page folder
- Example structure:
  ```
  Customer Center/
    page.yml           # Page metadata + fields only
    grid-row-1/
      grid-row.yml     # Grid row metadata
      paragraph-1.yml  # Paragraph content
      paragraph-2.yml
    Sub Page/
      page.yml
  ```

### Mirror-tree Naming
- Folder names use sanitized page names (special chars replaced, spaces preserved)
- Duplicate sibling page names disambiguated with short GUID suffix: "Customer Center [a1b2c3]"
- Output directory is configurable via config file (not a hardcoded path)
- Deterministic sort: items always written in a consistent order to prevent git noise

### Claude's Discretion
- .NET project layout (namespace conventions, folder structure)
- Exact sanitization rules for folder names (which characters to strip/replace)
- YamlDotNet serializer/deserializer configuration details (ScalarStyle, emitter settings)
- Test project structure and framework choice

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DynamicWeb Content Model
- `.planning/research/ARCHITECTURE.md` — Component boundaries, DW service API mapping, content hierarchy traversal
- `.planning/research/STACK.md` — YamlDotNet 16.3.0 config, Dynamicweb.Core 10.23.9 APIs, DTO design guidance

### YAML Fidelity
- `.planning/research/PITFALLS.md` — YAML round-trip pitfalls: tilde=null, CRLF loss, HTML corruption, ScalarStyle requirements
- `.planning/research/SUMMARY.md` — Synthesized research findings, build order recommendations

### Project Requirements
- `.planning/REQUIREMENTS.md` — SER-01 (full tree), SER-02 (mirror-tree), SER-04 (deterministic output)
- `.planning/PROJECT.md` — Core value, constraints, key decisions

### DynamicWeb AppStore App
- DynamicWeb AppStore guide: https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html
- DynamicWeb scheduled tasks: https://doc.dynamicweb.dev/documentation/extending/extensibilitypoints/scheduled-task-addins.html

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — greenfield project, no existing code

### Established Patterns
- None — patterns will be established in this phase (DTOs, YAML config, file I/O)

### Integration Points
- Phase 2 (Configuration) will consume the predicate evaluation against the content tree DTOs
- Phase 3 (Serialization) will map DynamicWeb API objects to these DTOs
- Phase 4 (Deserialization) will read these DTOs from YAML and write to DynamicWeb API

</code_context>

<specifics>
## Specific Ideas

- File layout inspired by Sitecore Unicorn's Rainbow serialization format — one file per item, tree on disk mirrors tree in CMS
- Config file should use JSON format (not YAML) to avoid indentation ambiguity in machine-written config (from research)
- Test the YAML round-trip fidelity with known-tricky strings early: tilde (`~`), CRLF, raw HTML, double quotes, bang (`!`)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-foundation*
*Context gathered: 2026-03-19*

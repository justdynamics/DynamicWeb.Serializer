# Phase 2: Configuration - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Create the JSON config file format and predicate include/exclude system. A developer can define which content trees ContentSync operates on through a config file, and the predicate system correctly evaluates include/exclude rules. No DynamicWeb API integration — predicates evaluate against paths, not live content.

</domain>

<decisions>
## Implementation Decisions

### Config File Shape
- File name: `ContentSync.config.json`
- Location: project root (next to appsettings.json)
- Format: JSON (decided in research — avoids YAML indentation ambiguity)
- Top-level settings:
  - `outputDirectory` — where serialized YAML files are written (configurable, decided Phase 1)
  - `logLevel` — control verbosity (errors, info, debug)
  - `predicates` — array of predicate definitions

### Predicate Rule Format
- Path-based identification: `{ "path": "/Customer Center", "areaId": 1 }`
- `areaId` is required — explicit website scope, no auto-detection
- Each predicate has a `name` for logging/identification
- Predicates support an `excludes` array of subpaths to skip within the included tree

### Config File Example
```json
{
  "outputDirectory": "./serialization",
  "logLevel": "info",
  "predicates": [
    {
      "name": "Customer Center",
      "path": "/Customer Center",
      "areaId": 1,
      "excludes": [
        "/Customer Center/Archive",
        "/Customer Center/Drafts"
      ]
    }
  ]
}
```

### Predicate Evaluation
- Include: a content path matches a predicate if it starts with the predicate's path
- Exclude: a content path is excluded if it starts with any path in the predicate's excludes array
- Exclude overrides include (an item under an included path but matching an exclude is skipped)

### Claude's Discretion
- Config validation error messages and behavior (fail fast vs warn)
- Internal data model for config (POCO classes, records)
- Whether to use System.Text.Json or Newtonsoft for parsing
- Unit test structure and assertions

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing Codebase
- `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs` — takes root path, will consume output directory from config
- `src/Dynamicweb.ContentSync/Infrastructure/IContentStore.cs` — interface for tree I/O
- `src/Dynamicweb.ContentSync/Models/` — DTO types the predicate system evaluates against

### Project Requirements
- `.planning/REQUIREMENTS.md` — CFG-01 (standalone config file), CFG-02 (predicate rules)
- `.planning/PROJECT.md` — Unicorn-style predicate configuration decision

### Research
- `.planning/research/FEATURES.md` — Unicorn predicate system analysis, competitor comparison
- `.planning/research/PITFALLS.md` — Predicate misconfiguration silently excludes items (must log excluded items)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FileSystemStore` — already accepts a root path parameter; will consume `outputDirectory` from config
- `YamlConfiguration` — serializer/deserializer factory; config doesn't need YAML but establishes infrastructure patterns
- `IContentStore` — interface contract; predicate filtering could wrap this

### Established Patterns
- Record types used for DTOs (Phase 1) — config model could follow same pattern
- Infrastructure namespace for cross-cutting concerns
- xunit for testing with ContentTreeBuilder fixture

### Integration Points
- `FileSystemStore` constructor takes `rootPath` — will need to read from config's `outputDirectory`
- Phase 3 (Serialization) will use predicates to determine which content trees to serialize
- Phase 4 (Deserialization) will use predicates to determine what to expect from disk

</code_context>

<specifics>
## Specific Ideas

- Unicorn's predicate system uses path-based matching with include/exclude — ContentSync adapts this for DynamicWeb's Area > Page hierarchy
- Predicate must be fully unit-testable without a live DynamicWeb instance (success criterion from ROADMAP.md)
- Config file must produce clear error messages on malformed input (missing required fields, invalid paths)

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-configuration*
*Context gathered: 2026-03-19*

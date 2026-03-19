# Phase 4: Deserialization - Context

**Gathered:** 2026-03-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the disk-to-DynamicWeb write pipeline. Read YAML files via FileSystemStore.ReadTree(), resolve GUID-based identity against the target DB, write items in dependency order (Areas → Pages → GridRows → Paragraphs), and provide dry-run mode that reports what would change without writing. Verify with Customer Center YAML files applied to the Swift2.1 target instance.

</domain>

<decisions>
## Implementation Decisions

### Update Behavior
- All fields overwritten on GUID match — full replica, consistent with source-wins philosophy
- System fields (CreatedDate, CreatedBy, UpdatedDate, UpdatedBy) included in updates — target mirrors source exactly
- ItemType custom fields (Dictionary<string, object>) use full replace — clear target's fields, write all from YAML. If a field exists in target but not in YAML, it gets removed
- New items (no GUID match): attempt to set system fields from YAML, not just content fields. May require post-save update if DW auto-assigns on insert

### Failure Handling
- Continue-and-report on individual item failure — log error with item GUID and context, skip failed item, continue with remaining items
- Structured error summary at end: X succeeded, Y failed, Z skipped
- Cascade skip: if a parent page fails, skip all its children (they'd be orphaned). Log as "skipped due to parent failure"
- Error summary satisfies ROADMAP success criterion "rolled back or clearly reported" — no rollback mechanism needed
- DW transaction support is unverified (open blocker) — research must determine if PageService/GridService support transactions

### Dry-Run Mode (DES-04)
- Full field-level diffs: for UPDATE items, show exactly which fields differ between YAML and current DB state
- Only show changed fields — omit unchanged fields for compact, focused output
- For CREATE items: list all fields being set (no diff, since item doesn't exist)
- For SKIP items: note "unchanged" with GUID
- Output through structured logging (ILogger) — same infrastructure as real runs, works with DW log viewer
- Dry-run requires reading current DB state for each matching GUID to compute field-level diffs

### Orphan Items
- Ignore completely in v1 — don't touch items in DB that aren't in YAML files
- No orphan detection, no warnings, no tracking infrastructure
- OPS-04 (orphan handling) is explicitly v2 scope
- Minimal design — no pre-built orphan tracking, YAGNI

### Claude's Discretion
- GUID→numeric ID reference resolution approach (two-pass vs single-pass with deferred refs)
- Exact DW API method signatures for creating/updating pages, grid rows, paragraphs
- How to resolve DW services for writing (Services.Pages, etc.)
- Whether DW supports setting PageUniqueId on insert or if it's auto-generated
- Integration test structure and assertions for verifying write results

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### DynamicWeb APIs
- `.planning/research/ARCHITECTURE.md` — Component boundaries, DW service API mapping, content hierarchy traversal
- `.planning/research/STACK.md` — Dynamicweb.Core 10.23.9 APIs, service namespaces
- DynamicWeb Content API: https://doc.dynamicweb.dev/api/Dynamicweb.Content.html
- DynamicWeb Content manual: https://doc.dynamicweb.dev/manual/dynamicweb10/content/index.html

### Existing Codebase (Phase 3 — inverse operations)
- `src/Dynamicweb.ContentSync/Serialization/ContentMapper.cs` — DW→DTO mapping (reverse needed: DTO→DW)
- `src/Dynamicweb.ContentSync/Serialization/ContentSerializer.cs` — Serialize orchestrator (inverse pattern for deserializer)
- `src/Dynamicweb.ContentSync/Serialization/ReferenceResolver.cs` — GUID↔numeric ID resolution (reuse/extend for reverse direction)
- `src/Dynamicweb.ContentSync/Infrastructure/FileSystemStore.cs` — ReadTree() method provides DTO input for deserialization
- `src/Dynamicweb.ContentSync/Models/` — All DTO record types (SerializedArea, SerializedPage, SerializedGridRow, SerializedGridColumn, SerializedParagraph)

### Configuration
- `src/Dynamicweb.ContentSync/Configuration/ConfigLoader.cs` — Config loading (shared with serializer)
- `src/Dynamicweb.ContentSync/Configuration/ContentPredicate.cs` — Predicate evaluation (shared)

### Project Requirements
- `.planning/REQUIREMENTS.md` — DES-01 (deserialize), DES-02 (GUID identity), DES-03 (dependency order), DES-04 (dry-run)
- `.planning/PROJECT.md` — ID strategy, source-wins conflict resolution, test environment

### Known Blockers
- `.planning/STATE.md` — DW transaction support across PageService/GridService/ParagraphService is unverified
- `.planning/STATE.md` — `GetParagraphsByPageId` active/inactive behavior is forum-documented only
- `.planning/STATE.md` — Whether DW injects IConfiguration into scheduled task addins is unverified

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FileSystemStore.ReadTree()` — Returns fully populated SerializedArea from YAML files on disk; direct input to deserializer
- `ReferenceResolver` — Has GUID↔numeric ID caching; can be extended for reverse direction (GUID→new numeric ID in target)
- `ContentMapper` — DW→DTO mapping methods; reverse patterns inform DTO→DW mapper design
- `ContentSerializer.Serialize()` — Orchestration pattern (loop predicates, walk tree, clear caches) to mirror in deserializer
- `ContentPredicateSet` — Same predicate filtering applies to deserialization scope
- `ConfigLoader` — Same config file drives both serialize and deserialize
- `YamlConfiguration.BuildDeserializer()` — Already configured for reading YAML back to DTOs

### Established Patterns
- `Services.Xxx` static accessors for DW APIs (not constructor injection)
- Record types for DTOs with `Dictionary<string, object>` Fields bag
- IContentStore interface for file I/O abstraction
- xunit with ContentTreeBuilder for test fixtures

### Integration Points
- ContentDeserializer will be a peer to ContentSerializer in the Serialization namespace
- Shares ConfigLoader, ContentPredicateSet, FileSystemStore, and ReferenceResolver
- Writes to DW via Services.Pages, Services.Grids, Services.Paragraphs (same services serializer reads from)
- Test target: Swift2.1 instance at `C:\Projects\Solutions\swift.test.forsync`

</code_context>

<specifics>
## Specific Ideas

- Deserializer is the inverse of ContentSerializer — read YAML via ReadTree(), walk the DTO tree, write to DW
- Test against Swift2.1 (.NET 8) target instance with Customer Center YAML files produced by Phase 3 serializer
- DLL copy deployment to Swift2.1 for testing (same approach as Phase 3 with Swift2.2)
- Dependency order is natural from tree traversal: Area first, then pages top-down, then grid rows per page, then paragraphs per grid row

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 04-deserialization*
*Context gathered: 2026-03-19*

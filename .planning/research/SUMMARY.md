# Project Research Summary

**Project:** Dynamicweb.ContentSync
**Domain:** CMS Content Serialization / Cross-Environment Sync Tooling (DynamicWeb AppStore App)
**Researched:** 2026-03-19
**Confidence:** HIGH

## Executive Summary

Dynamicweb.ContentSync is a DynamicWeb 10 AppStore app that serializes content trees (Areas, Pages, Grid Rows, Paragraphs) to YAML files on disk and deserializes them back into the database — enabling source-controlled, repeatable content deployments across environments. This problem space is well-understood: Sitecore Unicorn and TDS have solved it for the Sitecore ecosystem, and the patterns translate directly to DynamicWeb's content model. The recommended approach is to implement the same five-subsystem architecture (Predicate, TreeWalker, Serializer/Deserializer, IdentityResolver, Evaluator) orchestrated by two thin scheduled task entry points. YamlDotNet (16.3.0) on .NET 8 is the unambiguous stack choice; no meaningful alternatives exist in the .NET YAML space.

The core insight from research is that GUID-based identity resolution is the load-bearing design decision for this entire project. DynamicWeb numeric IDs are environment-specific and must never appear as references in serialized YAML. Every other design choice flows from this: the mirror-tree file layout, the source-wins evaluator, the dependency-ordered deserialization write pass, and the predicate-scoped orphan handling. Getting the identity model right in the first implementation phase is the single most important risk mitigation.

The primary implementation risks are all known and avoidable: YAML round-trip data loss from unconfigured scalar styles (configure `ScalarStyle.Literal` for multiline before any serialization code ships), parent-before-child ordering violations during deserialization (build the depth-first sort into the write pass architecture from the start), and partial deserialization leaving the target in a broken state (wrap the write pass in a database transaction or implement a clear rollback strategy). All three risks have explicit prevention strategies documented in PITFALLS.md and must be addressed in design, not retrofitted.

## Key Findings

### Recommended Stack

The stack is simple and uncontroversial. DynamicWeb 10.2+ requires .NET 8 LTS, and `Dynamicweb.Core` (10.23.9, published 2026-03-17) is the root NuGet package — it pulls in all required content APIs transitively. YamlDotNet 16.3.0 is the only viable .NET YAML library (43.5M+ downloads, actively maintained, SharpYaml is dead since 2018). The config file for predicates should be JSON (not YAML) to avoid indentation ambiguity in machine-written config.

**Core technologies:**
- **.NET 8.0 LTS:** Target framework — required by DynamicWeb 10.2+
- **Dynamicweb.Core 10.23.9:** DW platform APIs (AreaService, PageService, GridService, Paragraphs, BaseScheduledTaskAddIn, LogManager) — the root package for all DW functionality
- **YamlDotNet 16.3.0:** YAML serialization — dominant .NET library, no real competition
- **Microsoft.Extensions.Configuration.Json 8.0.x:** Config file loading — only needed if DW does not inject IConfiguration directly (verify at implementation)
- **xunit 2.9.3 + Moq 4.x:** Test framework and mocking — xUnit is the dominant community choice for .NET libraries

**Critical constraint:** Do not serialize DW model objects (`Page`, `Paragraph`) directly — they carry internal state and lazy-loaded collections. Always map to plain C# DTO types (`SerializedPage`, `SerializedParagraph`) before touching YamlDotNet. The `GetParagraphsByPageId` method only returns active paragraphs by default — investigate the `bool` overload for including inactive paragraphs at implementation time.

### Expected Features

Research against Sitecore Unicorn and TDS provides a clear feature baseline. Every table-stakes feature has a direct Unicorn analogue that is well-documented.

**Must have (table stakes):**
- Serialize full content tree (Area > Page > Grid Row > Column > Paragraph) — core value, nothing works without this
- Deserialize files back to database with GUID-based identity resolution — the other half of the core loop
- Mirror-tree file layout (one `.yml` per item, folders reflect hierarchy) — enables readable git diffs
- Predicate configuration via config file (include/exclude path rules) — required for safe operation on real installations
- Source-wins conflict resolution (disk overwrites DB, no merge) — the only safe default
- Orphan handling (delete items in scope but not in files) — prevents content accumulation on target
- Dependency-ordered deserialization (parent before child) — non-optional; FK constraints enforce this
- Two scheduled tasks (serialize + deserialize as separate tasks) — the primary execution mechanism
- Structured logging (new/updated/deleted/skipped/error per item) — required for operator trust
- Fail-loud error handling with item context — prevents silent divergence between environments

**Should have (competitive advantage over DW's built-in deployment tool):**
- Field-level exclusions (FieldFilters) — avoid noisy diffs from system-managed fields like ModifiedDate (add when users report the noise)
- Multiple named configuration sets — needed when multiple feature teams share one DW instance (add when a customer requires it)
- Dry-run mode — report what would change without applying (add when users are nervous about production deserialize)

**Defer to v2+:**
- Real-time change detection via DynamicWeb Notifications API — high complexity; full scheduled sync is sufficient
- Admin UI for configuration and status — out of scope per PROJECT.md; defeats the developer workflow goal
- Incremental/delta sync — requires change tracking infrastructure; full sync is safe and predictable
- Media/file serialization — binary files in git; separate deployment concern

### Architecture Approach

The architecture follows Unicorn's five-subsystem decomposition directly: two thin scheduled task entry points delegate to a SyncCoordinator, which orchestrates a Predicate (what to include), TreeWalker (DW API traversal), ContentSerializer/ContentDeserializer (DW objects ↔ YAML DTOs), IdentityResolver (GUID to numeric ID mapping), and Evaluator (source-wins decision). All DW service calls happen through the service layer APIs — never via direct SQL. The Models layer contains plain DTOs with no DW dependencies, keeping the YAML schema decoupled from DW API churn.

**Major components:**
1. **Tasks/** — Thin DW entry-point wiring only; zero business logic; extends `BaseScheduledTaskAddIn`
2. **Configuration/** — ConfigurationLoader, Predicate; no DW dependency; fully unit-testable
3. **Serialization/** — SyncCoordinator, TreeWalker, ContentSerializer, ContentDeserializer, IdentityResolver; the core pipeline
4. **Models/** — SerializedArea, SerializedPage, SerializedGridRow, SerializedGridColumn, SerializedParagraph; plain DTOs; ItemType custom fields as `Dictionary<string, object>`
5. **Infrastructure/** — FileSystemStore (mirror-tree I/O), ContentSyncLogger (structured logging wrapper)

**Build order is strict:** DTOs first (no dependencies), then FileSystemStore, then ConfigurationLoader+Predicate, then TreeWalker, then Serializer, then IdentityResolver, then Deserializer, then SyncCoordinator, then Scheduled Tasks last. This ordering allows each layer to be tested independently before the next is built.

### Critical Pitfalls

1. **YAML round-trip data loss from unconfigured scalar styles** — Configure `ScalarStyle.Literal` for multiline/HTML fields and `ScalarStyle.DoubleQuoted` for all other strings before writing any serialization code. Write a round-trip fidelity test (serialize known-tricky strings including `~`, CRLF, `<html>`, `"quotes"`, `!bang`, deserialize, assert byte-for-byte equality) before any other serialization work. Recovery cost is HIGH — all existing YAML files must be re-serialized if the style is wrong.

2. **Inserting children before parents on deserialization** — Build a depth-first, parent-before-child insertion queue before writing anything to the database. Load all YAML files into memory, sort by tree depth (Areas=0, Pages=1, GridRows=2, Paragraphs=3), then write. Never write to the DB during file traversal. Design this into the architecture from the start — retrofitting is significantly harder.

3. **Numeric ID leakage in reference fields** — Audit every DW column and field type that stores inter-item references before writing any serialization code. Serialize reference fields as GUIDs (look up GUID by numeric ID at serialize time). On deserialize, look up numeric ID by GUID in target DB. Document fields with embedded numeric IDs in HTML content as a known v1 limitation.

4. **Partial deserialization leaving broken target state** — Wrap the entire deserialization write pass in a database transaction; rollback on any exception and log clearly. If DW APIs do not support explicit transaction wrapping across all content types, implement compensating logic: detect partial runs, log every written item with GUID and numeric ID, warn operators to verify/restore before re-running.

5. **Sibling ordering non-deterministic** — Always serialize `SortOrder` (or equivalent) for every item type. Enforce deterministic query ordering on all DB reads during serialization. Validate: serialize twice without content changes, `git diff` must show zero changes.

## Implications for Roadmap

Based on research, the architecture's build order and the pitfall phase mappings define a natural five-phase structure:

### Phase 1: Foundation — Data Model and YAML Serialization

**Rationale:** The ContentModel DTOs and YamlDotNet configuration are the shared contract that everything else depends on. YAML round-trip fidelity must be proven before any other work begins — if the scalar style is wrong, all downstream YAML is suspect and must be re-generated. This phase has no DW dependencies, enabling full unit test coverage.

**Delivers:** Plain DTO types for all content node types (SerializedArea, SerializedPage, SerializedGridRow, SerializedGridColumn, SerializedParagraph) with `Dictionary<string, object>` for ItemType fields; YamlDotNet configured with `ScalarStyle.Literal` for multiline and `ScalarStyle.DoubleQuoted` for strings; FileSystemStore for mirror-tree read/write; round-trip fidelity test suite passing for known-tricky strings.

**Addresses:** Mirror-tree file layout, YAML format selection, DTO design for custom fields

**Avoids:** YAML round-trip data loss (Pitfall 2), path length overflow on Windows (Pitfall 5 — implement path length check in FileSystemStore from the start), sibling ordering non-determinism (Pitfall 6 — sort order must be in the DTO schema from day one)

### Phase 2: Configuration and Predicate System

**Rationale:** The predicate system is a prerequisite for both serialization and deserialization — it defines what scope to operate on. It has no DW dependency (operates on paths/GUIDs, not live services), so it can be built and thoroughly tested before DW integration begins. The predicate evaluation model (which rule wins, how exclusions override inclusions) must be explicitly documented as a design artifact here.

**Delivers:** JSON config file format for ContentSyncConfiguration; ConfigurationLoader that reads and validates the config on disk; Predicate component that evaluates include/exclude rules against item paths; item-count verification tooling for validating predicate output.

**Addresses:** Predicate configuration (table-stakes feature), multiple named configurations (architecture extensibility)

**Avoids:** Predicate silently excluding required items (Pitfall 7 — build item-count verification alongside the predicate, not after)

### Phase 3: Serialization Pipeline (DW to Disk)

**Rationale:** Serialize before deserialize — the YAML files are a prerequisite for deserialization, and the serializer validates the DW API integration before the more complex deserializer is built. This is also where the ID strategy design must be finalized: which fields are reference fields (serialize as GUID), which are content fields (serialize as-is).

**Delivers:** TreeWalker that traverses the DW content hierarchy using AreaService, PageService, GridService, ParagraphService in predicate-filtered depth-first order; ContentSerializer that maps DW objects to DTOs and writes YAML files; deterministic traversal ordering (ORDER BY SortOrder); reference field audit and GUID serialization for cross-item references.

**Addresses:** Full content tree serialization (table stakes), GUID-based identity, deterministic output

**Avoids:** Numeric ID leakage in reference fields (Pitfall 3 — the reference field audit must happen in this phase, before any YAML files are committed to source control), sibling ordering non-determinism (Pitfall 6 — ORDER BY in all DW queries), N+1 DB query performance trap (batch-load children by parent ID)

### Phase 4: Deserialization Pipeline (Disk to DW)

**Rationale:** Deserialization is the most complex phase — it requires IdentityResolver (GUID lookup in target DB), dependency-ordered write pass, source-wins evaluation, orphan handling, and transaction wrapping for atomicity. Each of these is a distinct sub-component that must be designed together, not bolted on incrementally.

**Delivers:** IdentityResolver with GUID-to-numeric-ID lookup per content type; ContentDeserializer with depth-first, parent-before-child write ordering (Areas > Pages > GridRows > Paragraphs); Evaluator applying source-wins rule (update if GUID found, insert if not); Orphan detection and deletion within predicate scope; database transaction wrapping for atomicity; structured per-item logging (GUID + numeric ID for every write).

**Addresses:** Deserialize files back to database (table stakes), GUID-based identity resolution (table stakes), orphan handling (table stakes), dependency-ordered deserialization, source-wins conflict resolution

**Avoids:** Children before parents on deserialization (Pitfall 1 — depth-first sort is architectural, not a bolt-on), partial deserialization leaving broken state (Pitfall 4 — transaction wrapping designed in, not added later), numeric ID leakage on deserialize side (Pitfall 3 — IdentityResolver is the only component that maps GUIDs to numeric IDs)

### Phase 5: Scheduled Task Integration and End-to-End Validation

**Rationale:** The scheduled tasks are thin wiring — they add no business logic. Building them last allows the full pipeline to be tested in isolation before DW scheduler integration. End-to-end validation in this phase must include the "looks done but isn't" checklist from PITFALLS.md.

**Delivers:** SerializeScheduledTask and DeserializeScheduledTask extending `BaseScheduledTaskAddIn` with `[AddInName]`, `[AddInLabel]`, `[AddInDescription]` attributes; SyncCoordinator wiring all subsystems; end-to-end integration tests (serialize from instance A, deserialize into empty instance B, verify completeness and cross-reference integrity); NuGet package metadata for AppStore distribution.

**Addresses:** Scheduled task automation (differentiator feature), AppStore packaging, structured logging (full pipeline), error handling (fail loud)

**Avoids:** Scheduled task file system permission issues (verify write access when invoked by DW scheduler, not just developer credentials), silent partial success (validate file count matches source item count, and target item count matches file count after deserialize)

### Phase Ordering Rationale

- DTOs and YAML configuration must precede all other phases because they are the shared data contract; wrong scalar styles propagate to all downstream YAML files
- Predicate system precedes DW API integration because it can be fully unit-tested without a live DW instance, enabling early validation of the scoping logic
- Serialization precedes deserialization because the YAML files are a prerequisite for deserialization, and serialization validates DW read APIs before the more complex write APIs are touched
- Deserialization is kept as a single phase because its sub-components (IdentityResolver, write ordering, orphan handling, transaction wrapping) are tightly coupled — splitting them risks building a write loop that cannot be retrofitted with ordering constraints
- Scheduled tasks and AppStore packaging are last because they add no logic — they are integration wiring around an already-validated pipeline

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 3 (Serialization):** The reference field audit (which DW fields carry numeric cross-item references) requires examination of the DW content model field types. This is not documented in official docs and will require empirical investigation of the DW API or database schema.
- **Phase 4 (Deserialization):** DynamicWeb's support for explicit database transaction wrapping across PageService, GridService, and ParagraphService save calls is unverified. If DW does not expose this, the atomicity strategy needs a compensating design. Investigate `SavePage`/`SaveGridRow`/`SaveParagraph` return values and whether they participate in ambient transactions.
- **Phase 4 (Deserialization):** The `GetParagraphsByPageId` forum-documented method may only return active paragraphs. Verify the `bool` overload behavior for including inactive paragraphs before designing the deserialization completeness logic.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Foundation):** YamlDotNet scalar style configuration is well-documented in the YamlDotNet GitHub issues. DTO design is straightforward. No research needed.
- **Phase 2 (Configuration):** Unicorn's predicate model is a well-established pattern. JSON config loading via Microsoft.Extensions.Configuration.Json is standard .NET. No research needed.
- **Phase 5 (Scheduled Tasks):** BaseScheduledTaskAddIn pattern is verified against official DW docs. AppStore packaging is documented. No research needed.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | DW API verified via official docs; NuGet versions verified on nuget.org; YamlDotNet choice is unambiguous |
| Features | HIGH | Unicorn feature set directly analyzed from GitHub; DW built-in deployment tool docs verified; competitor comparison grounded in primary sources |
| Architecture | HIGH | Unicorn architecture well-documented by maintainer; DW 10 API verified via official docs; subsystem decomposition is established prior art |
| Pitfalls | HIGH | Critical pitfalls derived from Unicorn issue tracker, YamlDotNet GitHub issues, Windows MAX_PATH documentation, and database FK constraint literature — all multi-source verified |

**Overall confidence:** HIGH

### Gaps to Address

- **Reference field inventory:** Which DW content fields store numeric inter-item references (not just the primary key) is not documented. Must be discovered empirically during Phase 3 implementation. Treat all integer fields in the DW content model as suspect until audited.
- **Paragraph active/inactive retrieval:** `GetParagraphsByPageId` behavior for inactive paragraphs is documented via forum only (MEDIUM confidence). Verify at Phase 3 implementation with a test page containing inactive paragraphs.
- **Transaction support across DW service layer:** Whether DW's save APIs participate in ambient .NET database transactions is unverified. Verify at Phase 4 design time; design the atomicity strategy based on findings before writing the write loop.
- **IConfiguration injection:** Whether DynamicWeb injects `IConfiguration` into the scheduled task addin context is unverified. If it does, the `Microsoft.Extensions.Configuration.Json` dependency can be removed. Check at Phase 2/5 boundary.
- **App pool write permissions:** The DW app pool identity's write access to paths outside the web root is environment-specific. Must be verified in the target deployment environment before finalizing the output path strategy in Phase 1.

## Sources

### Primary (HIGH confidence)
- [NuGet: Dynamicweb.Core 10.23.9](https://www.nuget.org/packages/Dynamicweb.Core/) — verified version and target framework
- [NuGet: YamlDotNet 16.3.0](https://www.nuget.org/packages/YamlDotNet) — verified version, download count, target frameworks
- [DynamicWeb 10 AppStore App Guide](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) — project structure, csproj metadata
- [DynamicWeb: AreaService Class](https://doc.dynamicweb.com/api/html/02c7da84-1d1c-506d-0054-da04eaff373f.htm) — namespace and key methods
- [DynamicWeb: PageService Class](https://doc.dynamicweb.com/api/html/15516fc9-3e1c-ac41-9849-cc6ad67bb84d.htm) — namespace and key methods
- [DynamicWeb: GridService Class](https://doc.dynamicweb.dev/api/Dynamicweb.Content.GridService.html) — traversal methods
- [DynamicWeb: BaseScheduledTaskAddIn](https://doc.dynamicweb.com/api/html/75745460-c471-a370-1ddc-e4a3ae983f14.htm) — Run() override pattern
- [Unicorn GitHub README](https://github.com/SitecoreUnicorn/Unicorn) — architecture, predicate pattern, orphan handling
- [Unicorn Book](https://unicorn.kamsar.net/working-with-unicorn.html) — maintainer-authored architecture guide
- [DynamicWeb Deployment Tool docs](https://doc.dynamicweb.com/documentation-9/platform/platform-tools/deployment-tool) — competitor feature comparison
- [Windows MAX_PATH documentation](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation) — path length constraints
- [YamlDotNet GitHub Issue #846](https://github.com/aaubry/YamlDotNet/issues/846) — special character serialization issues

### Secondary (MEDIUM confidence)
- [Sitecore Serialization comparison blog](https://the-sitecore-chronicles.cyber-solutions.at/blogs/sitecore-content-serialization-vs-unicorn-and-tds-a-deep-dive-with-examples) — feature comparison, consistent with primary sources
- [DynamicWeb forum: Creating Pages and Paragraphs via API](https://doc.dynamicweb.com/forum/development/creating-pages-and-paragraph-with-the-api?PID=1605) — save API patterns
- [DynamicWeb forum: Paragraphs.GetParagraphsByPageId](https://doc.dynamicweb.com/forum/) — active-only behavior (needs verification)
- [YamlDotNet Issues #391, #361, #934](https://github.com/aaubry/YamlDotNet/issues/) — scalar style, multiline, and numeric string pitfalls

### Tertiary (LOW confidence)
- [Unicorn Octopus Deploy integration](https://www.sitecorenutsbolts.net/2016/03/14/Octopus-Deploy-Step-for-Unicorn-Sync/) — CI/CD integration pattern; dated 2016 but pattern is still valid

---
*Research completed: 2026-03-19*
*Ready for roadmap: yes*

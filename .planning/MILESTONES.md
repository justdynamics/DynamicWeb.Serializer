# Milestones

## v0.5.0 Granular Serialization Control (Shipped: 2026-04-09)

**Phases completed:** 6 phases, 6 plans, 11 tasks

**Key accomplishments:**

- XmlFormatter utility with XDocument-based PrettyPrint/Compact, integrated into ContentMapper (serialize) and ContentDeserializer (deserialize) for readable moduleSettings and urlDataProviderParameters in YAML
- Config-driven xmlColumns on SqlTable predicates with XmlFormatter.PrettyPrint in serialize, XmlFormatter.Compact in deserialize, and ForceStringScalarEmitter in FlatFileStore for readable YAML literal block scalars
- 1. [Rule 3 - Blocking] XmlFormatter.RemoveElements implemented in Task 1 instead of Task 3
- 1. [Rule 3 - Blocking] Provider-level deserialize test replaced with YAML round-trip + SQL-level tests
- 1. [Rule 3 - Blocking] Fixed CommandBuilder parameter syntax

---

## v1.3 Permissions (Shipped: 2026-03-23)

**Phases completed:** 12 phases, 23 plans, 41 tasks

**Key accomplishments:**

- Five plain C# record DTOs + YamlDotNet serializer with ForceStringScalarEmitter proving round-trip fidelity for tilde, CRLF, HTML, quotes, and bang
- FileSystemStore with mirror-tree write/read: area/page/grid-row/paragraph YAML hierarchy, deterministic SortOrder ordering, GUID-suffix sibling dedup, and 13 passing tests proving all behaviors
- One-liner:
- Recursive multi-level page tree serialization via nested folder structure, with INF-03 long-path safety tests and a SafeGetDirectory overflow bug fix
- JWT-style GUID-only DW content serialization via ContentMapper, ReferenceResolver, and ContentSerializer connecting live DW content APIs to the existing FileSystemStore/YAML infrastructure
- Serialization pipeline verified live against Swift2.2: 73 YAML files produced from Customer Center tree (pageid=8385) with GUID identity, reference resolution, and mirror folder hierarchy
- ContentDeserializer writes YAML DTOs to DynamicWeb via GUID identity resolution, dependency-ordered saves (Page>GridRow>Paragraph), dry-run diffs, and cascade-skip error handling
- DeserializeScheduledTask DW add-in and 4 integration tests covering GUID roundtrip, idempotency, dry-run, and GUID preservation on INSERT
- NuGet AppStore packaging with Dynamicweb 10.23.9 package reference replacing DLL HintPaths, and ContentSerializer.Serialize() logs aggregate page/gridrow/paragraph counts after processing all predicates.
- E2E xunit integration tests for SerializeScheduledTask and DeserializeScheduledTask: OPS-01 asserts byte-identical YAML output vs direct ContentSerializer call, OPS-02 asserts successful roundtrip through the deserialize entry point.
- ColumnId-based paragraph attribution with column-aware filenames for lossless multi-column grid round-trip
- PropertyFields diff in dry-run, OutputDirectory validation at config-load and deserialize-time, AreaId XML documentation
- Atomic JSON ConfigWriter with temp+rename and centralized ConfigPathResolver replacing duplicated discovery logic
- 1. [Rule 3 - Blocking] Fixed NuGet package version for Microsoft.Extensions.FileProviders.Embedded
- Full settings edit screen with OutputDirectory (text+validation), LogLevel (dropdown), DryRun (checkbox), ConflictStrategy (dropdown), and disk-existence validation for OutputDirectory per D-05
- Predicate CRUD commands/queries with ConfigLoader zero-predicate fix, PageId round-tripping, and name uniqueness validation
- Predicate list and edit screens with area/page selectors, tree node wiring, and breadcrumb navigation
- ExportDirectory config field and SerializeSubtreeCommand that zips page subtree YAML via ContentSerializer reuse and returns FileResult for browser download
- Deserialize modal with FileUpload (.zip), 3-mode Select, overwrite Alert warning, and validate-then-apply command using dual ContentDeserializer passes (dry-run + real)
- SerializedPermission DTO and PermissionMapper that reads DW PermissionService, resolves role/group names, and integrates into ContentSerializer pipeline with 17 passing tests
- Permission restoration from YAML with role-name matching, group-name resolution via cached lookup, and Anonymous=None safety fallback for missing groups
- README Permissions section covering serialization scope, role/group resolution, source-wins restore, and Anonymous safety fallback for missing groups

---

## v1.1 Robustness (Shipped: 2026-03-20)

**Phases completed:** 1 phases, 2 plans, 0 tasks

**Key accomplishments:**

- (none recorded)

---

## v1.0 MVP (Shipped: 2026-03-20)

**Phases completed:** 5 phases, 10 plans, 2 tasks

**Key accomplishments:**

- (none recorded)

---

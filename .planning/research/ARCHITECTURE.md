# Architecture Research

**Domain:** Content serialization/sync tooling (DynamicWeb AppStore app)
**Researched:** 2026-03-19
**Confidence:** HIGH (Unicorn architecture well-documented; DynamicWeb 10 API verified via official docs)

## Standard Architecture

Content serialization systems like Unicorn decompose into five cooperating subsystems: a predicate that defines *what* to include, a tree walker that traverses the source, a serializer/deserializer that converts between object and file form, an identity resolver that maps items across environments, and an evaluator that decides *what to do* when comparing source to target. These subsystems are orchestrated by an entry point (scheduled task / sync coordinator).

### System Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Entry Points                               │
│  ┌──────────────────────────┐  ┌──────────────────────────────────┐ │
│  │  SerializeScheduledTask  │  │  DeserializeScheduledTask        │ │
│  └────────────┬─────────────┘  └───────────────┬──────────────────┘ │
└───────────────┼──────────────────────────────────┼───────────────────┘
                │                                  │
┌───────────────▼──────────────────────────────────▼───────────────────┐
│                         Sync Coordinator                             │
│  Reads config → invokes predicate → drives walker → calls evaluator  │
└────────┬────────────────────────────────────────────────────────────┘
         │
         │  uses all of:
         ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        Core Subsystems                              │
│                                                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │  Predicate   │  │  Tree Walker │  │  Evaluator   │              │
│  │  (config)    │  │  (traversal) │  │  (diff/sync) │              │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘              │
│         │                 │                  │                      │
│  ┌──────▼─────────────────▼──────────────────▼───────┐             │
│  │              Identity Resolver                      │             │
│  │   (GUID ↔ numeric ID mapping per environment)       │             │
│  └───────────────────────────────────────────────────┘             │
│                                                                     │
│  ┌────────────────────────────┐  ┌──────────────────────────────┐  │
│  │  Serializer (DW → YAML)    │  │  Deserializer (YAML → DW)    │  │
│  └────────────────────────────┘  └──────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
         │                                        │
         ▼                                        ▼
┌─────────────────────┐              ┌───────────────────────────────┐
│   DW Content APIs   │              │   File System (YAML files)    │
│  AreaService        │              │  /Areas/                      │
│  PageService        │              │    {area-name}/               │
│  GridService        │              │      {page-name}/             │
│  ParagraphService   │              │        {paragraph-name}.yml   │
└─────────────────────┘              └───────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Communicates With |
|-----------|----------------|-------------------|
| SerializeScheduledTask | DW entry point for export; extends `BaseScheduledTaskAddIn`, implements `Run()` | SyncCoordinator |
| DeserializeScheduledTask | DW entry point for import; extends `BaseScheduledTaskAddIn`, implements `Run()` | SyncCoordinator |
| SyncCoordinator | Loads config, validates predicates, orchestrates tree walk, reports progress/errors | Predicate, TreeWalker, Serializer, Deserializer |
| ConfigurationLoader | Parses standalone YAML/JSON config file; builds predicate rules | SyncCoordinator |
| Predicate | Evaluates include/exclude rules against a content item path | ConfigurationLoader, TreeWalker |
| TreeWalker | Recursively traverses DW content hierarchy (Area → Page → GridRow → Paragraph) using DW services | PageService, GridService, ParagraphService, Predicate |
| Serializer | Converts DW content objects to YAML model; writes mirror-tree files to disk | TreeWalker, YamlDotNet, FileSystem |
| Deserializer | Reads YAML files from disk; converts to DW content objects and writes via services | IdentityResolver, PageService, GridService, ParagraphService, YamlDotNet |
| IdentityResolver | Maps GUIDs to numeric IDs in the target environment; determines insert vs update | PageService (GUID lookup), Deserializer |
| Evaluator | Compares existing target state to incoming serialized state; applies source-wins rule | Deserializer (called during deserialize pipeline) |
| ContentModel (DTOs) | Plain C# classes representing serialized Area, Page, GridRow, Paragraph | Serializer, Deserializer, YamlDotNet |

## Recommended Project Structure

```
Dynamicweb.ContentSync/
├── Tasks/
│   ├── SerializeContentTask.cs      # BaseScheduledTaskAddIn — triggers serialization
│   └── DeserializeContentTask.cs    # BaseScheduledTaskAddIn — triggers deserialization
│
├── Configuration/
│   ├── ContentSyncConfiguration.cs  # Root config model (parsed from config file)
│   ├── ConfigurationLoader.cs       # Reads + validates config file on disk
│   └── Predicate.cs                 # Include/exclude rule evaluation
│
├── Serialization/
│   ├── SyncCoordinator.cs           # Orchestrates the full serialize or deserialize run
│   ├── ContentSerializer.cs         # DW objects → YAML model → files
│   ├── ContentDeserializer.cs       # Files → YAML model → DW objects via services
│   ├── TreeWalker.cs                # Recursive DW content tree traversal
│   └── IdentityResolver.cs          # GUID ↔ numeric ID resolution in target DB
│
├── Models/
│   ├── SerializedArea.cs            # YAML DTO for Website/Area
│   ├── SerializedPage.cs            # YAML DTO for Page + ItemType fields
│   ├── SerializedGridRow.cs         # YAML DTO for GridRow
│   ├── SerializedGridColumn.cs      # YAML DTO for GridColumn grouping
│   └── SerializedParagraph.cs       # YAML DTO for Paragraph + ItemType fields
│
└── Infrastructure/
    ├── FileSystemStore.cs           # Read/write YAML files; manages mirror-tree paths
    └── ContentSyncLogger.cs         # Structured logging wrapper
```

### Structure Rationale

- **Tasks/:** Thin DW integration layer — only entry-point wiring. No business logic here.
- **Configuration/:** Isolated so predicates can be tested independently of DW services.
- **Serialization/:** The core pipeline. SyncCoordinator, TreeWalker, Serializer, and Deserializer are separate because serialize and deserialize are genuinely asymmetric operations.
- **Models/:** Plain DTOs with no DW dependencies — owned by YamlDotNet serialization. Keeps the YAML schema decoupled from DW API churn.
- **Infrastructure/:** I/O concerns (file paths, logging) isolated from business logic.

## Architectural Patterns

### Pattern 1: Source-Wins Evaluator (Disk as Truth)

**What:** During deserialization, the evaluator does not diff or merge — it unconditionally applies the serialized state to the target. The only decision is insert vs update (based on GUID presence).
**When to use:** v1 full-sync scenarios where files are explicitly the authoritative source.
**Trade-offs:** Simple, predictable, and safe for CI/CD pipelines. Loses any manual edits made in target environment — users must understand this contract.

```csharp
// Evaluator logic — no merge, source always wins
if (identityResolver.TryResolveNumericId(serializedPage.Guid, out int existingId))
{
    serializedPage.NumericId = existingId;
    pageService.SavePage(MapToPage(serializedPage)); // UPDATE
}
else
{
    var newPage = pageService.SavePage(MapToPage(serializedPage)); // INSERT
    // numeric ID assigned by DW; GUID preserved from serialized file
}
```

### Pattern 2: Mirror-Tree File Layout

**What:** The folder structure on disk directly mirrors the content hierarchy. Each item is one `.yml` file; its location in the folder tree encodes its parent relationships.
**When to use:** Always — this is the core value proposition. Git diffs are readable and hierarchically navigable.
**Trade-offs:** Path lengths can become long for deeply nested content. Renaming items shifts file paths (but GUID identity survives renames).

```
/Areas/
  Swift/                         ← Area GUID encoded in area.yml
    area.yml
    Customer-Center/             ← Page folder (slug from page name)
      page.yml
      grid-row-1/
        paragraph-banner.yml
        paragraph-text.yml
      Sub-Page/
        page.yml
```

### Pattern 3: GUID-Based Identity Resolution

**What:** On deserialize, the IdentityResolver queries the target database by `PageUniqueId` (GUID) before any write. Found → update existing record. Not found → insert new record with DW-assigned numeric ID.
**When to use:** Always. This pattern is what makes the tool environment-agnostic.
**Trade-offs:** Requires one lookup query per item during deserialization. For large trees this adds latency, but is unavoidable — it is the correctness guarantee.

### Pattern 4: Predicate-Driven Inclusion

**What:** The config file specifies root paths (by Area name, page name, or GUID) to include or exclude. The Predicate component evaluates each item during tree traversal.
**When to use:** Configured once per project. Lets teams serialize only the content trees relevant to their work.
**Trade-offs:** Path-based predicates are simple but can require updates when content is renamed. GUID-based predicates are stable but less human-readable.

## Data Flow

### Serialization Flow (DW → Disk)

```
SerializeScheduledTask.Run()
    ↓
SyncCoordinator.Serialize()
    ↓
ConfigurationLoader → loads predicate rules
    ↓
TreeWalker.Walk(areaId)
    ├── AreaService.GetAreas() → for each area matching predicate
    ├── PageService.GetPagesByAreaID(areaId) → for each page
    │     PageService.GetPagesByParentID(pageId) → recurse children
    ├── GridService.GetGridRowsByPageId(pageId) → for each row
    └── ParagraphService.GetParagraphsByPage(pageId) → for each paragraph
         ↓ (item emitted to Serializer)
ContentSerializer.Serialize(dwObject)
    ├── Maps DW object → SerializedModel DTO
    ├── Includes ItemType custom fields
    └── Writes YAML via YamlDotNet
         ↓
FileSystemStore.Write(mirrorPath, yaml)
    └── Creates folder hierarchy + .yml file on disk
```

### Deserialization Flow (Disk → DW)

```
DeserializeScheduledTask.Run()
    ↓
SyncCoordinator.Deserialize()
    ↓
FileSystemStore.Enumerate(rootPath)
    └── Walks mirror-tree, yields .yml files in hierarchy order
         ↓
ContentDeserializer.Deserialize(yamlFile)
    ├── YamlDotNet → SerializedModel DTO
    ├── IdentityResolver.Resolve(dto.Guid)
    │     └── PageService/GridService/ParagraphService GUID lookup
    │           Found → numeric ID for UPDATE
    │           Not found → null → INSERT path
    ├── Evaluator applies source-wins rule
    └── Maps DTO → DW object → Service.Save()
         └── PageService.SavePage() / GridService.SaveGridRow()
             / ParagraphService.SaveParagraph()
```

### Key Data Flows

1. **GUID stability across environments:** GUIDs are read from DW on serialize and written back into DW on deserialize. The GUID is the only identifier present in both environments; numeric IDs are local to each.
2. **Parent-before-child ordering:** Tree walker emits and file system enumerates in breadth-first top-down order. Pages must exist before their GridRows; GridRows must exist before Paragraphs. The Deserializer must process items in this order.
3. **ItemType custom fields:** DynamicWeb pages and paragraphs can carry arbitrary key-value fields via ItemType. The ContentModel DTOs must capture these as a `Dictionary<string, object>` so they round-trip through YAML without the serializer needing to know the field schema.

## Component Build Order

Build in this sequence — each layer depends on the previous:

| Step | Component | Why This Order |
|------|-----------|----------------|
| 1 | ContentModel DTOs | No dependencies; defines the shared data contract |
| 2 | FileSystemStore | Only depends on Models; enables file I/O testing in isolation |
| 3 | ConfigurationLoader + Predicate | No DW dependency; can be unit-tested against sample configs |
| 4 | TreeWalker | Depends on DW services; integration-testable once DW instance available |
| 5 | Serializer (DW → YAML) | Depends on TreeWalker + FileSystemStore; validate output against expected YAML |
| 6 | IdentityResolver | Depends on DW services; pure lookup logic |
| 7 | Deserializer (YAML → DW) | Depends on IdentityResolver + FileSystemStore; final integration step |
| 8 | SyncCoordinator | Wires 3-7 together; testable end-to-end |
| 9 | Scheduled Tasks | Thin DW entry-point wiring; last to build |

## Anti-Patterns

### Anti-Pattern 1: Numeric IDs in YAML Files

**What people do:** Serialize the DW numeric page ID or paragraph ID into the YAML file and use it to match items on deserialize.
**Why it's wrong:** Numeric IDs are environment-specific. Deserializing into a fresh instance will create ID collisions or orphaned records. The whole point of the GUID-based identity resolver is to handle this.
**Do this instead:** Store only the GUID (`PageUniqueId`) as the canonical identifier in YAML. Numeric IDs may be stored informatively (for debugging) but must never be used as the match key.

### Anti-Pattern 2: Tight Coupling Between Serializer and DW Services

**What people do:** Call `PageService` or `ParagraphService` directly inside the Serializer/Deserializer rather than going through the TreeWalker or IdentityResolver.
**Why it's wrong:** Makes the serializer responsible for both traversal and conversion. Harder to test, harder to swap out, and leads to duplicate traversal logic.
**Do this instead:** TreeWalker owns traversal. Serializer only receives already-fetched DW objects and converts them. Deserializer only receives already-parsed DTOs and writes to DW via services.

### Anti-Pattern 3: File-Per-Run Snapshot vs. Mirror Tree

**What people do:** Serialize all content into a single bundle file (ZIP or flat directory) stamped with a timestamp.
**Why it's wrong:** Not git-friendly. Every sync creates a new file rather than updating existing ones. Diffs become unreadable. Cannot track individual item history.
**Do this instead:** Mirror-tree layout where each item has a stable path derived from its position in the content hierarchy. Git sees clean per-item changes.

### Anti-Pattern 4: Processing Children Before Parents on Deserialize

**What people do:** Walk files alphabetically or by modification time, deserializing a child paragraph before its parent page exists in the target.
**Why it's wrong:** `SaveParagraph()` requires a valid page ID. If the page doesn't exist yet, the insert fails or orphans the paragraph.
**Do this instead:** Enumerate mirror-tree depth-first, top-down. Process Areas → Pages → GridRows → Paragraphs strictly in that order, waiting for each parent's numeric ID (from `SavePage()` return value or GUID lookup) before writing children.

## Integration Points

### DynamicWeb Service Layer

| Service | Used For | Notes |
|---------|----------|-------|
| `AreaService` | Get all areas; save areas | `GetAreas()` is the tree root entry point |
| `PageService` | Get/save pages; GUID lookup; get child pages | `GetPagesByAreaID()` and `GetPagesByParentID()` drive tree walk; `GetPage()` by GUID for identity resolution |
| `GridService` | Get/save grid rows | `GetGridRowsByPageId()` for traversal; `SaveGridRow()` returns new ID |
| `ParagraphService` | Get/save paragraphs; GUID lookup | `GetParagraphsByPage()` for traversal; paragraphs reference `GridRowID` and `GridRowColumn` |
| `BaseScheduledTaskAddIn` | Entry-point wiring | Implement `Run()` method; use `[AddInName]`, `[AddInLabel]`, `[AddInDescription]` attributes |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| SyncCoordinator ↔ TreeWalker | Direct method call; yields `IDynamicWebContentItem` | Walker should yield items lazily (IEnumerable) to avoid loading full tree into memory |
| TreeWalker ↔ Predicate | Synchronous include/exclude check per item | Predicate is stateless; called once per item during walk |
| Serializer ↔ FileSystemStore | Path calculation + YAML string handoff | Serializer computes mirror path from item metadata; FileSystemStore handles actual I/O |
| Deserializer ↔ IdentityResolver | GUID-in, (numericId, exists)-out | IdentityResolver is the only component allowed to call DW services during deserialization identity phase |
| Deserializer ↔ DW Services | Save calls after identity resolution | Parent IDs must be resolved before child saves; strict ordering required |

## Sources

- [Unicorn GitHub README — architecture overview](https://github.com/SitecoreUnicorn/Unicorn) — HIGH confidence (official repo)
- [Unicorn Book — Working with Unicorn](https://unicorn.kamsar.net/working-with-unicorn.html) — HIGH confidence (maintainer-authored)
- [DynamicWeb PageService API](https://doc.dynamicweb.com/api/html/ad909b2a-ecca-81ed-757d-28030e39269f.htm) — HIGH confidence (official docs)
- [DynamicWeb GridService API](https://doc.dynamicweb.dev/api/Dynamicweb.Content.GridService.html) — HIGH confidence (official docs)
- [DynamicWeb BaseScheduledTaskAddIn](https://doc.dynamicweb.com/api/html/d161f262-b0dd-ab61-5264-9c88dc0295e7.htm) — HIGH confidence (official docs)
- [DynamicWeb Paragraphs developer docs](https://doc.dynamicweb.dev/manual/dynamicweb10/content/paragraphs.html) — HIGH confidence (official docs)
- [Creating Pages and Paragraphs via API](https://doc.dynamicweb.com/forum/development/creating-pages-and-paragraph-with-the-api?PID=1605) — MEDIUM confidence (official forum with code examples)
- [YamlDotNet GitHub](https://github.com/aaubry/YamlDotNet) — HIGH confidence (official library)

---
*Architecture research for: Content serialization/sync system (DynamicWeb equivalent of Sitecore Unicorn)*
*Researched: 2026-03-19*

# Project Research Summary

**Project:** Dynamicweb.ContentSync v1.2 Admin UI
**Domain:** DynamicWeb 10 admin UI extension — settings screens, predicate CRUD, context menu actions, zip packaging
**Researched:** 2026-03-21
**Confidence:** MEDIUM (stack HIGH; architecture MEDIUM; features MEDIUM; pitfalls MEDIUM-HIGH)

## Executive Summary

ContentSync v1.2 adds a DW10 admin UI layer over the existing v1.1 serialization/deserialization engine. The pattern is well-established: DW10 provides a declarative C# screen framework (`EditScreenBase`, `ListScreenBase`, `CommandBase`, `DataQueryModelBase`) that integrates with the Settings tree via `NavigationNodeProvider<AreasSection>` and into existing screens via `ScreenInjector<T>`. A single NuGet package addition — `Dynamicweb.Content.UI 10.23.9` — brings the full transitive chain needed (CoreUI, Application.UI, Content.UI). No Razor SDK change, no custom views, no heavyweight meta-packages. The existing serialization engine is reused without modification; context menu actions construct temporary `SyncConfiguration` objects and pass them to the existing `ContentSerializer`/`ContentDeserializer` via constructor injection.

The recommended build order is a strict four-phase sequence driven by dependency and ascending risk. Phases 1-3 (config infrastructure, settings screen, predicate CRUD) follow proven patterns from the official ExpressDelivery sample and carry LOW implementation risk. Phase 4 (context menu serialize/deserialize) carries HIGH risk because three key DW API behaviors are unverified at runtime: the exact screen type to inject into for the content tree, how `DownloadFileAction` handles large file streaming, and whether CoreUI provides a file upload component for the deserialize flow. Phase 4 must begin with isolated spikes before connecting to serialize/deserialize logic.

The single most critical architectural constraint is that `ContentSync.config.json` remains the source of truth — no database tables. Every UI command follows a read-modify-write pattern on the JSON file. Config concurrency (concurrent UI saves, manual edits during a UI save) is the top pitfall and must be addressed with `ReaderWriterLockSlim` + file locking + stale-write detection in the infrastructure phase, before any screen touches the file.

## Key Findings

### Recommended Stack

The existing stack (`.NET 8`, `Dynamicweb 10.23.9`, `YamlDotNet 13.7.1`, `Microsoft.Extensions.Configuration.Json 8.0.1`) is unchanged. A single line added to the project file is the complete stack change for v1.2:

```
<PackageReference Include="Dynamicweb.Content.UI" Version="10.23.9" />
```

This one package transitively delivers `Dynamicweb.CoreUI` (screen bases, commands, queries, actions), `Dynamicweb.Application.UI` (AreasSection, SettingsArea, ActionBuilder), and `Dynamicweb.Content.UI` itself (PageListScreen, PageEditScreen for context menu injection). ZIP packaging is handled by `System.IO.Compression.ZipFile` from the .NET 8 BCL — no external library needed. The Razor SDK and embedded asset infrastructure are explicitly not needed because all screens use DW's declarative C# screen builder pattern.

**Core technologies:**
- `Dynamicweb.Content.UI 10.23.9`: admin UI screens and content tree integration — minimum sufficient package; avoids pulling all of Ring1 (20+ packages including Ecommerce, Products, Insights)
- `System.IO.Compression` (.NET 8 BCL): zip packaging for serialize downloads and deserialize uploads — zero additional dependency
- `Microsoft.NET.Sdk` (existing, no change): no SDK change needed since all screens are declarative C#, not custom Razor views

See `STACK.md` for the full transitive dependency chain, the complete class-to-namespace mapping, and the verified list of what NOT to add.

### Expected Features

Research identified four feature areas with clear complexity and risk profiles.

**Must have (table stakes):**
- Settings screen (OutputDirectory, LogLevel) under Settings > Content > Sync — LOW risk, proven pattern
- Config reads/writes JSON file directly — file is source of truth, no DB
- Predicate list screen with add/edit/delete — LOW-MEDIUM risk, standard CRUD
- Predicate edit form (Name, Path, AreaId, Excludes) — LOW-MEDIUM risk
- "Serialize to ZIP" context menu on content tree pages — MEDIUM-HIGH risk (ScreenInjector target type unverified)
- Browser ZIP download from serialize action — MEDIUM risk (DownloadFileAction pattern is proven but streaming behavior for large files is undocumented)
- "Deserialize from ZIP" context menu with file upload — HIGH risk (upload mechanism unknown in CoreUI)
- Confirmation dialog before destructive deserialize — LOW risk

**Should have (differentiators for v1.2 stretch):**
- Config file path shown read-only on settings screen — LOW effort, useful debugging aid
- Area picker dropdown (populated from DW areas) on predicate edit — MEDIUM, friendlier than typing a numeric ID
- Serialize also saves ZIP to OutputDirectory alongside browser download — LOW effort

**Defer to v1.3:**
- Import as new subtree with GUID remapping — significant new logic not in existing codebase
- Predicate path browser (content tree picker for path field) — requires `OpenSlideOverAction` research
- Predicate preview (show which pages match predicates) — expensive for large trees
- Visual status indicator (last sync time) — requires new persistence mechanism
- Dry-run preview from context menu before applying deserialize — nice-to-have, not blocking

**Anti-features (never build):**
- DB-backed settings storage — violates config-as-source-of-truth constraint
- Lucene query expression UI for predicates — semantic mismatch; predicates are path rules, not index queries
- Git push from admin UI — fragile and unsafe; git is a developer tool

See `FEATURES.md` for full complexity tables, feature dependency graph, and MVP recommendation.

### Architecture Approach

The v1.2 architecture is a management UI layer over the existing config file with the serialization engine untouched. New components fit into three groups: (1) config infrastructure — `ConfigPathResolver` (extracted from duplicated scheduled task logic) and `ConfigWriter` (counterpart to existing `ConfigLoader`); (2) settings UI — screens, queries, commands following the ExpressDelivery CQRS-style screen pattern; (3) context menu actions — a `ScreenInjector` on the page overview screen that adds serialize/deserialize actions backed by commands that construct temporary `SyncConfiguration` objects and delegate to the existing engine.

**Major components:**
1. `ConfigPathResolver` — shared config file discovery, extracted from the 4-path search logic currently duplicated in both scheduled tasks
2. `ConfigWriter` — write `SyncConfiguration` back to JSON, counterpart to `ConfigLoader`
3. `ContentSyncSettingsNodeProvider` — `NavigationNodeProvider<AreasSection>` registering "Content Sync" with "Predicates" sub-node
4. Settings screens + queries + commands — `EditScreenBase` / `ListScreenBase` with file-backed read-modify-write commands; index-based predicate identity (`GetId() => $"{Index}"`) because predicates have no DB-assigned ID
5. `PageOverviewInjector` — `ScreenInjector<T>` adding serialize/deserialize context menu to content tree
6. `SerializeToZipCommand` — builds temp `SyncConfiguration`, runs `ContentSerializer`, zips output, returns `FileResult` download
7. `DeserializeFromZipCommand` — accepts uploaded zip, extracts to temp dir, builds temp `SyncConfiguration`, runs `ContentDeserializer`, cleans up

The critical design insight: context menu actions reuse `ContentSerializer`/`ContentDeserializer` by constructing a temporary `SyncConfiguration`. No code duplication needed. The serializer/deserializer already accept config via constructor and are agnostic to how that config was created.

See `ARCHITECTURE.md` for component boundaries, full data flow diagrams, implementation patterns with code samples, and the anti-patterns to avoid.

### Critical Pitfalls

1. **Config file concurrency — read-modify-write race** — concurrent UI saves or a UI save overlapping a manual edit silently overwrites changes. Prevent with `ReaderWriterLockSlim` + `FileShare.None` file locking + stale-write detection (compare file timestamp at save time to the timestamp when the UI loaded the config). Must be built before any screen touches the config file.

2. **NavigationNodeProvider chain incomplete — node visible but screen 404s** — all four artifacts (provider, screen, query, model) must exist and be discoverable by DW's assembly scanner before the first test. Build as a complete vertical slice; do not test the node in isolation.

3. **ScreenInjector targets wrong screen type** — silently injects nothing if the generic type parameter names the wrong DW screen class. Spike with a minimal "Test" action first; verify it appears on page nodes (not area nodes) before building the real action logic. The type name must be discovered via assembly inspection, not guessed.

4. **Zip temp file leaks on exception or process recycle** — temp directories from failed serialize operations accumulate on disk. Implement cleanup-on-startup to scan for `ContentSync-*` directories older than 1 hour. Do not rely solely on `finally` blocks (process recycle bypasses them).

5. **Config validation diverges between UI and scheduled task paths** — extract validation into a shared `ConfigValidator` called by both `ConfigLoader` and UI save commands. Never write config JSON without validating through the shared validator.

See `PITFALLS.md` for the full list including moderate pitfalls: zip-slip attack prevention on deserialize upload, permission guards on context menu commands, unknown JSON field preservation on UI save, and config caching in scheduled tasks.

## Implications for Roadmap

Based on combined research, a four-phase structure is recommended. Phases are ordered by dependency chain and ascending risk. The highest-uncertainty work is isolated in Phase 4 where accumulated DW UI experience from earlier phases can inform implementation spikes.

### Phase 1: Config Infrastructure + Settings Tree Node

**Rationale:** Zero DW UI framework risk. Config concurrency is the top pitfall and must be solved before any screen writes the file. The nav node registration is the most uncertain DW behavior in the settings UI work — surfacing it in Phase 1 allows early course correction without losing investment in the full screen stack.

**Delivers:** `ConfigPathResolver` (shared config file discovery), `ConfigWriter` (JSON write path with file locking), `ContentSyncSettingsNodeProvider` (nav node visible in DW admin tree), `ContentSyncNavigationNodePathProvider` (breadcrumb support), refactored scheduled tasks using shared `ConfigPathResolver`.

**Addresses:** Settings tree navigation (table stakes), config-file-as-source-of-truth constraint, scheduled task `FindConfigFile()` deduplication.

**Avoids:** Pitfall 1 (concurrency — locking built before first UI write), Pitfall 2 (node visible but 404 — validate registration early), Pitfall 10 (config caching — scheduled tasks load fresh from disk on each run).

**Research flag:** Needs a runtime spike in a running DW instance to confirm the `AreasSection` type parameter places the node under "Settings > Content" (not a top-level Settings section). Verify placement before building the screen stack.

### Phase 2: Settings Screen (OutputDirectory, LogLevel)

**Rationale:** Simplest screen pattern — single object, no collection management. Establishes the full screen/query/command cycle (EditScreenBase + DataQueryModelBase + CommandBase) that Phase 3 will replicate for predicates. Delivers immediate practical value: admins can edit config without touching the JSON file manually.

**Delivers:** `ContentSyncSettingsDataModel`, `ContentSyncSettingsQuery` (reads config, maps to model), `SaveContentSyncSettingsCommand` (read-modify-write with stale-write detection), `ContentSyncSettingsScreen` with OutputDirectory and LogLevel editors, shared `ConfigValidator` class.

**Addresses:** Settings edit (7 table stakes features from FEATURES.md), path validation on save, graceful handling of missing config file.

**Avoids:** Pitfall 6 (validation divergence — shared `ConfigValidator` built here), Pitfall 1 (stale-write detection implemented in save command).

**Research flag:** Standard pattern from ExpressDelivery sample — skip research phase.

### Phase 3: Predicate Management (CRUD List)

**Rationale:** Depends on Phase 2 screen pattern being validated. More complex than Phase 2 because it manages a collection with index-based identity rather than a single settings object. All patterns are proven; risk is implementation complexity, not API uncertainty.

**Delivers:** `ContentSyncPredicateDataModel` (index-based `IIdentifiable`), list and by-index queries, save/delete commands, `ContentSyncPredicateListScreen` with edit/delete context actions, `ContentSyncPredicateEditScreen` with Name, Path, AreaId, Excludes editors.

**Addresses:** Predicate list/add/edit/delete (7 table stakes features from FEATURES.md), AreaId dropdown from DW areas (differentiator), unknown JSON field preservation on save (Pitfall 7).

**Avoids:** Pitfall 3 (query UI mismatch — simple text fields, not Lucene expression UI), Pitfall 7 (multi-predicate config rendered as a list, not flattened into a single-screen form).

**Research flag:** Standard extension of Phase 2 patterns to a collection model — skip research phase. Index-based identity is a deliberate design choice; document it in the phase plan.

### Phase 4: Context Menu Actions (Serialize + Deserialize)

**Rationale:** Highest DW API uncertainty. Three behaviors require runtime discovery before implementation can proceed: (1) exact page tree screen type for ScreenInjector, (2) `DownloadFileAction` streaming behavior for large zips, (3) file upload mechanism for the deserialize flow. Built last because it has no hard dependency on Phases 2-3, and the DW UI knowledge accumulated in earlier phases reduces debugging time for the API discovery work.

**Delivers:** `PageOverviewInjector` (context menu on page tree), `SerializeToZipCommand` (temp config + ContentSerializer + zip + FileResult download), `DeserializeFromZipCommand` (upload + extract + temp config + ContentDeserializer), `DeserializeUploadScreen` (modal or prompt screen for zip upload). Context menu serialize built before deserialize (cleaner output path, lower risk first).

**Addresses:** Context menu serialize (5 table stakes features), context menu deserialize (5 of 6 table stakes — subtree GUID remapping deferred to v1.3).

**Avoids:** Pitfall 4 (temp file leaks — cleanup-on-startup), Pitfall 5 (wrong ScreenInjector target — spike first with minimal test action), Pitfall 8 (zip-slip — validate zip contents entry-by-entry before extraction), Pitfall 9 (unguarded commands — permission checks on all command handlers).

**Research flag:** REQUIRES phase-specific research. Three open questions must be answered via runtime inspection before implementation planning:
- What is the exact DW class name for the content tree page list/overview screen? (inspect `Dynamicweb.Content.UI.dll` on test instance)
- Does `DownloadFileAction` stream or buffer responses for large (>50MB) zip files? (test with large content tree)
- Does CoreUI provide a `FileUpload` editor in a modal/prompt screen, or is a custom API endpoint needed? (inspect `PromptScreenBase` and CoreUI editor registry)

### Phase Ordering Rationale

- Phase 1 before all others: `ConfigPathResolver` is a shared dependency of all commands and queries; concurrency protection must precede any UI writes; nav node registration uncertainty must be resolved cheaply before screen stack investment.
- Phase 2 before Phase 3: Settings screen establishes and validates the screen/query/command pattern that predicate screens replicate. Also validates the NuGet package and DW assembly scanning configuration.
- Phase 3 before Phase 4: Completes the settings UI milestone. Phase 4 is architecturally independent of 2-3 but isolating the highest-risk work last means earlier phases can proceed at full speed without blocking on API uncertainty.
- Serialize before deserialize within Phase 4: serialize has a known output path (DownloadFileAction is documented); deserialize's file upload mechanism is the single most uncertain DW behavior in the entire milestone.

### Research Flags

Needs dedicated research spike during planning:
- **Phase 1:** Exact section type for "Settings > Content" nav placement — verify `AreasSection` vs a more specific `ContentSection` by deploying node provider to test instance and observing placement
- **Phase 4:** Three open API questions (ScreenInjector target type, DownloadFileAction streaming, file upload in modal) — MUST be resolved before Phase 4 implementation planning begins

Standard patterns, skip research phase:
- **Phase 2:** Full ExpressDelivery sample reference available; `EditScreenBase` + `DataQueryModelBase` + `CommandBase` are well-documented
- **Phase 3:** Extension of Phase 2 patterns to a list/collection; index-based identity is a design decision, not a research question

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | NuGet dependency chain verified via package pages AND assembly reflection on test instance DLLs at `swift.test.forsync`; version pinning rationale solid |
| Features | MEDIUM | Settings and predicate features HIGH (proven patterns); context menu features LOW (no public sample for page tree injection or file upload in CoreUI modals) |
| Architecture | MEDIUM | Screen/query/command patterns HIGH (proven from ExpressDelivery); ScreenInjector target type LOW; file upload in modals LOW; temp config pattern HIGH (constructor injection already exists) |
| Pitfalls | MEDIUM-HIGH | Config concurrency and zip pitfalls verified via .NET docs; DW-specific ScreenInjector and DownloadFileAction streaming behaviors inferred from sparse official docs |

**Overall confidence:** MEDIUM

### Gaps to Address

- **ScreenInjector target type for content tree:** The DW page list/overview screen class name must be discovered by inspecting loaded assemblies on the test instance — cannot be reliably inferred from documentation. Address in Phase 4 research spike.
- **File upload in CoreUI modals:** No public example of `PromptScreenBase` with a `FileUpload` editor has been found. If CoreUI does not support this natively, the deserialize feature may require a custom API endpoint or admin page with a file input rather than a context menu action. Investigate in Phase 4 research spike; if unsupported, plan for the simpler alternative.
- **DownloadFileAction behavior for large responses:** Undocumented whether it streams or buffers the response. Test with a content tree of 100+ pages on the test instance. If buffering, a background task + polling pattern will be needed for large trees.
- **AreasSection vs ContentSection nav placement:** The ExpressDelivery sample uses `AreasSection` which maps to a top-level Settings section. "Settings > Content > Sync" may require a `ContentSection` or more specific type parameter. Verify by deploying the node provider in Phase 1 and observing actual placement.
- **Config concurrency scope:** `ReaderWriterLockSlim` guards in-process concurrency within a single DW worker process. Cross-process editing (developer editing the JSON file while DW admin is open) is an accepted v1.2 limitation. Document explicitly.

## Sources

### Primary (HIGH confidence)
- NuGet: `Dynamicweb.Content.UI`, `Dynamicweb.Application.UI`, `Dynamicweb.CoreUI` — dependency chain and class locations verified via package pages and assembly reflection
- Assembly reflection on test instance: `C:\Projects\Solutions\swift.test.forsync\Swift2.1\Dynamicweb.Host.Suite\bin\Debug\net8.0\` — confirmed class names and namespaces
- ExpressDelivery sample: `C:\Projects\temp\dwextensionsample\Samples-main\ExpressDelivery\` — verified NavigationNodeProvider, EditScreen, ListScreen, Commands, Queries, ScreenInjector patterns
- [DW10 AppStore App Guide](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) — official NavigationNodeProvider, EditScreen, Command, Query examples
- [DW10 CoreUI Actions API](https://doc.dynamicweb.dev/api/Dynamicweb.CoreUI.Actions.Implementations.html) — DownloadFileAction, RunCommandAction, ConfirmAction confirmed
- [.NET ZipArchive](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchive) — zip creation and entry-level validation (zip-slip prevention)
- [.NET ReaderWriterLockSlim](https://learn.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) — thread-safe file access

### Secondary (MEDIUM confidence)
- [DW10 Screen Types](https://doc.dynamicweb.dev/documentation/extending/administration-ui/screentypes.html) — screen type concepts; sparse on content tree specifics
- [DW10 ScreenInjector API](https://doc.dynamicweb.dev/api/Dynamicweb.CoreUI.Screens.ScreenInjector-1.html) — existence confirmed; target type discovery not documented
- [DW10 NavigationNode API](https://doc.dynamicweb.dev/api/Dynamicweb.CoreUI.Navigation.NavigationNode.html) — ContextActionGroups property confirmed
- Existing ContentSync codebase — verified `ContentSerializer`/`ContentDeserializer` constructor signatures enabling temp-config reuse pattern

### Tertiary (LOW confidence — needs runtime validation)
- Context menu injection on content tree page nodes — no public sample; ScreenInjector pattern inferred from `OrderOverviewInjector` in ExpressDelivery
- File upload mechanism in CoreUI modals — not found in API docs or samples; needs investigation in Phase 4 spike
- `DownloadFileAction` streaming behavior for large files — undocumented; test required

---
*Research completed: 2026-03-21*
*Ready for roadmap: yes*

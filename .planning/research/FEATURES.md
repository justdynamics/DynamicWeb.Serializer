# Feature Research: v1.2 Admin UI Integration

**Domain:** DynamicWeb 10 Admin UI Extension (Settings Screens, Query Config, Context Menu Actions)
**Researched:** 2026-03-21
**Confidence:** MEDIUM (patterns verified from official DW sample code + API docs; content tree injection patterns LOW confidence — no public sample for page tree context menus)

---

## Feature Landscape

### Area 1: Settings Screen (Settings > Content > Sync)

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Edit OutputDirectory path | Core config — where YAML files live on disk | Low | Text input editor via `EditorFor()`. Map to `SyncConfiguration.OutputDirectory`. |
| Edit LogLevel | Standard operational config | Low | Dropdown/Select editor with options: debug, info, warn, error. |
| Dry-run toggle | Already supported in config — must be surfaceable | Low | Boolean toggle editor. |
| Save persists to config file | Config file is source of truth per PROJECT.md constraint | Medium | `CommandBase<T>.Handle()` must write JSON to disk, not to DB. Unlike typical DW patterns that use `SqlUpdate`. |
| Settings appear in nav tree at Settings > Content > Sync | DW convention for app settings — AreasSection node provider | Low | `NavigationNodeProvider<AreasSection>` pattern proven in ExpressDelivery sample. |
| Path validation on save | OutputDirectory must be valid filesystem path | Low | Validate in save command, return `CommandResult.ResultType.Invalid` with message. |
| Load existing values from config file | Screen must show current state, not defaults | Medium | `DataQueryModelBase<T>` reads from config file on disk. Must handle missing file gracefully. |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Visual status indicator (last sync time, result) | Gives admins confidence the tool is working without checking logs | Medium | Would require persisting last-run metadata. Could use a simple status file alongside config. |
| Config file path display (read-only) | Shows admins where the config lives, aids debugging | Low | Informational field on settings screen. |
| "Test connection" for OutputDirectory | Validates the directory is writable before actual sync | Low | Button action that tries to create/delete a temp file. |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| DB-backed settings storage | Defeats the config-file-as-source-of-truth constraint. Config must be in source control. | Read/write the JSON config file directly. The file is the canonical store. |
| Inline predicate editing on settings screen | Predicates are a sub-node concern with their own CRUD lifecycle. Mixing into settings creates a cluttered, non-standard UI. | Separate predicate management as a sub-node (Area 2). |
| Output directory file browser | DW CoreUI has no standard file system browser component. Building one is scope creep. | Text input with validation on save. |

---

### Area 2: Query/Predicate Management Sub-Node

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| List all predicates | Standard list screen showing configured predicates | Low | `ListScreenBase<PredicateDataModel>` with columns: Name, Path, AreaId. Proven pattern from ExpressDelivery sample. |
| Add new predicate | CRUD — users must be able to create new include rules | Medium | `EditScreenBase<PredicateDataModel>` with editors for Name, Path, AreaId, Excludes. |
| Edit existing predicate | CRUD — modify path, excludes, name | Medium | Same edit screen, loaded via `DataQueryModelBase<T>` with predicate index/name as key. |
| Delete predicate | CRUD — remove a predicate with confirmation | Low | `ActionBuilder.Delete()` with `ConfirmAction`. Must prevent deleting last predicate (config requires at least one). |
| Excludes management per predicate | Core predicate feature — exclude subpaths from include | Medium | List of strings on edit screen. Could use multi-value text editor or separate list. |
| Persist changes to config file | All changes must write back to the JSON config file | Medium | Save command serializes full `SyncConfiguration` back to JSON. Must preserve formatting/comments — use `JsonSerializer` with `WriteIndented`. |
| Sub-node appears under Sync in nav tree | DW convention for related settings with their own list | Low | `NavigationNodeProvider` with `HasSubNodes = true` on parent, sub-node returns predicate list screen. |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Area picker dropdown (populated from DW areas) | AreaId is numeric — a dropdown is friendlier than typing a number | Medium | Query `Dynamicweb.Content.Services.Areas` to populate `Select` editor options. |
| Path browser (pick from content tree) | Path is a content tree path — browsing is friendlier than typing | High | Would need `OpenSlideOverAction` with a content tree selector. DW may have this built-in for page selection. Needs phase-specific research. |
| Predicate preview (show which pages match) | Visual confirmation that predicates are correct before syncing | High | Run `ContentPredicateSet.ShouldInclude()` against all pages and display results. Useful but expensive for large trees. |
| Drag-and-drop reordering | Predicates evaluated in order — reordering matters for overlapping scopes | Medium | Currently predicates use OR logic (any match includes), so order does not matter. Only relevant if switching to first-match-wins semantics. |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| DW Lucene Query expression UI for predicates | PROJECT.md mentions "DW query expression UI" but predicates are path-based include/exclude rules, not search queries. Lucene query UI is for full-text search, not content tree path matching. Forcing it would be a poor conceptual fit. | Use simple text fields for Path and Excludes with an AreaId dropdown. This matches the actual predicate model (`PredicateDefinition`: Name, Path, AreaId, Excludes). |
| Complex boolean predicate builder | Predicate logic is simple: include paths, exclude sub-paths, OR across predicates. A visual query builder adds complexity for no gain. | Keep the text-based path model. Users who need complex predicates edit the JSON file directly. |
| Separate "test" and "production" predicate sets | Multiple config sets per environment multiply complexity. One config file per environment is sufficient. | Config file is per-deployment. Different environments have different config files. |

---

### Area 3: Context Menu Serialize Action

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| "Serialize to ZIP" option in page context menu | Core ad-hoc operation — serialize a specific subtree on demand | High | Requires `ScreenInjector` on the page tree screen or a `NavigationNodeProvider` context action. DW's `NavigationNode.ContextActionGroups` supports this. |
| Serialize the selected page and all children | Subtree serialization is the unit of work | Medium | Reuse existing `ContentSerializer` with a temporary `SyncConfiguration` scoped to the selected node's path. |
| Package result as ZIP file | Portable output format for download | Low | `System.IO.Compression.ZipFile.CreateFromDirectory()`. Standard .NET. |
| Browser download of ZIP | Users expect to get the file immediately | Medium | `DownloadFileAction.Using<SerializeCommand>()` — DW CoreUI has `DownloadFileAction` specifically for this. Command creates ZIP, returns file path. |
| Progress/status feedback | Serialization may take seconds — user needs to know it's working | Medium | Could use `OpenOutputViewerAction` for streaming log output, or `ShowMessageAction` for completion toast. |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Also save ZIP to disk (configurable path) | Allows automated pickup by CI/CD pipelines alongside browser download | Low | Write ZIP to OutputDirectory alongside browser download. Configurable via settings. |
| Dry-run serialize (preview without download) | See what would be serialized before committing | Low | Reuse existing dry-run mode. Show results in a dialog or output viewer. |
| Serialize to OutputDirectory (no ZIP) | Direct disk write matches scheduled task behavior | Low | Alternative action: write YAML directly to OutputDirectory, skip ZIP packaging. |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Serialize to git directly | Git operations from a web admin UI are fragile and unsafe. Git is a developer tool. | Serialize to disk or ZIP. Git operations happen in the developer's local workflow. |
| Selective field serialization from UI | Adds complexity for marginal value. Field exclusions belong in config. | Use config-file field exclusions if implemented. |
| Serialize across multiple areas | Scope of a context menu action should be the node you right-clicked, not a cross-cutting operation. | Context menu serializes the clicked subtree only. Full-sync uses scheduled tasks. |

---

### Area 4: Context Menu Deserialize Action

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| "Deserialize from ZIP" option in page context menu | Core ad-hoc operation — import content from a ZIP package | High | Same injection pattern as serialize. Must handle file upload first. |
| Upload ZIP via browser | Standard file upload UX | High | DW CoreUI does not have an obvious built-in file upload action. May need a `PromptScreenBase` with a file upload editor, or a custom API endpoint. This is the highest-risk feature — needs phase-specific research. |
| Extract ZIP to temp directory | Standard unpackaging | Low | `ZipFile.ExtractToDirectory()`. Clean up temp dir after import. |
| Deserialize into content tree | Reuse existing `ContentDeserializer` | Medium | Point deserializer at extracted YAML files. |
| Choice: overwrite existing vs import as subtree | Users need control over how imported content relates to existing content | High | Overwrite = match by GUID and update. Import as subtree = generate new GUIDs and insert under selected node. Subtree import requires GUID remapping — significant new logic. |
| Confirmation before applying | Destructive operation — must confirm | Low | `ConfirmAction` wrapping the command. Show summary of what will be changed. |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Dry-run preview before deserialize | Show what would change before applying — builds trust | Medium | Reuse existing dry-run mode. Display diff in `OutputViewerScreen` or dialog. |
| Conflict report | Show items that exist and will be overwritten | Medium | Compare incoming GUIDs against existing DB content. List matches. |
| Undo/rollback | Reverse a bad deserialize | High | Would require snapshotting DB state before import. Too complex for v1.2. |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Drag-and-drop file onto content tree | Non-standard DW interaction pattern. Would require custom JavaScript in admin UI. | Standard context menu action with file upload prompt. |
| Auto-merge conflicting content | Merge semantics for structured CMS content are undefined. Source-wins or skip are the only safe options. | Source-wins overwrite, or skip-existing option. |
| Deserialize from URL | Adds network complexity, security concerns, and error handling for no clear UX advantage over file upload. | Upload ZIP from local disk. |

---

## Feature Dependencies

```
[Settings Screen]
    reads/writes --> [Config File on Disk]
    provides config for --> [Context Menu Serialize]
    provides config for --> [Context Menu Deserialize]

[Predicate Management Sub-Node]
    reads/writes --> [Config File on Disk]
    must exist before --> [Context Menu Serialize] (needs predicates to scope)
    must exist before --> [Context Menu Deserialize] (optional — ad-hoc can use temp config)

[Context Menu Serialize]
    depends on --> [ContentSerializer] (existing v1.0 code)
    depends on --> [Config File] (for OutputDirectory, predicates)
    uses --> [DownloadFileAction] (DW CoreUI built-in)

[Context Menu Deserialize]
    depends on --> [ContentDeserializer] (existing v1.0 code)
    depends on --> [File Upload mechanism] (RESEARCH NEEDED)
    depends on --> [ZIP extraction] (.NET standard)

[Config File on Disk]
    read by --> [Scheduled Tasks] (existing v1.0)
    read by --> [Settings Screen] (new v1.2)
    read by --> [Predicate Sub-Node] (new v1.2)
    written by --> [Settings Screen] (new v1.2)
    written by --> [Predicate Sub-Node] (new v1.2)
    manually editable --> [Developer in IDE] (must remain valid)
```

### Critical Dependency: Config File Concurrency

The config file is read by scheduled tasks, written by the UI, and manually edited by developers. There is no locking mechanism. Concurrent writes could corrupt the file.

**Mitigation:** Read-modify-write with file locking (`FileShare.None` during write). Accept that manual edits while UI is saving may conflict — document this as a known limitation.

---

## Complexity Assessment

| Feature Area | Table Stakes Count | Complexity | Risk |
|--------------|-------------------|------------|------|
| Settings screen | 7 features | Low-Medium | LOW — proven DW pattern from ExpressDelivery sample |
| Predicate sub-node | 7 features | Medium | LOW — standard CRUD list/edit screens |
| Context menu serialize | 5 features | Medium-High | MEDIUM — ScreenInjector on content tree is unverified |
| Context menu deserialize | 6 features | High | HIGH — file upload mechanism unknown, subtree import logic is new |

---

## MVP Recommendation for v1.2

### Phase 1: Settings + Predicates (build first)

1. Settings screen with OutputDirectory, LogLevel, DryRun editors
2. Predicate list screen with add/edit/delete
3. Predicate edit screen with Name, Path, AreaId, Excludes
4. All changes persist to config JSON file
5. Navigation node under Settings > Content > Sync

**Rationale:** Lowest risk, proven patterns, delivers immediate value. Config file read/write is the foundation all other features need.

### Phase 2: Context Menu Serialize (build second)

1. "Serialize subtree" context menu action on content tree page nodes
2. Serialize selected page + children to temp directory
3. ZIP and trigger browser download via `DownloadFileAction`
4. Optional: also save to OutputDirectory

**Rationale:** Medium risk. Depends on ScreenInjector working with the content tree. `DownloadFileAction` is a proven DW pattern.

### Phase 3: Context Menu Deserialize (build last)

1. "Deserialize from ZIP" context menu action
2. File upload via prompt screen
3. Extract and deserialize with overwrite mode only (skip subtree import for v1.2)
4. Confirmation dialog before applying

**Rationale:** Highest risk. File upload mechanism needs investigation. Subtree import with GUID remapping should be deferred to v1.3.

### Defer to v1.3

| Feature | Why Defer |
|---------|-----------|
| Import as subtree (GUID remapping) | New logic not in existing codebase. Complex and risky. |
| Predicate preview (show matching pages) | Nice-to-have, not blocking. |
| Path browser (content tree picker for predicate paths) | Requires `OpenSlideOverAction` with page selector. Needs research. |
| Visual status indicator (last sync time) | Requires new persistence mechanism. |

---

## DW CoreUI Pattern Reference

### Key Classes for Implementation

| Pattern | DW Class | Our Usage |
|---------|----------|-----------|
| Settings nav node | `NavigationNodeProvider<AreasSection>` | Register "Sync" node under Settings > Content |
| Settings edit screen | `EditScreenBase<SyncSettingsDataModel>` | OutputDirectory, LogLevel, DryRun editors |
| Predicate list | `ListScreenBase<PredicateDataModel>` | List predicates with context actions |
| Predicate edit | `EditScreenBase<PredicateDataModel>` | Add/edit predicate fields |
| Data model | `DataViewModelBase` with `[ConfigurableProperty]` | Field labels and editor hints |
| Load data | `DataQueryModelBase<T>` / `DataQueryListBase<T, TSource>` | Read from config file |
| Save data | `CommandBase<T>` | Write to config file |
| Delete action | `ActionBuilder.Delete()` with command | Delete predicate with confirmation |
| Nav path | `NavigationNodePathProvider<T>` | Breadcrumb navigation |
| Content tree action | `NavigationNode.ContextActionGroups` | Serialize/Deserialize context menu items |
| Screen injection | `ScreenInjector<T>` (if needed) | Inject actions into page edit screens |
| File download | `DownloadFileAction.Using<TCommand>()` | ZIP download trigger |
| Dialog prompt | `PromptScreenBase<T>` | File upload for deserialize |
| Confirmation | `ConfirmAction.For(command, title, message)` | Confirm destructive deserialize |
| Composite action | `CompositeAction(action1, action2)` | Chain close-popup + reload after save |
| Mapping config | `MappingConfigurationBase` | Map domain models to view models |

### NuGet Dependencies Required

The project currently references `Dynamicweb` (10.23.9). The admin UI features require:
- `Dynamicweb.CoreUI` — for screens, actions, commands, queries (transitive via `Dynamicweb`)
- `Dynamicweb.Application.UI` — for `AreasSection`, `SettingsArea` (transitive via `Dynamicweb`)
- Possibly `Dynamicweb.Content.UI` — for content tree screen types to inject into

**Verification needed:** Confirm these are already transitive dependencies of the `Dynamicweb` meta-package, or if explicit references are required.

---

## Open Questions / Research Gaps

| Question | Impact | Confidence |
|----------|--------|------------|
| How to add context menu actions to content tree page nodes (not settings tree)? | Blocks context menu features | LOW — no public sample found. `NavigationNodeProvider` for content tree vs settings tree may differ. May need `ScreenInjector` on page edit/overview screen instead. |
| Does DW CoreUI have a file upload editor component? | Blocks deserialize feature | LOW — not found in API docs. May need custom API endpoint + JavaScript. |
| Is `Dynamicweb.Content.UI` needed as explicit dependency? | Affects project setup | LOW — likely transitive but unverified. |
| Can `DownloadFileAction` handle large ZIP files (100MB+)? | Affects serialize feature for large trees | LOW — undocumented. Likely streams but unverified. |
| How to get current page context (PageId, AreaId) in a context menu command? | Required for scoped serialize/deserialize | MEDIUM — command likely receives context via query parameters, but exact mechanism needs verification. |

---

## Sources

- [DW10 Screen Types and Elements](https://doc.dynamicweb.dev/documentation/extending/administration-ui/screentypes.html) — HIGH confidence (official docs)
- [DW10 AppStore App Guide](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) — HIGH confidence (official docs, includes NavigationNodeProvider + EditScreen patterns)
- [DW10 CoreUI Actions API](https://doc.dynamicweb.dev/api/Dynamicweb.CoreUI.Actions.Implementations.html) — HIGH confidence (official API reference, confirms DownloadFileAction exists)
- [DW10 NavigationNode API](https://doc.dynamicweb.dev/api/Dynamicweb.CoreUI.Navigation.NavigationNode.html) — HIGH confidence (official API reference, confirms ContextActionGroups property)
- [DW10 ScreenInjector API](https://doc.dynamicweb.dev/api/Dynamicweb.CoreUI.Screens.ScreenInjector-1.html) — MEDIUM confidence (official API, sparse documentation)
- [ExpressDelivery Sample Code](https://github.com/dynamicweb/Samples) — HIGH confidence (official DW sample, verified locally)
- [DW10 DownloadFileAction API](https://doc.dynamicweb.dev/api/Dynamicweb.CoreUI.Actions.Implementations.DownloadFileAction.html) — HIGH confidence (official API reference)
- Existing ContentSync codebase (`SyncConfiguration`, `ConfigLoader`, `ContentPredicate`) — HIGH confidence (local verified code)

---

*Feature research for: DynamicWeb 10 Admin UI Integration (Dynamicweb.ContentSync v1.2)*
*Researched: 2026-03-21*

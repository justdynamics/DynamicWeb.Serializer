# Domain Pitfalls

**Domain:** DynamicWeb admin UI integration, query configuration, context menus, zip packaging
**Researched:** 2026-03-21
**Milestone:** v1.2 Admin UI
**Confidence:** MEDIUM-HIGH (DW10 extensibility patterns verified via official docs; config file concurrency and zip pitfalls verified via .NET documentation; some DW-specific pitfalls like ScreenInjector behavior based on limited public docs — flagged where uncertain)

---

## Critical Pitfalls

Mistakes that cause rewrites, data loss, or major integration failures.

### Pitfall 1: Config File Concurrency — UI Writes Corrupt Manual Edits (and Vice Versa)

**What goes wrong:** The admin UI writes to `ContentSync.config.json` at the same time a developer is editing it manually, or two admin users save settings simultaneously. One write silently overwrites the other. Worse: the UI reads a stale copy, the user edits one field, and the save overwrites all other fields with stale values — destroying changes made by another user or manual edit moments earlier.

**Why it happens:** The existing `ConfigLoader.Load()` does a simple `File.ReadAllText()` with no file locking, no ETags, no last-modified checking. Adding an admin UI that does `File.WriteAllText()` creates a classic read-modify-write race condition. JSON config files have no built-in concurrency control.

**Consequences:**
- Predicate definitions silently deleted when UI save overwrites manual additions
- OutputDirectory path reverted to old value after concurrent edits
- Config file left in a corrupted state (partial JSON) if two writes overlap at the byte level
- Developer trust in the config file erodes — "my manual changes keep disappearing"

**Prevention:**
- Use `ReaderWriterLockSlim` as an in-process guard for all config file reads and writes. Every path that touches the config file (ConfigLoader, UI save commands, scheduled tasks) must acquire the lock.
- Implement file-level locking via `FileStream` with `FileShare.None` during writes to prevent cross-process corruption.
- On UI save: read the file, compare a hash/timestamp against the version the UI loaded, reject the save with a clear message if the file changed ("Config file was modified externally since you loaded it. Reload and try again.").
- Store a `lastModified` timestamp in the UI model so the save command can detect stale writes.
- NEVER do read-then-write without holding a lock for the entire duration.

**Detection:** Config file changes that "revert" after admin UI saves. Scheduled tasks picking up different config than what the UI shows. JSON parse errors in logs.

**Phase to address:** Config management layer — must be the FIRST thing built before any UI screen touches the config file.

---

### Pitfall 2: NavigationNodeProvider Registration Renders but Actions Throw 404

**What goes wrong:** The tree node appears correctly under Settings > Content > Sync, but clicking it produces a blank screen or a 404-style error. The node was registered with `NavigateScreenAction.To<MyScreen>()` but the screen class is not discovered by DW's assembly scanner, or the query class referenced by the screen is not registered.

**Why it happens:** DW10's admin UI is a decoupled frontend that calls backend endpoints. The `NavigationNodeProvider` registers the node structure, but the screen, query, and command classes must ALL be independently discoverable by DW's reflection-based add-in system. Missing any one piece (screen class, query class, model class, mapping configuration) causes the chain to break silently. Additionally, the class must be in an assembly that DW's add-in scanner loads — if the DLL is not in the right directory or the namespace does not match expectations, it is invisible.

**Consequences:**
- Tree node visible but clicking produces blank panel or JavaScript error in browser console
- Screen loads but shows no data (query not found)
- Save button does nothing (command not found)
- Difficult to debug because errors appear in browser console, not server logs

**Prevention:**
- Verify the assembly is loaded by DW's scanner: check that the NuGet package places the DLL in the correct location (typically the application's bin folder or a plugins directory).
- Implement ALL four pieces before testing any single one: NavigationNodeProvider, Screen class (inheriting `EditScreenBase` or `ListScreenBase`), Query class (inheriting `DataQueryModelBase` or similar), and Model class.
- If using `CommandBase` for save operations, ensure the command is also in the scanned assembly.
- Implement a `MappingConfigurationBase` subclass if mapping between domain models and view models — DW10 requires explicit mapping registration.
- Test the complete chain end-to-end in a running DW instance early. Unit tests cannot validate DW's assembly scanning.

**Detection:** Node appears in tree but click does nothing. Browser DevTools network tab shows 404 or 500 on the screen endpoint.

**Phase to address:** Admin UI tree registration phase — build all four artifacts (provider, screen, query, model) together as a single vertical slice.

---

### Pitfall 3: Query Expression UI Reuse — Coupling to Index-Specific Queries Instead of UI Queries

**What goes wrong:** The developer attempts to reuse DW's Lucene index query builder UI to define predicates, but the query expression UI is tightly coupled to index repositories. ContentSync predicates are path-based (include `/CustomerCenter`, exclude `/CustomerCenter/Archive`) — they are NOT index queries. Attempting to map the query expression UI to config-file predicates creates a semantic mismatch: the UI expects index fields, operators, and values, but predicates need path patterns and area IDs.

**Why it happens:** DW10 has TWO distinct query systems: (1) Index queries that search Lucene indexes with field/operator/value expressions, and (2) UI queries (`DataQueryBase` subclasses) that provide data to screens. The Lucene query builder UI is designed for index queries and expects expressions against indexed fields. ContentSync predicates are neither — they are simple path-matching rules. Trying to force predicates into the query expression model creates an impedance mismatch.

**Consequences:**
- Enormous complexity to make the query UI understand path-based predicates
- UI shows irrelevant operators (Contains, MatchAny) that do not apply to path matching
- Users confused by query builder UI when all they need is a simple path input with include/exclude
- Maintenance burden of keeping the query-to-predicate translation layer working across DW updates

**Prevention:**
- Do NOT reuse the Lucene query expression UI for predicate management. Build a simpler, purpose-built UI.
- Use a custom `EditScreenBase` with a list of predicate rows, each containing: Name (text), Path (text), AreaId (dropdown from available areas), and Excludes (list of path strings).
- If you want to offer a "browse for path" experience, use a content tree picker (if DW provides one as a UI component) rather than a query builder.
- Keep the predicate model simple: it maps directly to the existing `PredicateDefinition` record (Name, Path, AreaId, Excludes). No translation layer needed.

**Detection:** Excessive code complexity in the query-to-predicate mapping. UI showing operators that make no sense for path matching.

**Phase to address:** Predicate management UI design — decide the UI approach BEFORE writing any screen code.

---

### Pitfall 4: Zip Packaging — Temp Files Leaked on Exception or Process Recycle

**What goes wrong:** The serialize-to-zip context menu action creates a temp directory, serializes content into it, creates a zip file, and streams it to the browser. If any step after temp directory creation throws an exception, the temp directory and its files are never cleaned up. Over time, or after repeated failures, the server accumulates gigabytes of orphaned temp directories. Worse: if the IIS/Kestrel worker process recycles mid-operation, the temp files are orphaned permanently because the `finally` block never executes.

**Why it happens:** Developers put cleanup in a `finally` block or `IDisposable.Dispose()`, which handles normal exceptions but not process kills or app pool recycles. Windows file locks from `ZipFile.CreateFromDirectory()` or `FileStream` also prevent cleanup if the zip creation fails partway through — the files are locked and `Directory.Delete()` throws, swallowing the original exception.

**Consequences:**
- Disk space exhaustion on production servers
- Temp directory fills up, causing other system operations to fail
- Orphaned YAML files in temp directories potentially expose content data

**Prevention:**
- Use `Path.GetTempPath()` combined with a unique subdirectory name (e.g., `ContentSync-{Guid}`) for temp directories.
- Implement a cleanup-on-startup pattern: when the app initializes, scan the temp directory for any `ContentSync-*` directories older than 1 hour and delete them. This handles process-recycle orphans.
- Use `try/finally` for the happy path, but do NOT rely on it as the sole cleanup mechanism.
- For zip creation, use `ZipArchive` with `ZipArchiveMode.Create` on a `FileStream` or `MemoryStream` directly, rather than `ZipFile.CreateFromDirectory()` which requires all files to exist on disk first. This avoids the temp-directory-to-zip step entirely if the content tree is small enough to fit in memory.
- Set a size limit on ad-hoc serialization (e.g., max 1000 pages) to prevent multi-GB temp directories.
- After streaming the zip to the browser response, delete the temp directory in a background task (not in the response pipeline).

**Detection:** Growing disk usage in temp directory. `ContentSync-*` directories that persist across app restarts.

**Phase to address:** Context menu action implementation — design the temp file lifecycle before writing the serialize action.

---

### Pitfall 5: ScreenInjector Context Menu — Injecting Into Wrong Screen or Missing the Target Type

**What goes wrong:** The `ScreenInjector<T>` pattern is used to inject context menu actions into the content tree's page list/edit screen, but the type parameter `T` targets the wrong screen class. The context menu action either never appears (wrong target), appears on ALL screens (overly broad target), or appears but crashes because it receives the wrong data context (e.g., expects a page ID but receives an area ID).

**Why it happens:** DW10's `ScreenInjector<T>` uses the generic type parameter to match the target screen. The content tree has multiple screen types: area list, page list, page edit, paragraph edit. Injecting into the wrong one is silent — no error, the action simply does not appear. Getting the right type requires knowing DW's internal screen class hierarchy, which is poorly documented publicly.

**Consequences:**
- Context menu action invisible (wrong target type) — developer wastes hours debugging
- Context menu action appears on wrong items (area-level instead of page-level)
- Action handler receives wrong model/ID, causing runtime exceptions or operating on wrong content
- Action appears but is greyed out because required permissions are not configured

**Prevention:**
- Inspect DW's built-in screen classes in the decompiled assembly (or API docs) to identify the exact screen type for the content tree page context menu. Do not guess the type name.
- Start with a minimal injection that adds a single "Test" context menu item. Verify it appears on the correct node type (page, not area) before building the real action logic.
- The context menu action must provide a `CommandBase` subclass that handles the action — the action is the UI trigger, the command is the backend handler.
- Test the injection on BOTH root-level pages and nested child pages to ensure the context menu appears consistently throughout the tree.
- Check whether `ScreenInjector` requires a specific NuGet package reference (e.g., `Dynamicweb.CoreUI` or `Dynamicweb.Apps.UI`).

**Detection:** Context menu item not visible. Context menu item visible on wrong item types. Runtime errors when clicking the action.

**Phase to address:** Context menu integration phase — build the injection as an isolated spike before connecting it to serialize/deserialize logic. Confidence level: LOW on exact ScreenInjector API surface (limited public docs; verify against decompiled DW assemblies or DW support).

---

## Moderate Pitfalls

### Pitfall 6: Config File Validation Diverges Between Manual and UI Paths

**What goes wrong:** The existing `ConfigLoader.Validate()` method enforces rules (non-empty OutputDirectory, at least one predicate, areaId > 0). The UI save path bypasses this validation because it writes JSON directly without going through `ConfigLoader`. Or the UI adds its own validation that is stricter/looser than `ConfigLoader`, creating two divergent validation rulesets. A config that the UI considers valid fails when loaded by the scheduled task, or vice versa.

**Prevention:**
- Extract validation into a standalone `ConfigValidator` class that BOTH `ConfigLoader.Load()` and the UI save command call before writing.
- The UI save command must: (1) build the `SyncConfiguration` model, (2) validate it through the shared validator, (3) serialize to JSON, (4) write to disk.
- Never write config JSON without validating it first.
- Add a "test configuration" action in the UI that loads the saved file through `ConfigLoader` and reports success/failure.

---

### Pitfall 7: Admin UI Assumes Single-Area Configuration But System Supports Multiple

**What goes wrong:** The admin UI is designed with a single-area mindset (one OutputDirectory, one set of settings), but the config file supports multiple predicates across different areas. The UI either flattens this into a confusing single-screen layout or fails to represent multi-predicate configs that were created manually.

**Prevention:**
- Design the UI to show predicates as a LIST, not as a single-form. Each predicate should be an editable row or sub-node.
- Load and display ALL predicates from the config file, even if there are more than expected.
- Handle the case where a manually-edited config has structures the UI does not expect (extra fields, nested objects) — preserve them on save, do not discard unknown fields.
- Use `JsonSerializerOptions` with `JsonExtensionDataAttribute` or similar to round-trip unknown JSON properties through the UI save cycle.

---

### Pitfall 8: Zip Upload Deserialize — No Validation of Zip Contents Before Extraction

**What goes wrong:** The deserialize context menu action accepts a zip file upload and extracts it directly, then attempts to deserialize whatever is inside. A malformed zip, a zip containing non-YAML files, or a zip with unexpected directory structure causes confusing errors deep in the deserialization pipeline. Worse: a zip-slip attack (entries with `../` in paths) writes files outside the intended extraction directory.

**Prevention:**
- Validate zip contents BEFORE extraction: check that all entries end in `.yml` or are directories, check for path traversal (`..` segments), check total uncompressed size against a limit.
- Use `ZipArchive` and iterate entries manually rather than `ZipFile.ExtractToDirectory()` to inspect each entry path.
- After extraction, verify the expected directory structure exists (area subdirectory containing `area.yml`) before passing to `ContentDeserializer`.
- Reject zips larger than a configurable limit (e.g., 100MB compressed).
- Return clear error messages: "Invalid zip: no area.yml found" rather than a stack trace from `FileSystemStore.ReadTree()`.

---

### Pitfall 9: Context Menu Actions Not Guarded by DW Permissions

**What goes wrong:** The serialize/deserialize context menu actions are visible to all admin users, including those who should not have access to content sync operations. A user without appropriate permissions triggers a full deserialization, overwriting content they were not supposed to modify.

**Prevention:**
- Add DW permission checks to the command handlers. Use DW's built-in permission system (if available via `ContextActions` permissions) to restrict who can see and execute the context menu actions.
- At minimum, restrict to administrators or a specific permission group.
- Log WHO triggered the action and WHEN, not just that it ran.

---

### Pitfall 10: UI Save Command Writes Config but Scheduled Task Reads Cached Copy

**What goes wrong:** The admin UI successfully writes a new config to disk, but the currently-running DW application has already loaded and cached the config in memory (e.g., in a static variable or singleton). The next scheduled task run uses the stale cached config, not the freshly saved file. The user changes settings in the UI, but the changes have no effect until the app restarts.

**Prevention:**
- The config must be loaded fresh from disk on EVERY scheduled task run. Do not cache `SyncConfiguration` in a static field or singleton.
- The current `ConfigLoader.Load()` already reads from disk each time, which is correct. Ensure this pattern is preserved — do NOT optimize it with a cache unless the cache is explicitly invalidated when the UI saves.
- If caching is needed for performance, implement a `FileSystemWatcher` that invalidates the cache when the config file changes.
- In the UI, after save, display a confirmation that includes "Changes will take effect on the next scheduled task run."

---

## Minor Pitfalls

### Pitfall 11: Tree Node Sort Order Conflicts with Other Apps

**What goes wrong:** The `NavigationNodeProvider` adds nodes at a sort position that conflicts with another AppStore app's nodes, causing the tree to display in unexpected order or nodes to overlap visually.

**Prevention:** Use a high sort value (e.g., 900+) for custom nodes to avoid conflicts. Check for collisions by inspecting the tree after installation alongside common DW apps.

---

### Pitfall 12: Zip File Name Encoding on Non-ASCII Page Names

**What goes wrong:** Pages with Unicode characters in their names (e.g., accented characters, CJK) produce folder names in the zip that do not round-trip correctly across different zip tools or OS locales. The zip file created on a Windows server extracts with garbled folder names on macOS or Linux.

**Prevention:** Use `ZipArchive` with `Encoding.UTF8` for entry names. Sanitize folder names to ASCII-safe characters before creating zip entries, matching the existing `SanitizeFolderName()` logic in `FileSystemStore`.

---

### Pitfall 13: Browser Download Timeout on Large Content Trees

**What goes wrong:** Serializing a large content tree to zip takes 30+ seconds. The browser request times out before the zip is ready, showing an error to the user. The serialization continues on the server, creating an orphaned temp directory.

**Prevention:** For trees exceeding a threshold (e.g., 100 pages), use a background task pattern: start the operation, return immediately with a "processing" status, and provide a download link or notification when complete. For smaller trees, set an appropriate response timeout. Show a progress indicator in the UI.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Config management layer | Concurrent read-write corruption (Pitfall 1) | Build ReaderWriterLockSlim + file locking + stale-write detection FIRST |
| Tree node registration | Node visible but screen 404 (Pitfall 2) | Build all four artifacts as a vertical slice; test in running DW instance |
| Predicate management UI | Query UI mismatch (Pitfall 3) | Purpose-built simple UI, not query expression reuse |
| Serialize context menu | Temp file leaks (Pitfall 4) | Cleanup-on-startup pattern + size limits |
| Deserialize context menu | Zip-slip and malformed input (Pitfall 8) | Validate zip contents before extraction |
| Context menu injection | Wrong ScreenInjector target (Pitfall 5) | Spike with minimal test action first; verify target type |
| UI save operations | Validation divergence (Pitfall 6) | Shared ConfigValidator class |
| Permissions | Unguarded actions (Pitfall 9) | Permission checks on all command handlers |

---

## Sources

- [DW10 Extensibility & Customization](https://doc.dynamicweb.com/dw10-quickstart/frontpage/developers/extensibility-customization) -- NavigationNodeProvider, ScreenInjector, CommandBase, DataQueryBase patterns
- [DW10 Screen Types and Elements](https://doc.dynamicweb.dev/documentation/extending/administration-ui/screentypes.html) -- Screen types, context menus, UI element patterns
- [DW10 Getting Started with Extending](https://doc.dynamicweb.dev/documentation/extending/index.html) -- Extension categories, notification subscribers, providers
- [DW10 AppStore App Guide](https://doc.dynamicweb.dev/documentation/extending/guides/newappstoreapp.html) -- NavigationNodeProvider for AppsSettingSection, EditScreenBase, CommandBase examples
- [DW10 Queries Documentation](https://doc.dynamicweb.dev/manual/dynamicweb10/settings/system/repositories/queries.html) -- Index queries vs UI queries distinction, expression groups
- [DW10 Create Custom Setting Node](https://doc.dynamicweb.com/forum/development/create-custom-setting-node-in-settings-section?PID=1605) -- Community guidance on custom settings nodes
- [.NET ReaderWriterLockSlim](https://learn.microsoft.com/en-us/dotnet/api/system.threading.readerwriterlockslim) -- Thread-safe file access patterns
- [.NET FileStream File Locks](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.lock) -- Cross-process file locking
- [C# Thread Safe File Writer](https://briancaos.wordpress.com/2022/06/16/c-thread-safe-file-writer-and-reader/) -- Practical file locking patterns
- [ZipArchive Class](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchive) -- Zip creation without temp files, entry-level control
- [ASP.NET Temp File Accumulation](https://techcommunity.microsoft.com/blog/iis-support-blog/asp-net-temp-file-accumulation-on-server/3951330) -- Temp file cleanup on IIS/Kestrel

---
*Pitfalls research for: Dynamicweb.ContentSync v1.2 Admin UI integration*
*Researched: 2026-03-21*

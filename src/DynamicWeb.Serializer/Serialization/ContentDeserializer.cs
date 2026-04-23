using Dynamicweb.Content;
using Dynamicweb.Data;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers.SqlTable;

namespace DynamicWeb.Serializer.Serialization;

/// <summary>
/// Orchestrates the disk-to-DynamicWeb deserialization pipeline:
/// reads YAML files via FileSystemStore.ReadTree(), resolves GUID identity against
/// the target database, writes items in dependency order (Area > Pages > GridRows > Paragraphs),
/// supports dry-run mode with field-level diffs, and handles errors with cascade-skip semantics.
/// </summary>
public class ContentDeserializer
{
    private readonly SerializerConfiguration _configuration;
    private readonly IContentStore _store;
    private readonly Action<string>? _log;
    private readonly bool _isDryRun;
    private readonly string? _filesRoot;
    private readonly ConflictStrategy _conflictStrategy;
    private readonly TargetSchemaCache _schemaCache;
    private readonly PermissionMapper _permissionMapper;
    private readonly TemplateAssetManifest _templateManifest;
    private readonly StrictModeEscalator _templateEscalator;
    // Phase 38 A.2 (D-38-05): test seam for Area SQL write paths so the
    // SET IDENTITY_INSERT [Area] ON/OFF wrapping can be asserted without a live DB.
    // Production default: DwSqlExecutor (wraps Dynamicweb.Data.Database.ExecuteNonQuery).
    private readonly ISqlExecutor _sqlExecutor;

    /// <summary>
    /// When <see cref="ConflictStrategy.DestinationWins"/> (Phase 39 Seed mode), pages whose
    /// <c>PageUniqueId</c> is already present on target are field-level merged with the Seed
    /// YAML: scalars, sub-object DTO properties, ItemFields, and PropertyItem fields each
    /// honor <see cref="MergePredicate.IsUnsetForMerge(object?, System.Type)"/>. Only fields
    /// that are NULL or at the type default on target are filled from YAML; customer tweaks
    /// already set on target survive intrinsically. Page permissions are never touched on the
    /// Seed UPDATE path (D-06). Phase 39 supersedes the Phase 37-01 row-level skip that
    /// previously short-circuited the UPDATE here.
    /// </summary>
    /// <param name="schemaCache">
    /// Shared target-schema cache used by the Area write path for schema-drift tolerance and
    /// YAML→CLR type coercion (Phase 37-02). Defaults to a new instance backed by the live
    /// INFORMATION_SCHEMA query loader.
    /// </param>
    /// <param name="sqlExecutor">
    /// Phase 38 A.2 (D-38-05): optional test seam for the Area write paths. Production callers
    /// pass <c>null</c> to get a <see cref="DwSqlExecutor"/> wrapping the live Dynamicweb.Data.Database
    /// static API. Tests inject a Moq&lt;ISqlExecutor&gt; to capture CommandBuilder text and
    /// assert on the SET IDENTITY_INSERT [Area] ON/INSERT/OFF ordering.
    /// </param>
    public ContentDeserializer(
        SerializerConfiguration configuration,
        IContentStore? store = null,
        Action<string>? log = null,
        bool isDryRun = false,
        string? filesRoot = null,
        ConflictStrategy conflictStrategy = ConflictStrategy.SourceWins,
        TargetSchemaCache? schemaCache = null,
        // Phase 38 A.2 (D-38-05): test seam for Area write paths.
        ISqlExecutor? sqlExecutor = null)
    {
        _configuration = configuration;
        _store = store ?? new FileSystemStore();
        _log = log;
        _isDryRun = isDryRun;
        _filesRoot = filesRoot;
        _conflictStrategy = conflictStrategy;
        _permissionMapper = new PermissionMapper(log);
        _schemaCache = schemaCache ?? new TargetSchemaCache();
        _templateManifest = new TemplateAssetManifest();
        // Phase 37-05: manifest validation uses a lenient escalator by default — the
        // orchestrator's strict-mode wrapper (Phase 37-04) will intercept the WARNING
        // lines and escalate them at end-of-run when strict mode is active.
        _templateEscalator = new StrictModeEscalator(strict: false, log: _log);
        // Phase 38 A.2 (D-38-05): default SqlExecutor wraps Dynamicweb.Data.Database.
        _sqlExecutor = sqlExecutor ?? new DwSqlExecutor();
    }

    private void Log(string message) => _log?.Invoke(message);

    // -------------------------------------------------------------------------
    // Write context — carries mutable state through the recursive tree walk
    // -------------------------------------------------------------------------

    private class WriteContext
    {
        public int TargetAreaId { get; set; }
        public int ParentPageId { get; set; }  // 0 for root pages
        public Dictionary<Guid, int> PageGuidCache { get; set; } = new();
        public HashSet<Guid> FailedParentGuids { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        /// <summary>Fields excluded from serialization — must NOT be nulled out during deserialization.</summary>
        public IReadOnlySet<string>? ExcludeFields { get; set; }
        /// <summary>Per-item-type field exclusions from config-level dictionary.</summary>
        public IReadOnlyDictionary<string, List<string>>? ExcludeFieldsByItemType { get; set; }
    }

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Deserializes all predicates defined in the configuration from disk to DW.
    /// Returns a DeserializeResult with aggregate counts and any errors encountered.
    /// </summary>
    public DeserializeResult Deserialize()
    {
        if (!Directory.Exists(_configuration.OutputDirectory))
        {
            var msg = $"OutputDirectory '{_configuration.OutputDirectory}' does not exist. " +
                      "Cannot deserialize — run serialization first to create it.";
            Log(msg);
            return new DeserializeResult
            {
                Errors = new List<string> { msg }
            };
        }

        int totalCreated = 0;
        int totalUpdated = 0;
        int totalSkipped = 0;
        int totalFailed = 0;
        var allErrors = new List<string>();

        // Phase 37-05 / TEMPLATE-01: pre-flight template manifest validation. Runs once
        // before any page writes so operators see missing-template errors up-front rather
        // than per-page during the run. Missing templates flow through the escalator —
        // orchestrator's strict-mode log wrapper elevates WARNING lines to the cumulative
        // exception when strict mode is active.
        ValidateTemplateManifest();

        // Collect all areas and their deserialized page caches for cross-area link resolution
        var allAreaPages = new List<SerializedPage>();
        var globalPageGuidCache = new Dictionary<Guid, int>();

        foreach (var predicate in _configuration.Predicates)
        {
            // Resolve the area name from the predicate AreaId to read the correct subfolder
            var dwArea = Services.Areas.GetArea(predicate.AreaId);
            var areaName = dwArea?.Name;
            var area = _store.ReadTree(_configuration.OutputDirectory, areaName);

            var result = DeserializePredicate(predicate, area, globalPageGuidCache, allAreaPages);
            totalCreated += result.Created;
            totalUpdated += result.Updated;
            totalSkipped += result.Skipped;
            totalFailed += result.Failed;
            allErrors.AddRange(result.Errors);
        }

        // Phase 2: Resolve internal links using a CROSS-AREA map
        // Read ALL area directories from the content root to build a complete source→target map
        // (ContentProvider calls us per-area, but links reference pages across areas)
        if (!_isDryRun && globalPageGuidCache != null && globalPageGuidCache.Count > 0)
        {
            // Collect pages from ALL area directories for a complete source ID map
            var allYamlPages = new List<SerializedPage>();
            var allGuidCache = new Dictionary<Guid, int>();

            // Scan all area subdirectories in the content root
            var contentRoot = _configuration.OutputDirectory;
            foreach (var areaDir in Directory.GetDirectories(contentRoot))
            {
                var areaYml = Path.Combine(areaDir, "area.yml");
                if (!File.Exists(areaYml)) continue;

                try
                {
                    var areaData = _store.ReadTree(contentRoot, Path.GetFileName(areaDir));
                    allYamlPages.AddRange(areaData.Pages);
                }
                catch { /* skip unreadable areas */ }
            }

            // Build GUID cache from ALL areas in the target DB
            foreach (var masterArea in Services.Areas.GetAreas())
            {
                foreach (var page in Services.Pages.GetPagesByAreaID(masterArea.ID))
                    if (page.UniqueId != Guid.Empty)
                        allGuidCache.TryAdd(page.UniqueId, page.ID);
            }

            var crossAreaMap = InternalLinkResolver.BuildSourceToTargetMap(allYamlPages, allGuidCache);
            Log($"Cross-area link resolution: {crossAreaMap.Count} page ID mappings from {Directory.GetDirectories(contentRoot).Length} areas");

            // Build paragraph map too
            var paragraphCache = new Dictionary<Guid, int>();
            foreach (var masterArea in Services.Areas.GetAreas())
                foreach (var page in Services.Pages.GetPagesByAreaID(masterArea.ID))
                    foreach (var para in Services.Paragraphs.GetParagraphsByPageId(page.ID))
                        if (para.UniqueId != Guid.Empty)
                            paragraphCache.TryAdd(para.UniqueId, para.ID);
            var paragraphMap = InternalLinkResolver.BuildSourceToTargetParagraphMap(allYamlPages, paragraphCache);

            var resolver = new InternalLinkResolver(crossAreaMap, _log, sourceToTargetParagraphIds: paragraphMap);
            foreach (var predicate in _configuration.Predicates)
                ResolveLinksInArea(predicate.AreaId, resolver);

            var (resolved, unresolved, paraResolved, paraUnresolved) = resolver.GetStats();
            if (resolved > 0 || unresolved > 0)
                Log($"Link resolution: {resolved} page links resolved, {unresolved} unresolvable; {paraResolved} paragraph anchors resolved, {paraUnresolved} unresolvable");
        }

        var aggregated = new DeserializeResult
        {
            Created = totalCreated,
            Updated = totalUpdated,
            Skipped = totalSkipped,
            Failed = totalFailed,
            Errors = allErrors
        };

        Log(aggregated.Summary);
        if (aggregated.HasErrors)
        {
            foreach (var error in aggregated.Errors)
                Log(error);
        }

        return aggregated;
    }

    /// <summary>
    /// Phase 37-05 / TEMPLATE-01: read <c>templates.manifest.yml</c> from the output root
    /// and verify every referenced cshtml / grid-row / item-type file exists on the target
    /// filesystem. Runs before any page writes so operators see upfront whether templates
    /// are in place. No-op when <see cref="_filesRoot"/> is null (unit tests typically
    /// don't provide one) or no manifest is present (older baselines pre-Phase-37-05).
    /// </summary>
    private void ValidateTemplateManifest()
    {
        if (string.IsNullOrEmpty(_filesRoot)) return;

        List<TemplateReference>? refs;
        try
        {
            refs = _templateManifest.Read(_configuration.OutputDirectory);
        }
        catch (Exception ex)
        {
            Log($"WARNING: Could not read template manifest: {ex.Message}");
            return;
        }

        if (refs == null || refs.Count == 0) return;

        Log($"Validating {TemplateAssetManifest.ManifestFileName} ({refs.Count} reference(s))...");
        var missing = _templateManifest.Validate(_filesRoot, refs, _templateEscalator);
        Log($"Template validation: {refs.Count - missing} found, {missing} missing");
    }

    // -------------------------------------------------------------------------
    // Predicate-level processing
    // -------------------------------------------------------------------------

    private DeserializeResult DeserializePredicate(ProviderPredicateDefinition predicate, SerializedArea area,
        Dictionary<Guid, int>? globalPageGuidCache = null, List<SerializedPage>? allAreaPages = null)
    {
        // Build excludeFields set early — needed for area creation before WriteContext
        var excludeFieldsSet = predicate.ExcludeFields.Count > 0
            ? new HashSet<string>(predicate.ExcludeFields, StringComparer.OrdinalIgnoreCase)
            : null;

        var targetArea = Services.Areas.GetArea(predicate.AreaId);
        if (targetArea == null)
        {
            // AREA-04: Create the area if it doesn't exist on target
            if (!_isDryRun && area.Properties.Count > 0)
            {
                Log($"Area with ID {predicate.AreaId} not found. Creating from YAML data.");
                try
                {
                    // Phase 37-01.1: explicit Deploy accessor — ContentDeserializer is Deploy-scoped
                    // today (Seed path is handled via DestinationWins skip). Follow-up plan threads
                    // per-mode exclusions through here.
                    var createAreaExclude = _configuration.Deploy.ExcludeFieldsByItemType.Count > 0 && !string.IsNullOrEmpty(area.ItemType)
                    ? ExclusionMerger.MergeFieldExclusions(
                        excludeFieldsSet?.ToList() ?? new List<string>(),
                        _configuration.Deploy.ExcludeFieldsByItemType,
                        area.ItemType)
                    : excludeFieldsSet;
                CreateAreaFromProperties(predicate.AreaId, area, createAreaExclude);
                    Services.Areas.ClearCache(); // Critical: per project_dw_area_cache.md
                    targetArea = Services.Areas.GetArea(predicate.AreaId);
                    if (targetArea == null)
                    {
                        Log($"ERROR: Area creation succeeded but GetArea still returns null after cache clear. Skipping predicate '{predicate.Name}'.");
                        return new DeserializeResult();
                    }
                    Log($"Area created: ID={predicate.AreaId}, Name={area.Name}");
                }
                catch (Exception ex)
                {
                    Log($"ERROR: Failed to create area {predicate.AreaId}: {ex.Message}. Skipping predicate '{predicate.Name}'.");
                    return new DeserializeResult();
                }
            }
            else
            {
                Log($"Warning: Area with ID {predicate.AreaId} not found. Skipping predicate '{predicate.Name}'.");
                return new DeserializeResult();
            }
        }

        Log($"Deserializing predicate '{predicate.Name}' into area ID={predicate.AreaId}");

        // Pre-build page GUID cache for the entire area (avoids per-item full table scans)
        var allPages = Services.Pages.GetPagesByAreaID(predicate.AreaId);
        var pageGuidCache = allPages
            .Where(p => p.UniqueId != Guid.Empty)
            .ToDictionary(p => p.UniqueId, p => p.ID);

        var ctx = new WriteContext
        {
            TargetAreaId = predicate.AreaId,
            ParentPageId = 0,
            PageGuidCache = pageGuidCache,
            ExcludeFields = excludeFieldsSet,
            ExcludeFieldsByItemType = _configuration.Deploy.ExcludeFieldsByItemType.Count > 0
                ? _configuration.Deploy.ExcludeFieldsByItemType
                : null
        };

        // Write full area properties (AREA-04)
        if (area.Properties.Count > 0 && !_isDryRun)
        {
            Log($"Writing {area.Properties.Count} area properties for area ID={predicate.AreaId}");
            var areaPropsExclude = ctx.ExcludeFieldsByItemType != null && !string.IsNullOrEmpty(area.ItemType)
                ? ExclusionMerger.MergeFieldExclusions(
                    ctx.ExcludeFields?.ToList() ?? new List<string>(),
                    ctx.ExcludeFieldsByItemType,
                    area.ItemType)
                : ctx.ExcludeFields;
            var excludeAreaColumnsSet = predicate.ExcludeAreaColumns.Count > 0
                ? new HashSet<string>(predicate.ExcludeAreaColumns, StringComparer.OrdinalIgnoreCase)
                : null;
            WriteAreaProperties(predicate.AreaId, area.Properties, areaPropsExclude, excludeAreaColumnsSet);
            Services.Areas.ClearCache();
        }

        // Save area-level ItemType fields (AREA-01)
        if (!string.IsNullOrEmpty(area.ItemType) && area.ItemFields.Count > 0 && !_isDryRun)
        {
            var targetAreaItemId = targetArea.ItemId;

            // If the area has no Item row yet, create one (same pattern as GridRow Item creation)
            if (string.IsNullOrEmpty(targetAreaItemId) || Services.Items.GetItem(area.ItemType, targetAreaItemId) == null)
            {
                try
                {
                    var item = new Dynamicweb.Content.Items.Item(area.ItemType);
                    Services.Items.SaveItem(item);
                    targetAreaItemId = item.Id;
                    targetArea.ItemId = targetAreaItemId;
                    targetArea.ItemType = area.ItemType;
                    Services.Areas.SaveArea(targetArea);
                    Services.Areas.ClearCache();
                    Log($"Created area Item: type={area.ItemType}, id={targetAreaItemId}");
                }
                catch (Exception ex)
                {
                    Log($"WARNING: Could not create area Item: {ex.Message}");
                }
            }
            else if (targetArea.ItemType != area.ItemType)
            {
                // Repair binding for an Area whose Item exists but whose AreaItemType column
                // is blank or stale (e.g., written by a pre-fix deserialize). Without this
                // assignment, the downstream ResolveLinksInArea guard skips link remapping.
                targetArea.ItemType = area.ItemType;
                Services.Areas.SaveArea(targetArea);
                Services.Areas.ClearCache();
                Log($"Repaired area binding: type={area.ItemType}, id={targetAreaItemId}");
            }

            if (!string.IsNullOrEmpty(targetAreaItemId))
            {
                Log($"Applying area ItemType fields: type={area.ItemType}, id={targetAreaItemId}, fields={area.ItemFields.Count}");
                var effectiveExclude = ctx.ExcludeFieldsByItemType != null
                    ? ExclusionMerger.MergeFieldExclusions(
                        ctx.ExcludeFields?.ToList() ?? new List<string>(),
                        ctx.ExcludeFieldsByItemType,
                        area.ItemType)
                    : ctx.ExcludeFields;
                SaveItemFields(area.ItemType, targetAreaItemId, area.ItemFields, effectiveExclude);
            }
        }

        foreach (var page in area.Pages)
        {
            DeserializePageSafe(page, ctx);
        }

        // Contribute this area's pages and GUID cache to the global collections for cross-area link resolution
        if (globalPageGuidCache != null)
        {
            foreach (var kvp in ctx.PageGuidCache)
                globalPageGuidCache.TryAdd(kvp.Key, kvp.Value);
        }
        allAreaPages?.AddRange(area.Pages);

        return new DeserializeResult
        {
            Created = ctx.Created,
            Updated = ctx.Updated,
            Skipped = ctx.Skipped,
            Failed = ctx.Failed,
            Errors = ctx.Errors
        };
    }

    // -------------------------------------------------------------------------
    // Area SQL property write-back
    // -------------------------------------------------------------------------

    /// <summary>
    /// Write area properties back to the [Area] table via SQL UPDATE.
    /// Skips columns in excludeFields to preserve environment-specific values.
    /// Also skips columns not present on the target schema (logs a warning once per column).
    /// Type coercion and schema-drift handling delegate to the shared <see cref="TargetSchemaCache"/>
    /// (Phase 37-02).
    /// </summary>
    private void WriteAreaProperties(int areaId, Dictionary<string, object> properties, IReadOnlySet<string>? excludeFields, IReadOnlySet<string>? excludeAreaColumns = null)
    {
        if (properties.Count == 0) return;

        var targetCols = _schemaCache.GetColumns("Area");

        var cb = new CommandBuilder();
        var first = true;
        foreach (var kvp in properties)
        {
            // Skip excluded fields (per AREA-05) and excluded area columns (per AREA-08)
            if (excludeFields?.Contains(kvp.Key) == true) continue;
            if (excludeAreaColumns?.Contains(kvp.Key) == true) continue;

            // Skip columns that don't exist on the target schema (graceful cross-version handling)
            if (!targetCols.Contains(kvp.Key))
            {
                _schemaCache.LogMissingColumnOnce("Area", kvp.Key, _log);
                continue;
            }

            var coerced = _schemaCache.Coerce("Area", kvp.Key, kvp.Value);
            if (first)
            {
                cb.Add($"UPDATE [Area] SET [{kvp.Key}] = {{0}}", coerced);
                first = false;
            }
            else
            {
                cb.Add($", [{kvp.Key}] = {{0}}", coerced);
            }
        }
        // If all properties were excluded, nothing to update
        if (first) return;

        cb.Add(" WHERE [AreaID] = {0}", areaId);
        // Phase 38 A.2 (D-38-05): routed through ISqlExecutor seam for testability.
        _sqlExecutor.ExecuteNonQuery(cb);
    }

    /// <summary>
    /// Create a new area row via SQL INSERT using serialized properties.
    /// Called when the target area does not exist (AREA-04).
    /// </summary>
    private void CreateAreaFromProperties(int areaId, SerializedArea area, IReadOnlySet<string>? excludeFields)
    {
        var columns = new List<string> { "[AreaID]", "[AreaName]", "[AreaSort]", "[AreaUniqueId]" };
        var values = new List<object> { areaId, area.Name, area.SortOrder, area.AreaId };

        var targetCols = _schemaCache.GetColumns("Area");
        foreach (var kvp in area.Properties)
        {
            if (excludeFields?.Contains(kvp.Key) == true) continue;
            if (!targetCols.Contains(kvp.Key))
            {
                _schemaCache.LogMissingColumnOnce("Area", kvp.Key, _log);
                continue;
            }
            columns.Add($"[{kvp.Key}]");
            values.Add(_schemaCache.Coerce("Area", kvp.Key, kvp.Value) ?? DBNull.Value);
        }

        // 2026-04-20: wrap in SET IDENTITY_INSERT so explicit AreaID writes succeed against
        // a fresh target where Area.AreaId is an identity column. Keeping the areaId stable
        // across env is required for predicate.areaId references to work.
        //
        // Phase 38 WR-02: Wrap the INSERT in TRY/CATCH so SET IDENTITY_INSERT [Area] OFF is
        // always emitted even when the INSERT throws (FK violation, duplicate AreaUniqueId,
        // etc.). Without the CATCH, a failed INSERT would leave the connection's session
        // state with IDENTITY_INSERT still ON for [Area], and subsequent work on the same
        // pooled connection could fail unexpectedly. THROW re-raises the original exception
        // so the caller still sees the failure. The outer OFF is a belt-and-braces terminator
        // for the success path (TRY completes without entering CATCH).
        var cb = new CommandBuilder();
        cb.Add("SET IDENTITY_INSERT [Area] ON; ");
        cb.Add("BEGIN TRY ");
        cb.Add($"INSERT INTO [Area] ({string.Join(", ", columns)}) VALUES (");
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) cb.Add(", ");
            cb.Add("{0}", values[i]);
        }
        cb.Add("); ");
        cb.Add("END TRY BEGIN CATCH ");
        cb.Add("SET IDENTITY_INSERT [Area] OFF; ");
        cb.Add("THROW; ");
        cb.Add("END CATCH; ");
        cb.Add("SET IDENTITY_INSERT [Area] OFF;");
        // Phase 38 A.2 (D-38-05): routed through ISqlExecutor seam so the
        // SET IDENTITY_INSERT [Area] ON/OFF wrapping can be asserted by tests.
        _sqlExecutor.ExecuteNonQuery(cb);
    }

    // -------------------------------------------------------------------------
    // Phase 38 A.2 (D-38-05): internal test hooks for Area SQL write paths.
    // Access is gated by the <InternalsVisibleTo Include="DynamicWeb.Serializer.Tests" />
    // entry in DynamicWeb.Serializer.csproj. Production code never calls these.
    // The forwarder pattern avoids making the private methods public or using
    // reflection — reviewable and deterministic (checker warning W2 resolution).
    // -------------------------------------------------------------------------

    /// <summary>
    /// Test-only forwarder to the private <c>CreateAreaFromProperties</c>.
    /// Drives the Area INSERT path so the SET IDENTITY_INSERT [Area] ON/INSERT/OFF
    /// ordering can be asserted via the injected <see cref="ISqlExecutor"/>.
    /// </summary>
    internal void InvokeCreateAreaFromPropertiesForTest(int areaId, SerializedArea area, IReadOnlySet<string>? excludeFields)
        => CreateAreaFromProperties(areaId, area, excludeFields);

    /// <summary>
    /// Test-only forwarder to the private <c>WriteAreaProperties</c>. Drives the
    /// Area UPDATE path to confirm it does NOT emit IDENTITY_INSERT wrappers.
    /// </summary>
    internal void InvokeUpdateAreaFromPropertiesForTest(int areaId, Dictionary<string, object> properties, IReadOnlySet<string>? excludeFields, IReadOnlySet<string>? excludeAreaColumns = null)
        => WriteAreaProperties(areaId, properties, excludeFields, excludeAreaColumns);

    // -------------------------------------------------------------------------
    // Page deserialization
    // -------------------------------------------------------------------------

    private void DeserializePageSafe(SerializedPage dto, WriteContext ctx)
    {
        // Cascade skip: if any ancestor failed, skip this page and all its children
        if (ctx.FailedParentGuids.Contains(dto.PageUniqueId))
        {
            ctx.Skipped++;
            Log($"SKIPPED page {dto.PageUniqueId} ('{dto.MenuText}') — parent failed");
            return;
        }

        // Check if any ancestor of this page is in the failed set by traversal context
        // (FailedParentGuids accumulates failed pages; children have their parent GUID tracked separately)
        // The cascade skip check above handles direct parent matching; the broader check is handled
        // by not recursing into children when a parent throws (implicit via exception handling below)

        try
        {
            int resolvedId = DeserializePage(dto, ctx);

            // In dry-run mode, don't attempt grid rows/children with synthetic -1 ID
            if (resolvedId < 0 && _isDryRun)
            {
                // Still log children would be processed
                foreach (var child in dto.Children)
                {
                    Log($"[DRY-RUN] SKIP child {child.PageUniqueId} ('{child.MenuText}') — parent is CREATE in dry-run");
                    ctx.Skipped++;
                }
                return;
            }

            if (resolvedId < 0)
                return;

            // Process grid rows for this page
            var gridRowCache = Services.Grids.GetGridRowsByPageId(resolvedId)
                .Where(gr => gr.UniqueId != Guid.Empty)
                .ToDictionary(gr => gr.UniqueId, gr => gr.ID);

            foreach (var row in dto.GridRows)
            {
                DeserializeGridRowSafe(row, resolvedId, gridRowCache, ctx);
            }

            // Recurse children with this page as parent
            var savedParentPageId = ctx.ParentPageId;
            ctx.ParentPageId = resolvedId;
            foreach (var child in dto.Children)
            {
                DeserializePageSafe(child, ctx);
            }
            ctx.ParentPageId = savedParentPageId;
        }
        catch (Exception ex)
        {
            ctx.Failed++;
            var msg = $"ERROR deserializing page {dto.PageUniqueId} ('{dto.MenuText}'): {ex.Message}";
            ctx.Errors.Add(msg);
            Log(msg);

            // Mark this page as failed so all descendant pages are cascade-skipped
            ctx.FailedParentGuids.Add(dto.PageUniqueId);
            Log($"  SKIPPED children of {dto.PageUniqueId} due to parent failure");
        }
    }

    /// <summary>
    /// Writes a single page to DW (insert or update). Returns the resolved numeric page ID,
    /// or -1 in dry-run CREATE mode (no DW ID assigned).
    /// </summary>
    private int DeserializePage(SerializedPage dto, WriteContext ctx)
    {
        // Phase 37-05: inline template validation removed — the manifest pre-flight
        // (ValidateTemplateManifest) now covers all layout / item-type / grid-row refs.

        if (!ctx.PageGuidCache.TryGetValue(dto.PageUniqueId, out var existingId))
        {
            // INSERT path — GUID not found in target area
            if (_isDryRun)
            {
                Log($"[DRY-RUN] CREATE page {dto.PageUniqueId} ('{dto.MenuText}')");
                foreach (var f in dto.Fields)
                    Log($"  set {f.Key} = '{f.Value}'");
                if (dto.Permissions.Count > 0)
                    Log($"[DRY-RUN] Would apply {dto.Permissions.Count} permission(s) to page {dto.PageUniqueId}");
                ctx.Created++;
                return -1;
            }

            var page = new Page();
            page.UniqueId = dto.PageUniqueId;
            page.AreaId = ctx.TargetAreaId;
            page.ParentPageId = ctx.ParentPageId;
            page.MenuText = dto.MenuText;
            page.UrlName = dto.UrlName;
            page.Active = dto.IsActive;
            page.Sort = dto.SortOrder;
            page.ItemType = dto.ItemType ?? string.Empty;
            page.LayoutTemplate = dto.Layout ?? string.Empty;
            page.LayoutApplyToSubPages = dto.LayoutApplyToSubPages;
            page.IsFolder = dto.IsFolder;
            page.TreeSection = dto.TreeSection ?? string.Empty;
            ApplyPageProperties(page, dto);
            // Do NOT set page.ID — leave 0 for insert path (Pitfall 4)

            var saved = Services.Pages.SavePage(page);
            ctx.PageGuidCache[dto.PageUniqueId] = saved.ID;

            // Apply ItemType fields via ItemService (page.Item[key] = value does not persist)
            var refetched = Services.Pages.GetPage(saved.ID);
            if (refetched != null)
            {
                var pageExclude = ctx.ExcludeFieldsByItemType != null
                    ? ExclusionMerger.MergeFieldExclusions(
                        ctx.ExcludeFields?.ToList() ?? new List<string>(),
                        ctx.ExcludeFieldsByItemType,
                        dto.ItemType)
                    : ctx.ExcludeFields;
                SaveItemFields(refetched.ItemType, refetched.ItemId, dto.Fields, pageExclude);

                // Re-apply LayoutTemplate if DW overwrote it during HandleItemStructure
                // (DW sets it to the ItemType's default template on new pages)
                if (!string.IsNullOrEmpty(dto.Layout) && refetched.LayoutTemplate != dto.Layout)
                {
                    Log($"  Re-applying LayoutTemplate: '{refetched.LayoutTemplate}' -> '{dto.Layout}'");
                    refetched.LayoutTemplate = dto.Layout;
                    Services.Pages.SavePage(refetched);
                }

                // Apply PropertyItem fields (e.g. Icon, SubmenuType)
                SavePropertyItemFields(refetched, dto.PropertyFields, pageExclude);
            }

            ctx.Created++;
            Log($"CREATED page {dto.PageUniqueId} -> ID={saved.ID}");
            _permissionMapper.ApplyPermissions(saved.ID, dto.Permissions);
            return saved.ID;
        }
        else
        {
            // UPDATE path — GUID matched an existing page
            // Load existing page from DW so it has an internally-set ID (DW Entity<int>.ID has no public setter)
            var existingPage = Services.Pages.GetPage(existingId);
            if (existingPage == null)
            {
                throw new InvalidOperationException(
                    $"Could not load existing page with ID {existingId} for update.");
            }

            // Phase 39 D-01..D-07, D-11, D-19: Seed mode — field-level merge.
            // Supersedes the row-level skip previously enforced here (Phase 37-01 D-06).
            if (_conflictStrategy == ConflictStrategy.DestinationWins)
            {
                var seedExclude = ctx.ExcludeFieldsByItemType != null
                    ? ExclusionMerger.MergeFieldExclusions(
                        ctx.ExcludeFields?.ToList() ?? new List<string>(),
                        ctx.ExcludeFieldsByItemType,
                        dto.ItemType)
                    : ctx.ExcludeFields;

                if (_isDryRun)
                {
                    LogSeedMergeDryRun(dto, existingPage, seedExclude, ctx);
                    return existingId;
                }

                // Identity — always source-wins per D-05.
                existingPage.UniqueId = dto.PageUniqueId;
                existingPage.AreaId = ctx.TargetAreaId;
                existingPage.ParentPageId = ctx.ParentPageId;

                int filled = 0;
                int left = 0;

                filled += MergePageScalars(existingPage, dto, ref left);
                filled += ApplyPagePropertiesWithMerge(existingPage, dto, ref left);

                Services.Pages.SavePage(existingPage);

                // D-02 / D-03: field-level merge for ItemFields + PropertyItem fields.
                filled += MergeItemFields(existingPage.ItemType, existingPage.ItemId, dto.Fields, seedExclude, ref left);
                filled += MergePropertyItemFields(existingPage, dto.PropertyFields, seedExclude, ref left);

                // D-06: permissions NOT applied on Seed UPDATE.
                // (Intentionally absent: no _permissionMapper.ApplyPermissions call here.)

                // D-11: new log format + counter repurpose.
                if (filled == 0) ctx.Skipped++;
                else ctx.Updated++;
                Log($"Seed-merge: page {dto.PageUniqueId} (ID={existingId}) - {filled} filled, {left} left");

                // D-07: child recursion (gridrows -> columns -> paragraphs) continues below and
                // inherits _conflictStrategy automatically.
                return existingId;
            }

            if (_isDryRun)
            {
                LogDryRunPageUpdate(dto, existingPage, ctx);
                return existingId;
            }

            // Apply scalar properties (source-wins)
            existingPage.UniqueId = dto.PageUniqueId;
            existingPage.AreaId = ctx.TargetAreaId;
            existingPage.ParentPageId = ctx.ParentPageId;
            existingPage.MenuText = dto.MenuText;
            existingPage.UrlName = dto.UrlName;
            existingPage.Active = dto.IsActive;
            existingPage.Sort = dto.SortOrder;
            existingPage.ItemType = dto.ItemType ?? string.Empty;
            existingPage.LayoutTemplate = dto.Layout ?? string.Empty;
            existingPage.LayoutApplyToSubPages = dto.LayoutApplyToSubPages;
            existingPage.IsFolder = dto.IsFolder;
            existingPage.TreeSection = dto.TreeSection ?? string.Empty;
            ApplyPageProperties(existingPage, dto);

            Services.Pages.SavePage(existingPage);

            // Apply ItemType fields via ItemService (source-wins)
            var updatePageExclude = ctx.ExcludeFieldsByItemType != null
                ? ExclusionMerger.MergeFieldExclusions(
                    ctx.ExcludeFields?.ToList() ?? new List<string>(),
                    ctx.ExcludeFieldsByItemType,
                    dto.ItemType)
                : ctx.ExcludeFields;
            SaveItemFields(existingPage.ItemType, existingPage.ItemId, dto.Fields, updatePageExclude);

            // Apply PropertyItem fields (e.g. Icon, SubmenuType)
            SavePropertyItemFields(existingPage, dto.PropertyFields, updatePageExclude);

            ctx.Updated++;
            Log($"UPDATED page {dto.PageUniqueId} (ID={existingId})");
            _permissionMapper.ApplyPermissions(existingId, dto.Permissions);
            return existingId;
        }
    }

    // -------------------------------------------------------------------------
    // Grid row deserialization
    // -------------------------------------------------------------------------

    private void DeserializeGridRowSafe(
        SerializedGridRow dto,
        int pageId,
        Dictionary<Guid, int> gridRowCache,
        WriteContext ctx)
    {
        try
        {
            int resolvedGridRowId = DeserializeGridRow(dto, pageId, gridRowCache, ctx);

            if (resolvedGridRowId < 0 && _isDryRun)
                return;

            if (resolvedGridRowId < 0)
                return;

            // Build paragraph GUID cache for this page
            var paragraphCache = Services.Paragraphs.GetParagraphsByPageId(pageId)
                .Where(p => p.UniqueId != Guid.Empty)
                .ToDictionary(p => p.UniqueId, p => p.ID);

            foreach (var column in dto.Columns)
            {
                foreach (var para in column.Paragraphs)
                {
                    DeserializeParagraphSafe(para, pageId, resolvedGridRowId, column.Id, paragraphCache, ctx);
                }
            }
        }
        catch (Exception ex)
        {
            ctx.Failed++;
            var msg = $"ERROR deserializing grid row {dto.Id} on page {pageId}: {ex.Message}";
            ctx.Errors.Add(msg);
            Log(msg);
        }
    }

    private int DeserializeGridRow(
        SerializedGridRow dto,
        int pageId,
        Dictionary<Guid, int> gridRowCache,
        WriteContext ctx)
    {
        // Phase 37-05: inline validation removed — manifest pre-flight covers these refs.

        if (!gridRowCache.TryGetValue(dto.Id, out var existingGridRowId))
        {
            // INSERT path
            if (_isDryRun)
            {
                Log($"[DRY-RUN] CREATE grid row {dto.Id} (sort={dto.SortOrder}) on page {pageId}");
                ctx.Created++;
                return -1;
            }

            var row = new GridRow(pageId);
            row.UniqueId = dto.Id;
            row.Sort = dto.SortOrder;
            if (!string.IsNullOrEmpty(dto.DefinitionId))
                row.DefinitionId = dto.DefinitionId;
            if (!string.IsNullOrEmpty(dto.ItemType))
                row.ItemType = dto.ItemType;
            ApplyGridRowVisualProperties(row, dto);
            // Do NOT set row.ID (insert path)

            Services.Grids.SaveGridRow(row);

            // Re-query to get DW-assigned numeric ID (Pitfall 1: SaveGridRow returns bool, not GridRow)
            var saved = Services.Grids.GetGridRowsByPageId(pageId)
                .FirstOrDefault(gr => gr.UniqueId == dto.Id);

            if (saved == null)
                throw new InvalidOperationException($"Could not find inserted grid row with GUID {dto.Id}");

            // GridRow.SaveGridRow does NOT auto-create Items (unlike SaveParagraph).
            // Create Item manually and link it to the grid row.
            if (!string.IsNullOrEmpty(dto.ItemType) && string.IsNullOrEmpty(saved.ItemId))
            {
                try
                {
                    var item = new Dynamicweb.Content.Items.Item(dto.ItemType);
                    Services.Items.SaveItem(item);
                    Log($"  GridRow Item created: type={dto.ItemType}, id={item.Id}");
                    saved.ItemId = item.Id;
                    Services.Grids.SaveGridRow(saved);
                    var gridRowExclude = ctx.ExcludeFieldsByItemType != null
                        ? ExclusionMerger.MergeFieldExclusions(
                            ctx.ExcludeFields?.ToList() ?? new List<string>(),
                            ctx.ExcludeFieldsByItemType,
                            dto.ItemType)
                        : ctx.ExcludeFields;
                    SaveItemFields(dto.ItemType, item.Id, dto.Fields, gridRowExclude);
                }
                catch (Exception ex)
                {
                    Log($"  WARNING: GridRow Item creation failed: {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(saved.ItemId))
            {
                var gridRowExclude2 = ctx.ExcludeFieldsByItemType != null
                    ? ExclusionMerger.MergeFieldExclusions(
                        ctx.ExcludeFields?.ToList() ?? new List<string>(),
                        ctx.ExcludeFieldsByItemType,
                        dto.ItemType)
                    : ctx.ExcludeFields;
                SaveItemFields(dto.ItemType, saved.ItemId, dto.Fields, gridRowExclude2);
            }

            var newGridRowId = saved.ID;
            ctx.Created++;
            Log($"CREATED grid row {dto.Id} -> ID={newGridRowId} on page {pageId}");
            return newGridRowId;
        }
        else
        {
            // UPDATE path
            if (_isDryRun)
            {
                // Fetch existing to compare sort order
                var existingRows = Services.Grids.GetGridRowsByPageId(pageId);
                var existingRow = existingRows.FirstOrDefault(gr => gr.ID == existingGridRowId);
                if (existingRow != null && existingRow.Sort != dto.SortOrder)
                {
                    Log($"[DRY-RUN] UPDATE grid row {dto.Id} (ID={existingGridRowId}): Sort: {existingRow.Sort} -> {dto.SortOrder}");
                    ctx.Updated++;
                }
                else
                {
                    Log($"[DRY-RUN] SKIP grid row {dto.Id} (ID={existingGridRowId}) (unchanged)");
                    ctx.Skipped++;
                }
                return existingGridRowId;
            }

            // Load existing grid row from DW so it has internally-set ID (DW Entity<int>.ID has no public setter)
            var existingRow2 = Services.Grids.GetGridRowsByPageId(pageId)
                .FirstOrDefault(gr => gr.ID == existingGridRowId);
            if (existingRow2 == null)
            {
                throw new InvalidOperationException(
                    $"Could not load existing grid row with ID {existingGridRowId} for update.");
            }

            existingRow2.UniqueId = dto.Id;
            existingRow2.Sort = dto.SortOrder;
            if (!string.IsNullOrEmpty(dto.DefinitionId))
                existingRow2.DefinitionId = dto.DefinitionId;
            if (!string.IsNullOrEmpty(dto.ItemType))
                existingRow2.ItemType = dto.ItemType;
            ApplyGridRowVisualProperties(existingRow2, dto);

            Services.Grids.SaveGridRow(existingRow2);

            // Apply ItemType fields via ItemService
            if (!string.IsNullOrEmpty(existingRow2.ItemId))
            {
                var gridRowUpdateExclude = ctx.ExcludeFieldsByItemType != null
                    ? ExclusionMerger.MergeFieldExclusions(
                        ctx.ExcludeFields?.ToList() ?? new List<string>(),
                        ctx.ExcludeFieldsByItemType,
                        dto.ItemType)
                    : ctx.ExcludeFields;
                SaveItemFields(dto.ItemType, existingRow2.ItemId, dto.Fields, gridRowUpdateExclude);
            }

            ctx.Updated++;
            Log($"UPDATED grid row {dto.Id} (ID={existingGridRowId})");
            return existingGridRowId;
        }
    }

    // -------------------------------------------------------------------------
    // Paragraph deserialization
    // -------------------------------------------------------------------------

    private void DeserializeParagraphSafe(
        SerializedParagraph dto,
        int pageId,
        int gridRowId,
        int columnId,
        Dictionary<Guid, int> paragraphCache,
        WriteContext ctx)
    {
        try
        {
            DeserializeParagraph(dto, pageId, gridRowId, columnId, paragraphCache, ctx);
        }
        catch (Exception ex)
        {
            ctx.Failed++;
            var msg = $"ERROR deserializing paragraph {dto.ParagraphUniqueId} on page {pageId}: {ex.Message}";
            ctx.Errors.Add(msg);
            Log(msg);
        }
    }

    private void DeserializeParagraph(
        SerializedParagraph dto,
        int pageId,
        int gridRowId,
        int columnId,
        Dictionary<Guid, int> paragraphCache,
        WriteContext ctx)
    {
        // Phase 37-05: inline validation removed — manifest pre-flight covers item types.

        if (!paragraphCache.TryGetValue(dto.ParagraphUniqueId, out var existingParagraphId))
        {
            // INSERT path
            if (_isDryRun)
            {
                Log($"[DRY-RUN] CREATE paragraph {dto.ParagraphUniqueId} (sort={dto.SortOrder}, type={dto.ItemType}) on page {pageId}");
                foreach (var f in dto.Fields)
                    Log($"  set {f.Key} = '{f.Value}'");
                ctx.Created++;
                return;
            }

            var para = new Paragraph();
            para.UniqueId = dto.ParagraphUniqueId;
            para.PageID = pageId;
            para.GridRowId = gridRowId;
            para.GridRowColumn = columnId;
            para.Sort = dto.SortOrder;
            para.Header = dto.Header;
            para.Template = dto.Template;
            para.ColorSchemeId = dto.ColorSchemeId;
            para.ItemType = dto.ItemType;
            para.ModuleSystemName = dto.ModuleSystemName ?? string.Empty;
            para.ModuleSettings = XmlFormatter.Compact(dto.ModuleSettings) ?? string.Empty;
            // Do NOT set para.ID (insert path)

            Services.Paragraphs.SaveParagraph(para);

            // Re-query to get assigned ID
            var saved = Services.Paragraphs.GetParagraphsByPageId(pageId)
                .FirstOrDefault(p => p.UniqueId == dto.ParagraphUniqueId);

            // Apply ItemType fields via ItemService using paragraph's ItemId (not paragraph ID)
            if (saved != null)
            {
                var paraExclude = ctx.ExcludeFieldsByItemType != null
                    ? ExclusionMerger.MergeFieldExclusions(
                        ctx.ExcludeFields?.ToList() ?? new List<string>(),
                        ctx.ExcludeFieldsByItemType,
                        dto.ItemType)
                    : ctx.ExcludeFields;
                SaveItemFields(dto.ItemType, saved.ItemId, dto.Fields, paraExclude);

                // Re-apply fields that DW may overwrite during HandleItemStructure:
                // - Header: DW sets it to Item's title (template default)
                // - ModuleSystemName/ModuleSettings: may not persist on new paragraphs
                bool needsResave = false;
                if (saved.Header != (dto.Header ?? string.Empty))
                {
                    saved.Header = dto.Header ?? string.Empty;
                    needsResave = true;
                }
                if (!string.IsNullOrEmpty(dto.ModuleSystemName) && saved.ModuleSystemName != dto.ModuleSystemName)
                {
                    saved.ModuleSystemName = dto.ModuleSystemName;
                    saved.ModuleSettings = XmlFormatter.Compact(dto.ModuleSettings) ?? string.Empty;
                    needsResave = true;
                }
                if (!string.IsNullOrEmpty(dto.Template) && saved.Template != dto.Template)
                {
                    saved.Template = dto.Template;
                    needsResave = true;
                }
                if (!string.IsNullOrEmpty(dto.ColorSchemeId) && saved.ColorSchemeId != dto.ColorSchemeId)
                {
                    saved.ColorSchemeId = dto.ColorSchemeId;
                    needsResave = true;
                }
                if (needsResave)
                    Services.Paragraphs.SaveParagraph(saved);
            }

            ctx.Created++;
            Log($"CREATED paragraph {dto.ParagraphUniqueId} on page {pageId}");
        }
        else
        {
            // UPDATE path
            if (_isDryRun)
            {
                var existingParagraphs = Services.Paragraphs.GetParagraphsByPageId(pageId);
                var existing = existingParagraphs.FirstOrDefault(p => p.ID == existingParagraphId);
                if (existing != null)
                    LogDryRunParagraphUpdate(dto, existing, ctx);
                return;
            }

            // Load existing paragraph for update
            var existingForUpdate = Services.Paragraphs.GetParagraphsByPageId(pageId)
                .FirstOrDefault(p => p.ID == existingParagraphId);

            if (existingForUpdate == null)
            {
                throw new InvalidOperationException(
                    $"Could not load existing paragraph with ID {existingParagraphId} for update.");
            }

            existingForUpdate.UniqueId = dto.ParagraphUniqueId;
            existingForUpdate.GridRowId = gridRowId;
            existingForUpdate.GridRowColumn = columnId;
            existingForUpdate.Sort = dto.SortOrder;
            existingForUpdate.Header = dto.Header;
            existingForUpdate.Template = dto.Template;
            existingForUpdate.ColorSchemeId = dto.ColorSchemeId;
            existingForUpdate.ItemType = dto.ItemType;
            existingForUpdate.ModuleSystemName = dto.ModuleSystemName ?? string.Empty;
            existingForUpdate.ModuleSettings = XmlFormatter.CompactWithMerge(dto.ModuleSettings, existingForUpdate.ModuleSettings) ?? string.Empty;

            Services.Paragraphs.SaveParagraph(existingForUpdate);

            // Apply ItemType fields via ItemService (source-wins)
            var paraUpdateExclude = ctx.ExcludeFieldsByItemType != null
                ? ExclusionMerger.MergeFieldExclusions(
                    ctx.ExcludeFields?.ToList() ?? new List<string>(),
                    ctx.ExcludeFieldsByItemType,
                    dto.ItemType)
                : ctx.ExcludeFields;
            SaveItemFields(existingForUpdate.ItemType, existingForUpdate.ItemId, dto.Fields, paraUpdateExclude);
            ctx.Updated++;
            Log($"UPDATED paragraph {dto.ParagraphUniqueId} (ID={existingParagraphId})");
        }
    }

    // -------------------------------------------------------------------------
    // Page PropertyItem persistence (Icon, SubmenuType, etc.)
    // -------------------------------------------------------------------------

    private void SavePropertyItemFields(Page page, Dictionary<string, object> propertyFields, IReadOnlySet<string>? excludeFields = null)
    {
        if (propertyFields.Count == 0)
            return;

        if (string.IsNullOrEmpty(page.PropertyItemId))
        {
            Log($"  Page {page.UniqueId} has no PropertyItemId — cannot write property fields");
            return;
        }

        var propItem = page.PropertyItem;
        if (propItem == null)
        {
            Log($"  WARNING: Could not load PropertyItem for page {page.UniqueId}");
            return;
        }

        var contentFields = propertyFields
            .Where(kvp => !ItemSystemFields.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);

        // Source-wins: null out property fields not present in serialized data
        foreach (var fieldName in propItem.Names)
        {
            if (!ItemSystemFields.Contains(fieldName) && !contentFields.ContainsKey(fieldName))
            {
                // Skip guard: do NOT null out fields that were intentionally excluded from serialization (FILT-03)
                if (excludeFields != null && excludeFields.Contains(fieldName))
                    continue;
                contentFields[fieldName] = null;
            }
        }

        if (contentFields.Count == 0)
            return;

        propItem.DeserializeFrom(contentFields);
        propItem.Save();
    }

    // -------------------------------------------------------------------------
    // GridRow visual property helpers
    // -------------------------------------------------------------------------

    private static void ApplyGridRowVisualProperties(GridRow row, SerializedGridRow dto)
    {
        if (!string.IsNullOrEmpty(dto.Container))
            row.Container = dto.Container;
        row.ContainerWidth = dto.ContainerWidth;
        row.BackgroundImage = dto.BackgroundImage ?? string.Empty;
        row.ColorSchemeId = dto.ColorSchemeId ?? string.Empty;
        row.TopSpacing = dto.TopSpacing;
        row.BottomSpacing = dto.BottomSpacing;
        row.GapX = dto.GapX;
        row.GapY = dto.GapY;
        row.MobileLayout = dto.MobileLayout ?? string.Empty;
        if (!string.IsNullOrEmpty(dto.VerticalAlignment) &&
            Enum.TryParse<Dynamicweb.Content.Styles.VerticalAlignment>(dto.VerticalAlignment, true, out var va))
            row.VerticalAlignment = va;
        row.FlexibleColumns = dto.FlexibleColumns ?? string.Empty;
    }

    // -------------------------------------------------------------------------
    // Item field persistence via ItemService
    // -------------------------------------------------------------------------

    private static readonly HashSet<string> ItemSystemFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "ItemInstanceType", "Sort", "GlobalRecordPageGuid"
    };

    /// <summary>
    /// Saves Item fields using ItemService.GetItem + DeserializeFrom + Save.
    /// The paragraph.Item[key] = value approach does not persist to the ItemType table.
    /// Implements source-wins: fields present in the item type definition but absent
    /// from the serialized YAML are explicitly set to null so stale target data is cleared.
    /// </summary>
    private void SaveItemFields(string? itemType, string itemId, Dictionary<string, object> fields, IReadOnlySet<string>? excludeFields = null)
    {
        if (string.IsNullOrEmpty(itemType))
            return;

        var itemEntry = Services.Items.GetItem(itemType, itemId);
        if (itemEntry == null)
        {
            Log($"WARNING: Could not load ItemEntry for type={itemType}, id={itemId}");
            return;
        }

        var contentFields = fields
            .Where(kvp => !ItemSystemFields.Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);

        // Source-wins: null out item fields not present in the serialized data.
        // Without this, stale target values (e.g. invalid button data) survive sync.
        foreach (var fieldName in itemEntry.Names)
        {
            if (!ItemSystemFields.Contains(fieldName) && !contentFields.ContainsKey(fieldName))
            {
                // Skip guard: do NOT null out fields that were intentionally excluded from serialization (FILT-03)
                if (excludeFields != null && excludeFields.Contains(fieldName))
                    continue;
                contentFields[fieldName] = null;
            }
        }

        if (contentFields.Count == 0)
            return;

        itemEntry.DeserializeFrom(contentFields);
        itemEntry.Save();
    }

    // -------------------------------------------------------------------------
    // Page property assignment helper (shared by INSERT and UPDATE paths)
    // -------------------------------------------------------------------------

    private static void ApplyPageProperties(Page page, SerializedPage dto)
    {
        // Flat scalars
        page.NavigationTag = dto.NavigationTag;
        page.ShortCut = dto.ShortCut;
        page.Hidden = dto.Hidden;
        page.Allowclick = dto.Allowclick;
        page.Allowsearch = dto.Allowsearch;
        page.ShowInSitemap = dto.ShowInSitemap;
        page.ShowInLegend = dto.ShowInLegend;
        page.SslMode = dto.SslMode;
        page.ColorSchemeId = dto.ColorSchemeId;
        page.ExactUrl = dto.ExactUrl;
        page.ContentType = dto.ContentType;
        page.TopImage = dto.TopImage;
        page.PermissionType = dto.PermissionType;

        // DisplayMode -- parse from string, skip if not parseable
        if (!string.IsNullOrEmpty(dto.DisplayMode) &&
            Enum.TryParse<Dynamicweb.Content.DisplayMode>(dto.DisplayMode, true, out var dm))
            page.DisplayMode = dm;

        // ActiveFrom/ActiveTo -- only set when DTO has non-null values
        // (DW defaults to DateTime.Now / DateHelper.MaxDate() -- do not overwrite)
        if (dto.ActiveFrom.HasValue)
            page.ActiveFrom = dto.ActiveFrom.Value;
        if (dto.ActiveTo.HasValue)
            page.ActiveTo = dto.ActiveTo.Value;

        // SEO sub-object
        if (dto.Seo != null)
        {
            page.MetaTitle = dto.Seo.MetaTitle;
            page.MetaCanonical = dto.Seo.MetaCanonical;
            page.Description = dto.Seo.Description;
            page.Keywords = dto.Seo.Keywords;
            page.Noindex = dto.Seo.Noindex;
            page.Nofollow = dto.Seo.Nofollow;
            page.Robots404 = dto.Seo.Robots404;
        }

        // URL settings sub-object
        if (dto.UrlSettings != null)
        {
            page.UrlDataProviderTypeName = dto.UrlSettings.UrlDataProviderTypeName;
            page.UrlDataProviderParameters = XmlFormatter.CompactWithMerge(dto.UrlSettings.UrlDataProviderParameters, page.UrlDataProviderParameters);
            page.UrlIgnoreForChildren = dto.UrlSettings.UrlIgnoreForChildren;
            page.UrlUseAsWritten = dto.UrlSettings.UrlUseAsWritten;
        }

        // Visibility sub-object
        if (dto.Visibility != null)
        {
            page.HideForPhones = dto.Visibility.HideForPhones;
            page.HideForTablets = dto.Visibility.HideForTablets;
            page.HideForDesktops = dto.Visibility.HideForDesktops;
        }

        // NavigationSettings -- ONLY create when UseEcomGroups is true (per research pitfall 3)
        if (dto.NavigationSettings != null && dto.NavigationSettings.UseEcomGroups)
        {
            page.NavigationSettings = new PageNavigationSettings
            {
                UseEcomGroups = true,
                Groups = dto.NavigationSettings.Groups,
                ShopID = dto.NavigationSettings.ShopID,
                MaxLevels = dto.NavigationSettings.MaxLevels,
                ProductPage = dto.NavigationSettings.ProductPage,
                NavigationProvider = dto.NavigationSettings.NavigationProvider,
                IncludeProducts = dto.NavigationSettings.IncludeProducts
            };
            if (Enum.TryParse<EcommerceNavigationParentType>(
                dto.NavigationSettings.ParentType, true, out var pt))
                page.NavigationSettings.ParentType = pt;
        }
    }

    // -------------------------------------------------------------------------
    // Phase 39: Seed-mode field-level merge helpers (D-01..D-07, D-11, D-19)
    // -------------------------------------------------------------------------

    /// <summary>
    /// D-05: applies the DTO's flat scalars (MenuText, UrlName, Active, Sort, ItemType,
    /// LayoutTemplate, LayoutApplyToSubPages, IsFolder, TreeSection) to the existing page
    /// only when the target value is unset per <see cref="MergePredicate"/>. Returns filled
    /// count; increments <paramref name="left"/> for each target-set skip.
    /// D-10 tradeoff: false/0/empty count as unset — documented in 39-CONTEXT.md.
    /// </summary>
    private static int MergePageScalars(Page existingPage, SerializedPage dto, ref int left)
    {
        int filled = 0;

        if (MergePredicate.IsUnsetForMerge(existingPage.MenuText)) { existingPage.MenuText = dto.MenuText; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(existingPage.UrlName))  { existingPage.UrlName = dto.UrlName;  filled++; } else left++;
        // D-10 tradeoff: false counts as unset for Active.
        if (MergePredicate.IsUnsetForMerge(existingPage.Active))   { existingPage.Active = dto.IsActive; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(existingPage.Sort))     { existingPage.Sort = dto.SortOrder;  filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(existingPage.ItemType)) { existingPage.ItemType = dto.ItemType ?? string.Empty; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(existingPage.LayoutTemplate)) { existingPage.LayoutTemplate = dto.Layout ?? string.Empty; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(existingPage.LayoutApplyToSubPages)) { existingPage.LayoutApplyToSubPages = dto.LayoutApplyToSubPages; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(existingPage.IsFolder)) { existingPage.IsFolder = dto.IsFolder; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(existingPage.TreeSection)) { existingPage.TreeSection = dto.TreeSection ?? string.Empty; filled++; } else left++;

        return filled;
    }

    /// <summary>
    /// D-04: per-property merge for Page properties and sub-object DTOs (Seo, UrlSettings,
    /// Visibility, NavigationSettings). Mirrors the structure of <see cref="ApplyPageProperties"/>
    /// but gates every assignment through <see cref="MergePredicate.IsUnsetForMerge(object?, System.Type)"/>.
    /// Returns filled count; increments <paramref name="left"/> for each target-set skip.
    /// </summary>
    private static int ApplyPagePropertiesWithMerge(Page page, SerializedPage dto, ref int left)
    {
        int filled = 0;

        // Flat scalars (the ~30 Phase-23 properties)
        if (MergePredicate.IsUnsetForMerge(page.NavigationTag))  { page.NavigationTag = dto.NavigationTag; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.ShortCut))       { page.ShortCut = dto.ShortCut; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.Hidden))         { page.Hidden = dto.Hidden; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.Allowclick))     { page.Allowclick = dto.Allowclick; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.Allowsearch))    { page.Allowsearch = dto.Allowsearch; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.ShowInSitemap))  { page.ShowInSitemap = dto.ShowInSitemap; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.ShowInLegend))   { page.ShowInLegend = dto.ShowInLegend; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.SslMode))        { page.SslMode = dto.SslMode; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.ColorSchemeId))  { page.ColorSchemeId = dto.ColorSchemeId; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.ExactUrl))       { page.ExactUrl = dto.ExactUrl; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.ContentType))    { page.ContentType = dto.ContentType; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.TopImage))       { page.TopImage = dto.TopImage; filled++; } else left++;
        if (MergePredicate.IsUnsetForMerge(page.PermissionType)) { page.PermissionType = dto.PermissionType; filled++; } else left++;

        // DisplayMode -- parse from string, only fill when target DisplayMode is at enum default.
        if (!string.IsNullOrEmpty(dto.DisplayMode) &&
            Enum.TryParse<Dynamicweb.Content.DisplayMode>(dto.DisplayMode, true, out var dm))
        {
            if (MergePredicate.IsUnsetForMerge(page.DisplayMode, typeof(Dynamicweb.Content.DisplayMode))) { page.DisplayMode = dm; filled++; } else left++;
        }

        // ActiveFrom / ActiveTo: gated by MergePredicate (DateTime.MinValue = unset).
        if (dto.ActiveFrom.HasValue)
        {
            if (MergePredicate.IsUnsetForMerge(page.ActiveFrom)) { page.ActiveFrom = dto.ActiveFrom.Value; filled++; } else left++;
        }
        if (dto.ActiveTo.HasValue)
        {
            if (MergePredicate.IsUnsetForMerge(page.ActiveTo)) { page.ActiveTo = dto.ActiveTo.Value; filled++; } else left++;
        }

        // SEO sub-object — D-04: per-property merge.
        if (dto.Seo != null)
        {
            if (MergePredicate.IsUnsetForMerge(page.MetaTitle))     { page.MetaTitle = dto.Seo.MetaTitle; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.MetaCanonical)) { page.MetaCanonical = dto.Seo.MetaCanonical; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.Description))   { page.Description = dto.Seo.Description; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.Keywords))      { page.Keywords = dto.Seo.Keywords; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.Noindex))       { page.Noindex = dto.Seo.Noindex; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.Nofollow))      { page.Nofollow = dto.Seo.Nofollow; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.Robots404))     { page.Robots404 = dto.Seo.Robots404; filled++; } else left++;
        }

        // URL settings sub-object — D-04.
        if (dto.UrlSettings != null)
        {
            if (MergePredicate.IsUnsetForMerge(page.UrlDataProviderTypeName)) { page.UrlDataProviderTypeName = dto.UrlSettings.UrlDataProviderTypeName; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.UrlDataProviderParameters))
            {
                page.UrlDataProviderParameters = XmlFormatter.CompactWithMerge(dto.UrlSettings.UrlDataProviderParameters, page.UrlDataProviderParameters);
                filled++;
            }
            else left++;
            if (MergePredicate.IsUnsetForMerge(page.UrlIgnoreForChildren)) { page.UrlIgnoreForChildren = dto.UrlSettings.UrlIgnoreForChildren; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.UrlUseAsWritten))      { page.UrlUseAsWritten = dto.UrlSettings.UrlUseAsWritten; filled++; } else left++;
        }

        // Visibility sub-object — D-04.
        if (dto.Visibility != null)
        {
            if (MergePredicate.IsUnsetForMerge(page.HideForPhones))   { page.HideForPhones = dto.Visibility.HideForPhones; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.HideForTablets))  { page.HideForTablets = dto.Visibility.HideForTablets; filled++; } else left++;
            if (MergePredicate.IsUnsetForMerge(page.HideForDesktops)) { page.HideForDesktops = dto.Visibility.HideForDesktops; filled++; } else left++;
        }

        // NavigationSettings — Pitfall 5: if the whole sub-object is null on target but
        // YAML has one, construct it fresh; otherwise per-property D-04 merge.
        if (dto.NavigationSettings != null && dto.NavigationSettings.UseEcomGroups)
        {
            if (page.NavigationSettings == null)
            {
                page.NavigationSettings = new PageNavigationSettings
                {
                    UseEcomGroups = true,
                    Groups = dto.NavigationSettings.Groups,
                    ShopID = dto.NavigationSettings.ShopID,
                    MaxLevels = dto.NavigationSettings.MaxLevels,
                    ProductPage = dto.NavigationSettings.ProductPage,
                    NavigationProvider = dto.NavigationSettings.NavigationProvider,
                    IncludeProducts = dto.NavigationSettings.IncludeProducts
                };
                if (Enum.TryParse<EcommerceNavigationParentType>(
                    dto.NavigationSettings.ParentType, true, out var pt))
                    page.NavigationSettings.ParentType = pt;
                filled++;
            }
            else
            {
                // Per-property merge for the nested settings.
                if (MergePredicate.IsUnsetForMerge(page.NavigationSettings.Groups))             { page.NavigationSettings.Groups = dto.NavigationSettings.Groups; filled++; } else left++;
                if (MergePredicate.IsUnsetForMerge(page.NavigationSettings.ShopID))             { page.NavigationSettings.ShopID = dto.NavigationSettings.ShopID; filled++; } else left++;
                if (MergePredicate.IsUnsetForMerge(page.NavigationSettings.MaxLevels))          { page.NavigationSettings.MaxLevels = dto.NavigationSettings.MaxLevels; filled++; } else left++;
                if (MergePredicate.IsUnsetForMerge(page.NavigationSettings.ProductPage))        { page.NavigationSettings.ProductPage = dto.NavigationSettings.ProductPage; filled++; } else left++;
                if (MergePredicate.IsUnsetForMerge(page.NavigationSettings.NavigationProvider)) { page.NavigationSettings.NavigationProvider = dto.NavigationSettings.NavigationProvider; filled++; } else left++;
                if (MergePredicate.IsUnsetForMerge(page.NavigationSettings.IncludeProducts))    { page.NavigationSettings.IncludeProducts = dto.NavigationSettings.IncludeProducts; filled++; } else left++;

                if (Enum.TryParse<EcommerceNavigationParentType>(dto.NavigationSettings.ParentType, true, out var pt2))
                {
                    if (MergePredicate.IsUnsetForMerge(page.NavigationSettings.ParentType, typeof(EcommerceNavigationParentType))) { page.NavigationSettings.ParentType = pt2; filled++; } else left++;
                }
            }
        }

        return filled;
    }

    /// <summary>
    /// D-02: field-level merge for ItemFields. Reads current target values via
    /// <c>ItemEntry.SerializeTo</c>, fills only entries where the target string is
    /// NULL or empty (D-02 string rule), overlays onto the current dict to prevent
    /// sibling clearing (Pitfall 7 defense), then <c>DeserializeFrom + Save</c>.
    /// Returns filled count; increments <paramref name="left"/> for each skip.
    /// </summary>
    private int MergeItemFields(
        string? itemType,
        string itemId,
        Dictionary<string, object> yamlFields,
        IReadOnlySet<string>? excludeFields,
        ref int left)
    {
        if (string.IsNullOrEmpty(itemType)) return 0;

        var itemEntry = Services.Items.GetItem(itemType, itemId);
        if (itemEntry == null)
        {
            Log($"WARNING: Could not load ItemEntry for type={itemType}, id={itemId}");
            return 0;
        }

        var currentDict = new Dictionary<string, object?>();
        itemEntry.SerializeTo(currentDict);

        int filled = 0;
        foreach (var kvp in yamlFields)
        {
            if (ItemSystemFields.Contains(kvp.Key)) continue;
            if (excludeFields != null && excludeFields.Contains(kvp.Key)) continue;

            currentDict.TryGetValue(kvp.Key, out var currentVal);
            if (MergePredicate.IsUnsetForMerge(currentVal?.ToString()))
            {
                currentDict[kvp.Key] = kvp.Value;   // overlay filled onto current (Pitfall 7)
                filled++;
            }
            else
            {
                left++;
            }
        }

        if (filled == 0) return 0;

        itemEntry.DeserializeFrom(currentDict);
        itemEntry.Save();
        return filled;
    }

    /// <summary>
    /// D-03: field-level merge for PropertyItem fields (Icon, SubmenuType, etc.).
    /// Same shape as <see cref="MergeItemFields"/> — live-read current target values,
    /// overlay only unset entries, save once.
    /// </summary>
    private int MergePropertyItemFields(
        Page page,
        Dictionary<string, object> propertyFields,
        IReadOnlySet<string>? excludeFields,
        ref int left)
    {
        if (propertyFields.Count == 0) return 0;
        if (string.IsNullOrEmpty(page.PropertyItemId))
        {
            Log($"  Page {page.UniqueId} has no PropertyItemId — cannot merge property fields");
            return 0;
        }

        var propItem = page.PropertyItem;
        if (propItem == null)
        {
            Log($"  WARNING: Could not load PropertyItem for page {page.UniqueId}");
            return 0;
        }

        var currentDict = new Dictionary<string, object?>();
        propItem.SerializeTo(currentDict);

        int filled = 0;
        foreach (var kvp in propertyFields)
        {
            if (ItemSystemFields.Contains(kvp.Key)) continue;
            if (excludeFields != null && excludeFields.Contains(kvp.Key)) continue;

            currentDict.TryGetValue(kvp.Key, out var currentVal);
            if (MergePredicate.IsUnsetForMerge(currentVal?.ToString()))
            {
                currentDict[kvp.Key] = kvp.Value;
                filled++;
            }
            else
            {
                left++;
            }
        }

        if (filled == 0) return 0;

        propItem.DeserializeFrom(currentDict);
        propItem.Save();
        return filled;
    }

    /// <summary>
    /// D-19: per-field dry-run diff for the Seed-merge path. Emits
    /// <c>"  would fill [col=X]: target=&lt;unset&gt; -&gt; seed='...'"</c> lines
    /// only where the merge would actually fire on a live run. No DW-API writes.
    /// </summary>
    /// <remarks>
    /// Dry-run log output includes YAML field values. Do not enable dry-run in
    /// logs that flow to untrusted parties — Phase 39 threat model T-39-01-03.
    /// </remarks>
    private void LogSeedMergeDryRun(SerializedPage dto, Page existing, IReadOnlySet<string>? excludeFields, WriteContext ctx)
    {
        var diffs = new List<string>();

        void Consider(string col, string? target, object? seedValue)
        {
            if (MergePredicate.IsUnsetForMerge(target))
                diffs.Add($"  would fill [col={col}]: target=<unset> -> seed='{seedValue}'");
        }

        void ConsiderInt(string col, int target, object seedValue)
        {
            if (MergePredicate.IsUnsetForMerge(target))
                diffs.Add($"  would fill [col={col}]: target=<unset> -> seed='{seedValue}'");
        }

        void ConsiderBool(string col, bool target, object seedValue)
        {
            if (MergePredicate.IsUnsetForMerge(target))
                diffs.Add($"  would fill [col={col}]: target=<unset> -> seed='{seedValue}'");
        }

        // Flat scalars
        Consider("MenuText", existing.MenuText, dto.MenuText);
        Consider("UrlName", existing.UrlName, dto.UrlName);
        ConsiderBool("Active", existing.Active, dto.IsActive);
        ConsiderInt("Sort", existing.Sort, dto.SortOrder);
        Consider("ItemType", existing.ItemType, dto.ItemType);
        Consider("LayoutTemplate", existing.LayoutTemplate, dto.Layout);
        Consider("TreeSection", existing.TreeSection, dto.TreeSection);

        // SEO sub-object
        if (dto.Seo != null)
        {
            Consider("MetaTitle", existing.MetaTitle, dto.Seo.MetaTitle);
            Consider("MetaCanonical", existing.MetaCanonical, dto.Seo.MetaCanonical);
            Consider("Description", existing.Description, dto.Seo.Description);
            Consider("Keywords", existing.Keywords, dto.Seo.Keywords);
        }

        // ItemFields — live-read current target
        if (!string.IsNullOrEmpty(existing.ItemType))
        {
            var itemEntry = Services.Items.GetItem(existing.ItemType, existing.ItemId);
            if (itemEntry != null)
            {
                var currentDict = new Dictionary<string, object?>();
                itemEntry.SerializeTo(currentDict);
                foreach (var kvp in dto.Fields)
                {
                    if (ItemSystemFields.Contains(kvp.Key)) continue;
                    if (excludeFields != null && excludeFields.Contains(kvp.Key)) continue;
                    currentDict.TryGetValue(kvp.Key, out var curr);
                    if (MergePredicate.IsUnsetForMerge(curr?.ToString()))
                        diffs.Add($"  would fill [col={kvp.Key}]: target=<unset> -> seed='{kvp.Value}'");
                }
            }
        }

        Log($"[DRY-RUN] Seed-merge: page {dto.PageUniqueId} (ID={existing.ID}) - {diffs.Count} would-fills");
        foreach (var d in diffs) Log(d);

        if (diffs.Count == 0) ctx.Skipped++;
        else ctx.Updated++;
    }

    // -------------------------------------------------------------------------
    // Phase 2: Internal link resolution
    // -------------------------------------------------------------------------

    private void ResolveLinksInArea(int areaId, InternalLinkResolver resolver)
    {
        // Resolve internal links in area-level ItemType fields (AREA-02)
        var targetArea = Services.Areas.GetArea(areaId);
        if (targetArea != null && !string.IsNullOrEmpty(targetArea.ItemType) && !string.IsNullOrEmpty(targetArea.ItemId))
        {
            ResolveLinksInItemFields(targetArea.ItemType, targetArea.ItemId, resolver);
        }

        // Re-read all pages in the area and scan their item fields for internal links
        var allPages = Services.Pages.GetPagesByAreaID(areaId);
        foreach (var page in allPages)
        {
            // Resolve item fields (link fields, button fields, rich text HTML)
            ResolveLinksInItemFields(page.ItemType, page.ItemId, resolver);

            // Resolve PropertyItem fields (Icon, SubmenuType, etc.)
            ResolveLinksInPropertyItem(page, resolver);

            // Resolve ShortCut link (PAGE-02) -- e.g., "Default.aspx?ID=42" -> "Default.aspx?ID=99"
            bool pageNeedsResave = false;
            if (!string.IsNullOrEmpty(page.ShortCut))
            {
                var resolved = resolver.ResolveLinks(page.ShortCut);
                if (resolved != page.ShortCut)
                {
                    page.ShortCut = resolved;
                    pageNeedsResave = true;
                }
            }

            // Resolve NavigationSettings.ProductPage link (ECOM-02)
            if (page.NavigationSettings?.ProductPage != null)
            {
                var resolved = resolver.ResolveLinks(page.NavigationSettings.ProductPage);
                if (resolved != page.NavigationSettings.ProductPage)
                {
                    page.NavigationSettings.ProductPage = resolved;
                    pageNeedsResave = true;
                }
            }

            if (pageNeedsResave)
                Services.Pages.SavePage(page);

            // Resolve paragraph item fields
            var paragraphs = Services.Paragraphs.GetParagraphsByPageId(page.ID);
            foreach (var para in paragraphs)
            {
                ResolveLinksInItemFields(para.ItemType, para.ItemId, resolver);
            }
        }
    }

    private void ResolveLinksInItemFields(string? itemType, string? itemId, InternalLinkResolver resolver)
    {
        if (string.IsNullOrEmpty(itemType) || string.IsNullOrEmpty(itemId))
            return;

        var item = Services.Items.GetItem(itemType, itemId);
        if (item == null)
            return;

        var fields = new Dictionary<string, object?>();
        item.SerializeTo(fields);

        bool anyChanged = false;
        var updatedFields = new Dictionary<string, object?>();

        foreach (var kvp in fields)
        {
            if (kvp.Value is string strValue && strValue.Length > 0)
            {
                var resolved = resolver.ResolveLinks(strValue);
                if (resolved != strValue)
                {
                    updatedFields[kvp.Key] = resolved;
                    anyChanged = true;
                }
                else
                {
                    updatedFields[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                updatedFields[kvp.Key] = kvp.Value;
            }
        }

        if (anyChanged)
        {
            if (_isDryRun)
            {
                Log($"[DRY-RUN] Would resolve links in {itemType}/{itemId}");
                return;
            }
            item.DeserializeFrom(updatedFields);
            item.Save();
        }
    }

    private void ResolveLinksInPropertyItem(Page page, InternalLinkResolver resolver)
    {
        if (string.IsNullOrEmpty(page.PropertyItemId))
            return;

        var propItem = page.PropertyItem;
        if (propItem == null)
            return;

        var fields = new Dictionary<string, object?>();
        propItem.SerializeTo(fields);

        bool anyChanged = false;
        var updatedFields = new Dictionary<string, object?>();

        foreach (var kvp in fields)
        {
            if (kvp.Value is string strValue && strValue.Length > 0)
            {
                var resolved = resolver.ResolveLinks(strValue);
                if (resolved != strValue)
                {
                    updatedFields[kvp.Key] = resolved;
                    anyChanged = true;
                }
                else
                {
                    updatedFields[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                updatedFields[kvp.Key] = kvp.Value;
            }
        }

        if (anyChanged)
        {
            if (_isDryRun)
            {
                Log($"[DRY-RUN] Would resolve links in PropertyItem of page {page.UniqueId}");
                return;
            }
            propItem.DeserializeFrom(updatedFields);
            propItem.Save();
        }
    }

    // -------------------------------------------------------------------------
    // Dry-run diff logging
    // -------------------------------------------------------------------------

    private void LogDryRunPageUpdate(SerializedPage dto, Page? existing, WriteContext ctx)
    {
        if (existing == null)
        {
            Log($"[DRY-RUN] UPDATE page {dto.PageUniqueId} (could not load existing for diff)");
            ctx.Updated++;
            return;
        }

        var diffs = new List<string>();

        if (dto.MenuText != existing.MenuText)
            diffs.Add($"MenuText: '{existing.MenuText}' -> '{dto.MenuText}'");
        if (dto.UrlName != existing.UrlName)
            diffs.Add($"UrlName: '{existing.UrlName}' -> '{dto.UrlName}'");
        if (dto.IsActive != existing.Active)
            diffs.Add($"Active: {existing.Active} -> {dto.IsActive}");
        if (dto.SortOrder != existing.Sort)
            diffs.Add($"Sort: {existing.Sort} -> {dto.SortOrder}");

        // Field-level diffs for ItemType fields
        foreach (var kvp in dto.Fields)
        {
            var currentVal = existing.Item?[kvp.Key]?.ToString();
            var newVal = kvp.Value?.ToString();
            if (currentVal != newVal)
                diffs.Add($"Fields[{kvp.Key}]: '{currentVal}' -> '{newVal}'");
        }

        // PropertyFields diffs (e.g. Icon, SubmenuType)
        if (existing.PropertyItem != null && dto.PropertyFields.Count > 0)
        {
            var existingPropFields = new Dictionary<string, object?>();
            existing.PropertyItem.SerializeTo(existingPropFields);

            foreach (var kvp in dto.PropertyFields)
            {
                if (ItemSystemFields.Contains(kvp.Key)) continue;
                existingPropFields.TryGetValue(kvp.Key, out var currentVal);
                var currentStr = currentVal?.ToString();
                var newStr = kvp.Value?.ToString();
                if (currentStr != newStr)
                    diffs.Add($"PropertyFields[{kvp.Key}]: '{currentStr}' -> '{newStr}'");
            }
        }
        else if (existing.PropertyItem == null && dto.PropertyFields.Count > 0)
        {
            // No existing PropertyItem but YAML has property fields — log all as new
            foreach (var kvp in dto.PropertyFields)
            {
                if (ItemSystemFields.Contains(kvp.Key)) continue;
                diffs.Add($"PropertyFields[{kvp.Key}]: '' -> '{kvp.Value}'");
            }
        }

        if (dto.Permissions.Count > 0)
            diffs.Add($"Would apply {dto.Permissions.Count} permission(s)");

        if (diffs.Count == 0)
        {
            Log($"[DRY-RUN] SKIP {dto.PageUniqueId} (unchanged)");
            ctx.Skipped++;
        }
        else
        {
            Log($"[DRY-RUN] UPDATE {dto.PageUniqueId}:\n  " + string.Join("\n  ", diffs));
            ctx.Updated++;
        }
    }

    private void LogDryRunParagraphUpdate(SerializedParagraph dto, Paragraph existing, WriteContext ctx)
    {
        var diffs = new List<string>();

        if (dto.SortOrder != existing.Sort)
            diffs.Add($"Sort: {existing.Sort} -> {dto.SortOrder}");
        if (dto.Header != existing.Header)
            diffs.Add($"Header: '{existing.Header}' -> '{dto.Header}'");
        if (dto.ItemType != existing.ItemType)
            diffs.Add($"ItemType: '{existing.ItemType}' -> '{dto.ItemType}'");

        // Field-level diffs for ItemType fields
        foreach (var kvp in dto.Fields)
        {
            string? currentVal;
            if (kvp.Key == "Text")
                currentVal = existing.Text;
            else
                currentVal = existing.Item?[kvp.Key]?.ToString();

            var newVal = kvp.Value?.ToString();
            if (currentVal != newVal)
                diffs.Add($"Fields[{kvp.Key}]: '{currentVal}' -> '{newVal}'");
        }

        if (diffs.Count == 0)
        {
            Log($"[DRY-RUN] SKIP {dto.ParagraphUniqueId} (unchanged)");
            ctx.Skipped++;
        }
        else
        {
            Log($"[DRY-RUN] UPDATE {dto.ParagraphUniqueId}:\n  " + string.Join("\n  ", diffs));
            ctx.Updated++;
        }
    }
}

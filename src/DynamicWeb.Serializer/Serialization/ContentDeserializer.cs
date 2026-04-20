using Dynamicweb.Content;
using Dynamicweb.Data;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;

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
    private readonly HashSet<string> _loggedTemplateMissing = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loggedAreaColumnMissing = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? _targetAreaColumns;
    private Dictionary<string, string>? _targetAreaColumnTypes;
    private readonly PermissionMapper _permissionMapper;

    /// <summary>
    /// When <see cref="ConflictStrategy.DestinationWins"/> (Phase 37-01 Seed mode), pages whose
    /// <c>PageUniqueId</c> is already present on target are NOT updated — the target row is
    /// preserved exactly as-is. INSERT paths (new pages) still run normally. Nested content
    /// (paragraphs within an existing page) follows up in later plans.
    /// </summary>
    public ContentDeserializer(
        SerializerConfiguration configuration,
        IContentStore? store = null,
        Action<string>? log = null,
        bool isDryRun = false,
        string? filesRoot = null,
        ConflictStrategy conflictStrategy = ConflictStrategy.SourceWins)
    {
        _configuration = configuration;
        _store = store ?? new FileSystemStore();
        _log = log;
        _isDryRun = isDryRun;
        _filesRoot = filesRoot;
        _conflictStrategy = conflictStrategy;
        _permissionMapper = new PermissionMapper(log);
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

        if (_loggedTemplateMissing.Count > 0)
            Log($"Template validation: {_loggedTemplateMissing.Count} missing template reference(s) detected — see warnings above");

        return aggregated;
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
                    Services.Areas.SaveArea(targetArea);
                    Services.Areas.ClearCache();
                    Log($"Created area Item: type={area.ItemType}, id={targetAreaItemId}");
                }
                catch (Exception ex)
                {
                    Log($"WARNING: Could not create area Item: {ex.Message}");
                }
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
    /// Returns the set of column names present on the target [Area] table, cached for the
    /// duration of this deserialize run. Used to gracefully skip source columns that do
    /// not exist on the target — prevents "Invalid column name" hard-fails when source
    /// and target DBs are on different DW schema versions.
    /// </summary>
    private HashSet<string> GetTargetAreaColumns()
    {
        EnsureTargetAreaSchema();
        return _targetAreaColumns!;
    }

    private void EnsureTargetAreaSchema()
    {
        if (_targetAreaColumns != null) return;

        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cb = new CommandBuilder();
        cb.Add("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Area'");
        using var reader = Database.CreateDataReader(cb);
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var type = reader.GetString(1);
            cols.Add(name);
            types[name] = type;
        }

        _targetAreaColumns = cols;
        _targetAreaColumnTypes = types;
    }

    /// <summary>
    /// Coerces a YAML-parsed value into the CLR type the target SQL column expects.
    /// YAML has no type hints for <c>Dictionary&lt;string, object&gt;</c> values, so a
    /// column like [AreaCreatedDate] arrives here as a <see cref="string"/> despite the
    /// target being <c>datetime</c>. SQL Server's implicit conversion fails on certain
    /// ISO strings (7-digit fractional seconds, etc.), so we parse explicitly.
    /// </summary>
    private object CoerceForColumn(string columnName, object? value)
    {
        if (value == null || value is DBNull) return DBNull.Value;
        if (_targetAreaColumnTypes == null) return value;
        if (!_targetAreaColumnTypes.TryGetValue(columnName, out var dataType)) return value;

        if (value is string s)
        {
            switch (dataType.ToLowerInvariant())
            {
                case "datetime":
                case "datetime2":
                case "smalldatetime":
                case "date":
                    if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
                    if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var dt))
                        return dt;
                    break;
                case "datetimeoffset":
                    if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
                    if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.RoundtripKind,
                            out var dto))
                        return dto;
                    break;
                case "bit":
                    if (bool.TryParse(s, out var b)) return b;
                    break;
                case "int":
                case "smallint":
                case "tinyint":
                    if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
                    if (int.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var i)) return i;
                    break;
                case "bigint":
                    if (string.IsNullOrWhiteSpace(s)) return DBNull.Value;
                    if (long.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var l)) return l;
                    break;
            }
        }
        return value;
    }

    /// <summary>
    /// Write area properties back to the [Area] table via SQL UPDATE.
    /// Skips columns in excludeFields to preserve environment-specific values.
    /// Also skips columns not present on the target schema (logs a warning once per column).
    /// </summary>
    private void WriteAreaProperties(int areaId, Dictionary<string, object> properties, IReadOnlySet<string>? excludeFields, IReadOnlySet<string>? excludeAreaColumns = null)
    {
        if (properties.Count == 0) return;

        var targetCols = GetTargetAreaColumns();

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
                if (_loggedAreaColumnMissing.Add(kvp.Key))
                    Log($"WARNING: source column [Area].[{kvp.Key}] not present on target schema — skipping");
                continue;
            }

            var coerced = CoerceForColumn(kvp.Key, kvp.Value);
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
        Database.ExecuteNonQuery(cb);
    }

    /// <summary>
    /// Create a new area row via SQL INSERT using serialized properties.
    /// Called when the target area does not exist (AREA-04).
    /// </summary>
    private void CreateAreaFromProperties(int areaId, SerializedArea area, IReadOnlySet<string>? excludeFields)
    {
        var columns = new List<string> { "[AreaID]", "[AreaName]", "[AreaSort]", "[AreaUniqueId]" };
        var values = new List<object> { areaId, area.Name, area.SortOrder, area.AreaId };

        var targetCols = GetTargetAreaColumns();
        foreach (var kvp in area.Properties)
        {
            if (excludeFields?.Contains(kvp.Key) == true) continue;
            if (!targetCols.Contains(kvp.Key))
            {
                if (_loggedAreaColumnMissing.Add(kvp.Key))
                    Log($"WARNING: source column [Area].[{kvp.Key}] not present on target schema — skipping");
                continue;
            }
            columns.Add($"[{kvp.Key}]");
            values.Add(CoerceForColumn(kvp.Key, kvp.Value));
        }

        var cb = new CommandBuilder();
        cb.Add($"INSERT INTO [Area] ({string.Join(", ", columns)}) VALUES (");
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) cb.Add(", ");
            cb.Add("{0}", values[i]);
        }
        cb.Add(")");
        Database.ExecuteNonQuery(cb);
    }

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
        ValidatePageLayout(dto.Layout);
        ValidateItemType(dto.ItemType);

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

            // Seed mode (D-06, Phase 37-01): page is already present on target, preserve as-is.
            // We still return the existingId so child pages / paragraphs resolve their ParentPageId
            // correctly during the recursive walk — skipping means "don't overwrite", not "disown".
            if (_conflictStrategy == ConflictStrategy.DestinationWins)
            {
                Log($"Seed-skip: page {dto.PageUniqueId} (already present, ID={existingId})");
                ctx.Skipped++;
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
        ValidateGridRowDefinition(dto.DefinitionId);
        ValidateItemType(dto.ItemType);

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
        ValidateItemType(dto.ItemType);

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
    // Template validation — warns when deserialized references point to missing files
    // -------------------------------------------------------------------------

    private void ValidatePageLayout(string? layout)
    {
        if (string.IsNullOrEmpty(_filesRoot) || string.IsNullOrEmpty(layout))
            return;

        var key = $"layout:{layout}";
        if (_loggedTemplateMissing.Contains(key))
            return;

        // Layout templates live under Templates/Designs/{design}/{layout}
        var designsDir = Path.Combine(_filesRoot, "Templates", "Designs");
        if (!Directory.Exists(designsDir))
            return;

        foreach (var designDir in Directory.GetDirectories(designsDir))
        {
            if (File.Exists(Path.Combine(designDir, layout)))
                return;
        }

        _loggedTemplateMissing.Add(key);
        Log($"WARNING: Page layout template '{layout}' not found in any design folder under {designsDir}");
    }

    private void ValidateItemType(string? itemType)
    {
        if (string.IsNullOrEmpty(_filesRoot) || string.IsNullOrEmpty(itemType))
            return;

        var key = $"item:{itemType}";
        if (_loggedTemplateMissing.Contains(key))
            return;

        var itemFile = Path.Combine(_filesRoot, "System", "Items", $"ItemType_{itemType}.xml");
        if (File.Exists(itemFile))
            return;

        _loggedTemplateMissing.Add(key);
        Log($"WARNING: Item type definition 'ItemType_{itemType}.xml' not found at {itemFile}");
    }

    private void ValidateGridRowDefinition(string? definitionId)
    {
        if (string.IsNullOrEmpty(_filesRoot) || string.IsNullOrEmpty(definitionId))
            return;

        var key = $"rowdef:{definitionId}";
        if (_loggedTemplateMissing.Contains(key))
            return;

        // Row definitions live under Templates/Designs/{design}/Grid/Page/RowDefinitions/{id}.json
        var designsDir = Path.Combine(_filesRoot, "Templates", "Designs");
        if (!Directory.Exists(designsDir))
            return;

        foreach (var designDir in Directory.GetDirectories(designsDir))
        {
            var defFile = Path.Combine(designDir, "Grid", "Page", "RowDefinitions", $"{definitionId}.json");
            if (File.Exists(defFile))
                return;
        }

        _loggedTemplateMissing.Add(key);
        Log($"WARNING: Grid row definition '{definitionId}.json' not found in any design folder under {designsDir}");
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

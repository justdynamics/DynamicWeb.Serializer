using Dynamicweb.Content;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Serialization;

/// <summary>
/// Orchestrates the DW-to-disk serialization pipeline:
/// traverses the DW page tree, applies predicate filtering, maps to DTOs via ContentMapper,
/// resolves cross-references via ReferenceResolver, and writes YAML via FileSystemStore.
/// </summary>
public class ContentSerializer
{
    private readonly SerializerConfiguration _configuration;
    private readonly IContentStore _store;
    private readonly ReferenceResolver _referenceResolver;
    private readonly ContentMapper _mapper;
    private readonly PermissionMapper _permissionMapper;
    private readonly ContentPredicateSet _predicateSet;
    private readonly Action<string>? _log;

    public ContentSerializer(SerializerConfiguration configuration, IContentStore? store = null, Action<string>? log = null)
    {
        _configuration = configuration;
        _store = store ?? new FileSystemStore();
        _referenceResolver = new ReferenceResolver();
        _mapper = new ContentMapper(_referenceResolver);
        _permissionMapper = new PermissionMapper(log);
        _predicateSet = new ContentPredicateSet(configuration);
        _log = log;
    }

    private void Log(string message) => _log?.Invoke(message);

    /// <summary>
    /// Serializes all predicates defined in the configuration to disk.
    /// Clears the reference resolver cache between predicates.
    /// Logs a count summary of pages, grid rows, and paragraphs after all predicates are processed.
    /// After all predicates complete (Phase 37-05 / TEMPLATE-01), scans the full serialized
    /// page tree for template references and emits <c>templates.manifest.yml</c> so the
    /// deserialize side can validate every cshtml / grid-row / item-type file exists on the
    /// target environment. Then runs <see cref="BaselineLinkSweeper"/> (D-22 pass 1) over the
    /// in-memory tree — any unresolvable <c>Default.aspx?ID=N</c> reference throws so the
    /// baseline is never committed with orphan links.
    /// </summary>
    public void Serialize()
    {
        int totalPages = 0, totalGridRows = 0, totalParagraphs = 0;
        var allSerializedPages = new List<SerializedPage>();

        // Phase 40 D-07: ContentSerializer is Deploy-scoped by convention. The orchestrator routes
        // Deploy and Seed predicates through separate calls into providers; ContentSerializer itself
        // emits Deploy YAML only. Filter the flat predicate list down to Deploy entries explicitly
        // so the single-mode intent is visible at the call site and matches the Deploy-only
        // exclusion-dict reads on lines 189 / 229 / 250.
        foreach (var predicate in _configuration.Predicates.Where(p => p.Mode == DeploymentMode.Deploy))
        {
            var area = SerializePredicate(predicate);
            _referenceResolver.Clear();

            if (area != null)
            {
                CountItems(area.Pages, ref totalPages, ref totalGridRows, ref totalParagraphs);
                allSerializedPages.AddRange(area.Pages);
            }
        }

        // Phase 37-05 / TEMPLATE-01: emit templates.manifest.yml listing every cshtml /
        // grid-row / item-type reference in the baseline, with per-reference source-page
        // attribution. The manifest lives at the output root (alongside area folders) so
        // ContentDeserializer can find it without knowing per-predicate subfolders.
        try
        {
            var scanner = new TemplateReferenceScanner();
            var refs = scanner.Scan(allSerializedPages);
            new TemplateAssetManifest().Write(_configuration.OutputDirectory, refs);
            Log($"Wrote {TemplateAssetManifest.ManifestFileName} with {refs.Count} template reference(s)");
        }
        catch (Exception ex)
        {
            Log($"WARNING: Failed to write template manifest: {ex.Message}");
        }

        // Phase 37-05 / LINK-02 pass 1 (D-22): sweep the in-memory tree for Default.aspx?ID=N
        // references that don't resolve to a SerializedPage.SourcePageId in the same tree.
        // Orphan references are fatal — a baseline with broken links fails at runtime on the
        // target environment and cannot be committed to git. 2026-04-20 follow-up: per-mode
        // AcknowledgedOrphanPageIds allows known-broken source data (that cannot be cleaned
        // upstream in time) to pass as warnings. Any unresolvable NOT in the acknowledged set
        // still fails.
        var sweeper = new BaselineLinkSweeper();
        var sweepResult = sweeper.Sweep(allSerializedPages);
        // Phase 38 A.3 (D-38-03): per-predicate ack list is the single source of truth.
        // Aggregate across both modes' predicates so the sweep receives the union.
        // Phase 40 D-07: read both slices off the flat predicate list filtered by Mode.
        var deployAck = _configuration.Predicates
            .Where(p => p.Mode == DeploymentMode.Deploy)
            .SelectMany(p => p.AcknowledgedOrphanPageIds)
            .ToList();
        var seedAck = _configuration.Predicates
            .Where(p => p.Mode == DeploymentMode.Seed)
            .SelectMany(p => p.AcknowledgedOrphanPageIds)
            .ToList();
        Log($"Link sweep: {sweepResult.ResolvedCount} internal link(s) verified, " +
            $"{sweepResult.Unresolved.Count} unresolvable " +
            $"(ack deploy={deployAck.Count}, seed={seedAck.Count})");
        if (sweepResult.Unresolved.Count > 0)
        {
            var acknowledged = new HashSet<int>(deployAck.Concat(seedAck));
            var (accepted, fatal) = sweepResult.Unresolved
                .GroupBy(u => acknowledged.Contains(u.UnresolvablePageId))
                .Aggregate(
                    (Accepted: new List<UnresolvedLink>(), Fatal: new List<UnresolvedLink>()),
                    (acc, grp) =>
                    {
                        if (grp.Key) acc.Accepted.AddRange(grp);
                        else acc.Fatal.AddRange(grp);
                        return acc;
                    });

            foreach (var u in accepted)
                Log($"WARNING: acknowledged orphan ID {u.UnresolvablePageId} in {u.SourcePageIdentifier} / {u.FieldName}: {u.Context}");

            if (fatal.Count > 0)
            {
                var lines = fatal.Select(u =>
                    $"  - ID {u.UnresolvablePageId} in {u.SourcePageIdentifier} / {u.FieldName}: {u.Context}");
                throw new InvalidOperationException(
                    $"Baseline link sweep found {fatal.Count} unresolvable reference(s):\n" +
                    string.Join("\n", lines) +
                    "\nFix the source baseline: include the referenced pages in a predicate path, or remove the references. " +
                    "Known-broken source refs may be listed under AcknowledgedOrphanPageIds on the owning Content predicate.");
            }
        }

        Log($"Serialization complete: {totalPages} pages, {totalGridRows} grid rows, {totalParagraphs} paragraphs serialized.");
    }

    // -------------------------------------------------------------------------
    // Private pipeline
    // -------------------------------------------------------------------------

    private SerializedArea? SerializePredicate(ProviderPredicateDefinition predicate)
    {
        var area = Services.Areas.GetArea(predicate.AreaId);
        if (area == null)
        {
            Log($"Warning: Area with ID {predicate.AreaId} not found. Skipping predicate '{predicate.Name}'.");
            return null;
        }

        Log($"Area found: ID={area.ID}, Name={area.Name}");

        // Build exclude sets from predicate config
        var excludeFields = predicate.ExcludeFields.Count > 0
            ? new HashSet<string>(predicate.ExcludeFields, StringComparer.OrdinalIgnoreCase)
            : null;
        IReadOnlyList<string>? excludeXmlElements = predicate.ExcludeXmlElements.Count > 0
            ? predicate.ExcludeXmlElements
            : null;

        // Get all top-level pages for this area
        var rootPages = Services.Pages.GetRootPagesForArea(predicate.AreaId)
            .OrderBy(p => p.Sort)
            .ToList();

        Log($"Root pages for area {predicate.AreaId}: {rootPages.Count}");
        foreach (var rp in rootPages)
            Log($"  Root page: ID={rp.ID}, MenuText='{rp.MenuText}', Name='{rp.GetDisplayName()}'");

        var serializedPages = new List<SerializedPage>();
        foreach (var rootPage in rootPages)
        {
            var contentPath = "/" + rootPage.MenuText;
            Log($"  Checking predicate for path: '{contentPath}'");
            var serializedPage = SerializePage(rootPage, predicate, contentPath, excludeFields, excludeXmlElements);
            if (serializedPage != null)
                serializedPages.Add(serializedPage);
            else
                Log($"  -> Skipped (predicate excluded or null)");
        }

        // Build area column exclude set (separate from item field excludes)
        var excludeAreaColumns = predicate.ExcludeAreaColumns.Count > 0
            ? new HashSet<string>(predicate.ExcludeAreaColumns, StringComparer.OrdinalIgnoreCase)
            : null;

        Log($"Serialized pages: {serializedPages.Count}");
        // Phase 40 D-04: exclusion dicts moved from per-ModeConfig to top-level on SerializerConfiguration.
        // ContentSerializer is Deploy-scoped, so Deploy-mode runs always read these dicts; Seed-mode
        // runs are dispatched through the orchestrator + ContentDeserializer (Phase 39 runtime, untouched).
        var serializedArea = _mapper.MapArea(area, serializedPages, excludeFields,
            _configuration.ExcludeFieldsByItemType, excludeAreaColumns);
        _store.WriteTree(serializedArea, _configuration.OutputDirectory);
        return serializedArea;
    }

    private SerializedPage? SerializePage(Page page, ProviderPredicateDefinition predicate, string contentPath, HashSet<string>? excludeFields = null, IReadOnlyList<string>? excludeXmlElements = null)
    {
        // Check predicate inclusion BEFORE loading children (short-circuit optimization)
        if (!_predicateSet.ShouldInclude(contentPath, predicate.AreaId))
        {
            Log($"  Predicate excluded: '{contentPath}'");
            return null;
        }
        Log($"  Predicate included: '{contentPath}' (page ID={page.ID})");

        // Fetch grid rows and paragraphs for this page.
        // DW allows multiple rows on the same page to share Sort (default is 0; manual
        // ordering can collide). When that happens, DW falls back to an implicit tiebreaker
        // (row creation order / ID) that is not preserved across DB boundaries — deserialize
        // creates new target rows with different IDs, so the tie resolves differently and
        // visual order flips. Sort by (Sort, ID) to stably match source display order, then
        // renumber SortOrder sequentially (1..N) so the YAML carries canonical order.
        var gridRows = Services.Grids.GetGridRowsByPageId(page.ID)
            .OrderBy(gr => gr.Sort)
            .ThenBy(gr => gr.ID)
            .ToList();

        var allParagraphs = Services.Paragraphs.GetParagraphsByPageId(page.ID)
            .ToList();

        // Map each grid row with its paragraphs grouped into columns
        var serializedGridRows = new List<SerializedGridRow>();
        for (int i = 0; i < gridRows.Count; i++)
        {
            var gridRow = gridRows[i];
            var rowParagraphs = allParagraphs
                .Where(p => p.GridRowId == gridRow.ID)
                .ToList();

            var columns = _mapper.BuildColumns(rowParagraphs, excludeFields, excludeXmlElements,
                _configuration.ExcludeFieldsByItemType, _configuration.ExcludeXmlElementsByType);
            var serializedGridRow = _mapper.MapGridRow(gridRow, columns) with { SortOrder = i + 1 };
            serializedGridRows.Add(serializedGridRow);
        }

        // Recursively process child pages
        var childPages = Services.Pages.GetPagesByParentID(page.ID)
            .OrderBy(c => c.Sort)
            .ToList();

        var serializedChildren = new List<SerializedPage>();
        foreach (var child in childPages)
        {
            var childContentPath = contentPath + "/" + child.MenuText;
            var serializedChild = SerializePage(child, predicate, childContentPath, excludeFields, excludeXmlElements);
            if (serializedChild != null)
                serializedChildren.Add(serializedChild);
        }

        var permissions = _permissionMapper.MapPermissions(page.ID);
        return _mapper.MapPage(page, serializedGridRows, serializedChildren, permissions, excludeFields, excludeXmlElements,
            _configuration.ExcludeFieldsByItemType, _configuration.ExcludeXmlElementsByType);
    }

    private static void CountItems(IEnumerable<SerializedPage> pages, ref int pageCount, ref int gridRowCount, ref int paragraphCount)
    {
        foreach (var page in pages)
        {
            pageCount++;
            gridRowCount += page.GridRows.Count;
            paragraphCount += page.GridRows.Sum(gr => gr.Columns.Sum(c => c.Paragraphs.Count));
            CountItems(page.Children, ref pageCount, ref gridRowCount, ref paragraphCount);
        }
    }
}

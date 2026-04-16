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
    /// </summary>
    public void Serialize()
    {
        int totalPages = 0, totalGridRows = 0, totalParagraphs = 0;

        foreach (var predicate in _configuration.Predicates)
        {
            var area = SerializePredicate(predicate);
            _referenceResolver.Clear();

            if (area != null)
                CountItems(area.Pages, ref totalPages, ref totalGridRows, ref totalParagraphs);
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

        // Fetch grid rows and paragraphs for this page
        var gridRows = Services.Grids.GetGridRowsByPageId(page.ID)
            .OrderBy(gr => gr.Sort)
            .ToList();

        var allParagraphs = Services.Paragraphs.GetParagraphsByPageId(page.ID)
            .ToList();

        // Map each grid row with its paragraphs grouped into columns
        var serializedGridRows = new List<SerializedGridRow>();
        foreach (var gridRow in gridRows)
        {
            var rowParagraphs = allParagraphs
                .Where(p => p.GridRowId == gridRow.ID)
                .ToList();

            var columns = _mapper.BuildColumns(rowParagraphs, excludeFields, excludeXmlElements,
                _configuration.ExcludeFieldsByItemType, _configuration.ExcludeXmlElementsByType);
            var serializedGridRow = _mapper.MapGridRow(gridRow, columns);
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

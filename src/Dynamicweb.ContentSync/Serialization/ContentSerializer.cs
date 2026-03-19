using Dynamicweb.Content;
using Dynamicweb.ContentSync.Configuration;
using Dynamicweb.ContentSync.Infrastructure;
using Dynamicweb.ContentSync.Models;

namespace Dynamicweb.ContentSync.Serialization;

/// <summary>
/// Orchestrates the DW-to-disk serialization pipeline:
/// traverses the DW page tree, applies predicate filtering, maps to DTOs via ContentMapper,
/// resolves cross-references via ReferenceResolver, and writes YAML via FileSystemStore.
/// </summary>
public class ContentSerializer
{
    private readonly SyncConfiguration _configuration;
    private readonly IContentStore _store;
    private readonly ReferenceResolver _referenceResolver;
    private readonly ContentMapper _mapper;
    private readonly ContentPredicateSet _predicateSet;
    private readonly Action<string>? _log;

    public ContentSerializer(SyncConfiguration configuration, IContentStore? store = null, Action<string>? log = null)
    {
        _configuration = configuration;
        _store = store ?? new FileSystemStore();
        _referenceResolver = new ReferenceResolver();
        _mapper = new ContentMapper(_referenceResolver);
        _predicateSet = new ContentPredicateSet(configuration);
        _log = log;
    }

    private void Log(string message) => _log?.Invoke(message);

    /// <summary>
    /// Serializes all predicates defined in the configuration to disk.
    /// Clears the reference resolver cache between predicates.
    /// </summary>
    public void Serialize()
    {
        foreach (var predicate in _configuration.Predicates)
        {
            SerializePredicate(predicate);
            _referenceResolver.Clear();
        }
    }

    // -------------------------------------------------------------------------
    // Private pipeline
    // -------------------------------------------------------------------------

    private SerializedArea? SerializePredicate(PredicateDefinition predicate)
    {
        var area = Services.Areas.GetArea(predicate.AreaId);
        if (area == null)
        {
            Log($"Warning: Area with ID {predicate.AreaId} not found. Skipping predicate '{predicate.Name}'.");
            return null;
        }

        Log($"Area found: ID={area.ID}, Name={area.Name}");

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
            var serializedPage = SerializePage(rootPage, predicate, contentPath);
            if (serializedPage != null)
                serializedPages.Add(serializedPage);
            else
                Log($"  -> Skipped (predicate excluded or null)");
        }

        Log($"Serialized pages: {serializedPages.Count}");
        var serializedArea = _mapper.MapArea(area, serializedPages);
        _store.WriteTree(serializedArea, _configuration.OutputDirectory);
        return serializedArea;
    }

    private SerializedPage? SerializePage(Page page, PredicateDefinition predicate, string contentPath)
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

            var columns = _mapper.BuildColumns(rowParagraphs);
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
            var serializedChild = SerializePage(child, predicate, childContentPath);
            if (serializedChild != null)
                serializedChildren.Add(serializedChild);
        }

        return _mapper.MapPage(page, serializedGridRows, serializedChildren);
    }
}

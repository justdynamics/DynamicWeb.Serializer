using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Infrastructure;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Serialization;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Serialization;

/// <summary>
/// Multi-language support: language-layer predicate expansion, master-link restore
/// computation, and YAML round-trip of the master-link fields on SerializedPage.
/// </summary>
public class MultiLanguageTests
{
    private static ProviderPredicateDefinition ContentPredicate(
        string name = "shop", int areaId = 1, string path = "/Shop", bool includeLanguageLayers = false) => new()
    {
        Name = name,
        ProviderType = "Content",
        Mode = DeploymentMode.Deploy,
        AreaId = areaId,
        Path = path,
        IncludeLanguageLayers = includeLanguageLayers
    };

    private static SerializedPage Page(Guid guid, Guid? masterGuid = null, string? masterType = null,
        List<SerializedPage>? children = null, List<SerializedGridRow>? gridRows = null) => new()
    {
        PageUniqueId = guid,
        Name = "P",
        MenuText = "P",
        UrlName = "p",
        SortOrder = 1,
        MasterPageGuid = masterGuid,
        MasterType = masterType,
        Children = children ?? new List<SerializedPage>(),
        GridRows = gridRows ?? new List<SerializedGridRow>()
    };

    // -------------------------------------------------------------------------
    // LanguageLayerExpander
    // -------------------------------------------------------------------------

    [Fact]
    public void Expand_FlaggedContentPredicate_AddsOnePredicatePerLanguageArea()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPredicate(includeLanguageLayers: true) };

        var expanded = LanguageLayerExpander.Expand(predicates, _ => new[] { 5, 9 });

        Assert.Equal(3, expanded.Count);
        Assert.Equal(1, expanded[0].AreaId);
        Assert.Equal(5, expanded[1].AreaId);
        Assert.Equal(9, expanded[2].AreaId);
        // Synthetic copies keep the master's path space and never re-expand
        Assert.All(expanded.Skip(1), p =>
        {
            Assert.Equal("/Shop", p.Path);
            Assert.False(p.IncludeLanguageLayers);
            Assert.Equal(DeploymentMode.Deploy, p.Mode);
        });
        Assert.Equal("shop-lang-area-5", expanded[1].Name);
        Assert.Equal("shop-lang-area-9", expanded[2].Name);
    }

    [Fact]
    public void Expand_UnflaggedPredicate_PassesThroughUnchanged()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPredicate() };

        var expanded = LanguageLayerExpander.Expand(predicates, _ => new[] { 5 });

        Assert.Single(expanded);
        Assert.Same(predicates[0], expanded[0]);
    }

    [Fact]
    public void Expand_FlaggedSqlTablePredicate_IsNotExpanded()
    {
        var predicates = new List<ProviderPredicateDefinition>
        {
            new() { Name = "flow", ProviderType = "SqlTable", Table = "EcomOrderFlow", IncludeLanguageLayers = true }
        };

        var expanded = LanguageLayerExpander.Expand(predicates, _ => new[] { 5 });

        Assert.Single(expanded);
    }

    [Fact]
    public void Expand_NoLanguageAreas_LogsAndKeepsMasterOnly()
    {
        var logs = new List<string>();
        var predicates = new List<ProviderPredicateDefinition> { ContentPredicate(includeLanguageLayers: true) };

        var expanded = LanguageLayerExpander.Expand(predicates, _ => Array.Empty<int>(), logs.Add);

        Assert.Single(expanded);
        Assert.Contains(logs, l => l.Contains("no language layers"));
    }

    [Fact]
    public void Expand_PreservesPredicateOrder_MasterBeforeItsLayers()
    {
        var predicates = new List<ProviderPredicateDefinition>
        {
            ContentPredicate("a", areaId: 1, includeLanguageLayers: true),
            ContentPredicate("b", areaId: 2, includeLanguageLayers: true)
        };

        var expanded = LanguageLayerExpander.Expand(predicates, masterId => new[] { masterId * 10 });

        Assert.Equal(new[] { "a", "a-lang-area-10", "b", "b-lang-area-20" },
            expanded.Select(p => p.Name).ToArray());
    }

    // -------------------------------------------------------------------------
    // MasterLinkRestorer — pages
    // -------------------------------------------------------------------------

    [Fact]
    public void ComputePageLinkUpdates_ResolvesMasterViaGuidCache()
    {
        var pageGuid = Guid.NewGuid();
        var masterGuid = Guid.NewGuid();
        var pages = new List<SerializedPage> { Page(pageGuid, masterGuid, "Inherit") };
        var cache = new Dictionary<Guid, int> { [pageGuid] = 100, [masterGuid] = 42 };

        var (updates, unresolved) = MasterLinkRestorer.ComputePageLinkUpdates(pages, cache);

        var update = Assert.Single(updates);
        Assert.Equal(100, update.TargetPageId);
        Assert.Equal(42, update.TargetMasterPageId);
        Assert.Equal("Inherit", update.MasterType);
        Assert.Empty(unresolved);
    }

    [Fact]
    public void ComputePageLinkUpdates_MasterMissingOnTarget_ReportsUnresolved()
    {
        var pageGuid = Guid.NewGuid();
        var masterGuid = Guid.NewGuid();
        var pages = new List<SerializedPage> { Page(pageGuid, masterGuid) };
        var cache = new Dictionary<Guid, int> { [pageGuid] = 100 };

        var (updates, unresolved) = MasterLinkRestorer.ComputePageLinkUpdates(pages, cache);

        Assert.Empty(updates);
        var miss = Assert.Single(unresolved);
        Assert.Equal("page", miss.Kind);
        Assert.Equal(masterGuid, miss.MasterGuid);
    }

    [Fact]
    public void ComputePageLinkUpdates_WalksChildrenRecursively()
    {
        var childGuid = Guid.NewGuid();
        var masterGuid = Guid.NewGuid();
        var pages = new List<SerializedPage>
        {
            Page(Guid.NewGuid(), children: new List<SerializedPage> { Page(childGuid, masterGuid) })
        };
        var cache = new Dictionary<Guid, int> { [childGuid] = 7, [masterGuid] = 3 };

        var (updates, _) = MasterLinkRestorer.ComputePageLinkUpdates(pages, cache);

        var update = Assert.Single(updates);
        Assert.Equal(7, update.TargetPageId);
        Assert.Equal(3, update.TargetMasterPageId);
    }

    [Fact]
    public void ComputePageLinkUpdates_PagesWithoutMasterLink_AreSkipped()
    {
        var pages = new List<SerializedPage> { Page(Guid.NewGuid()) };

        var (updates, unresolved) = MasterLinkRestorer.ComputePageLinkUpdates(pages, new Dictionary<Guid, int>());

        Assert.Empty(updates);
        Assert.Empty(unresolved);
    }

    // -------------------------------------------------------------------------
    // MasterLinkRestorer — paragraphs
    // -------------------------------------------------------------------------

    private static SerializedPage PageWithParagraph(Guid pageGuid, SerializedParagraph paragraph) =>
        Page(pageGuid, gridRows: new List<SerializedGridRow>
        {
            new()
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                Columns = new List<SerializedGridColumn>
                {
                    new() { Id = 1, Paragraphs = new List<SerializedParagraph> { paragraph } }
                }
            }
        });

    [Fact]
    public void ComputeParagraphLinkUpdates_ResolvesMasterParagraphAndGlobalRecordPage()
    {
        var paraGuid = Guid.NewGuid();
        var masterParaGuid = Guid.NewGuid();
        var globalPageGuid = Guid.NewGuid();
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = paraGuid,
            SortOrder = 1,
            Fields = new Dictionary<string, object>
            {
                ["MasterParagraphGuid"] = masterParaGuid.ToString(),
                ["GlobalRecordPageGuid"] = globalPageGuid.ToString()
            }
        };
        var pages = new List<SerializedPage> { PageWithParagraph(Guid.NewGuid(), para) };
        var paraCache = new Dictionary<Guid, int> { [paraGuid] = 500, [masterParaGuid] = 400 };
        var pageCache = new Dictionary<Guid, int> { [globalPageGuid] = 77 };

        var (updates, unresolved) = MasterLinkRestorer.ComputeParagraphLinkUpdates(pages, paraCache, pageCache);

        var update = Assert.Single(updates);
        Assert.Equal(500, update.TargetParagraphId);
        Assert.Equal(400, update.TargetMasterParagraphId);
        Assert.Equal(77, update.TargetGlobalRecordPageId);
        Assert.Empty(unresolved);
    }

    [Fact]
    public void ComputeParagraphLinkUpdates_UnresolvableMaster_ReportsKind()
    {
        var paraGuid = Guid.NewGuid();
        var masterParaGuid = Guid.NewGuid();
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = paraGuid,
            SortOrder = 1,
            Fields = new Dictionary<string, object> { ["MasterParagraphGuid"] = masterParaGuid.ToString() }
        };
        var pages = new List<SerializedPage> { PageWithParagraph(Guid.NewGuid(), para) };
        var paraCache = new Dictionary<Guid, int> { [paraGuid] = 500 };

        var (updates, unresolved) = MasterLinkRestorer.ComputeParagraphLinkUpdates(
            pages, paraCache, new Dictionary<Guid, int>());

        Assert.Empty(updates);
        var miss = Assert.Single(unresolved);
        Assert.Equal("paragraph", miss.Kind);
    }

    [Fact]
    public void ComputeParagraphLinkUpdates_NonGuidFieldValue_IsIgnored()
    {
        var paraGuid = Guid.NewGuid();
        var para = new SerializedParagraph
        {
            ParagraphUniqueId = paraGuid,
            SortOrder = 1,
            Fields = new Dictionary<string, object> { ["MasterParagraphGuid"] = "not-a-guid" }
        };
        var pages = new List<SerializedPage> { PageWithParagraph(Guid.NewGuid(), para) };
        var paraCache = new Dictionary<Guid, int> { [paraGuid] = 500 };

        var (updates, unresolved) = MasterLinkRestorer.ComputeParagraphLinkUpdates(
            pages, paraCache, new Dictionary<Guid, int>());

        Assert.Empty(updates);
        Assert.Empty(unresolved);
    }

    // -------------------------------------------------------------------------
    // YAML round-trip of the new SerializedPage fields
    // -------------------------------------------------------------------------

    [Fact]
    public void FileSystemStore_RoundTripsMasterPageGuidAndMasterType()
    {
        var masterGuid = Guid.NewGuid();
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "LangLayer",
            SortOrder = 1,
            Pages = new List<SerializedPage> { Page(Guid.NewGuid(), masterGuid, "Lock") }
        };

        var dir = Path.Combine(Path.GetTempPath(), "SerializerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new FileSystemStore();
            store.WriteTree(area, dir);
            var roundTripped = store.ReadTree(dir, "LangLayer");

            var page = Assert.Single(roundTripped.Pages);
            Assert.Equal(masterGuid, page.MasterPageGuid);
            Assert.Equal("Lock", page.MasterType);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void FileSystemStore_PageWithoutMasterLink_RoundTripsAsNull()
    {
        var area = new SerializedArea
        {
            AreaId = Guid.NewGuid(),
            Name = "Master",
            SortOrder = 1,
            Pages = new List<SerializedPage> { Page(Guid.NewGuid()) }
        };

        var dir = Path.Combine(Path.GetTempPath(), "SerializerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new FileSystemStore();
            store.WriteTree(area, dir);
            var roundTripped = store.ReadTree(dir, "Master");

            var page = Assert.Single(roundTripped.Pages);
            Assert.Null(page.MasterPageGuid);
            Assert.Null(page.MasterType);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}

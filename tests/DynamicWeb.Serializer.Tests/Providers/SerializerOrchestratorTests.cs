using System.Data;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using DynamicWeb.Serializer.Providers.SqlTable;
using DynamicWeb.Serializer.Reporting;
using DynamicWeb.Serializer.Tests.Helpers;
using Dynamicweb.Data;
using Moq;
using Xunit;

// Phase 43 / DESER-01 transitional: many tests still drive the orchestrator via the
// [Obsolete] DeserializeAll(predicates, ...) overload (Phase 44 deletes it via CONVERGE-04).
// File-scoped disable so the remaining predicate-fixture-flavored tests compile clean.
// Layer A SC-1/2/3/6 tests below use the new manifest-driven overload + DeserializeEntries
// test seam directly — they don't need the suppression.
#pragma warning disable CS0618 // Obsolete

namespace DynamicWeb.Serializer.Tests.Providers;

[Trait("Category", "Phase14")]
public class SerializerOrchestratorTests
{
    private readonly Mock<ISerializationProvider> _contentProvider;
    private readonly Mock<ISerializationProvider> _sqlTableProvider;
    private readonly ProviderRegistry _registry;
    private readonly SerializerOrchestrator _orchestrator;

    private static readonly ProviderPredicateDefinition ContentPred1 = new()
    {
        Name = "Pages",
        ProviderType = "Content",
        Path = "/",
        AreaId = 1
    };

    private static readonly ProviderPredicateDefinition ContentPred2 = new()
    {
        Name = "Blog",
        ProviderType = "Content",
        Path = "/blog",
        AreaId = 1
    };

    private static readonly ProviderPredicateDefinition SqlTablePred = new()
    {
        Name = "Order Flows",
        ProviderType = "SqlTable",
        Table = "EcomOrderFlow",
        NameColumn = "OrderFlowName"
    };

    // Phase 43 Layer A entry fixtures (per CONTEXT D-04 — Layer A retargets directly).
    private static readonly ContentEntry ContentEntry1 = new()
    {
        EntryId = "content/area-1",
        Files = new[] { "_content/area-1/page.yml" },
        AreaId = 1,
        AreaName = "Area 1",
        Path = "/",
        PageId = 0
    };

    private static readonly ContentEntry ContentEntry2 = new()
    {
        EntryId = "content/area-1/blog",
        Files = new[] { "_content/area-1/blog/post.yml" },
        AreaId = 1,
        AreaName = "Area 1",
        Path = "/blog",
        PageId = 0
    };

    private static readonly SqlTableEntry SqlTableEntryFx = new()
    {
        EntryId = "sql/EcomOrderFlow",
        Files = new[] { "_sql/EcomOrderFlow/row.yml" },
        Table = "EcomOrderFlow",
        NameColumn = "OrderFlowName"
    };

    public SerializerOrchestratorTests()
    {
        _contentProvider = new Mock<ISerializationProvider>();
        _contentProvider.Setup(p => p.ProviderType).Returns("Content");
        _contentProvider.Setup(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new SerializeResult { RowsSerialized = 5, TableName = "Content" });
        _contentProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 2, Updated = 1, TableName = "Content" });
        // Phase 43 / DESER-03: legacy DeserializeAll(predicates, ...) bridge converts via
        // BuildManifestEntry. Mock returns a canonical Content entry built from the predicate.
        _contentProvider.Setup(p => p.BuildManifestEntry(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns((ProviderPredicateDefinition pred, string _, IReadOnlyList<string> _) => pred.ToManifestEntry());

        _sqlTableProvider = new Mock<ISerializationProvider>();
        _sqlTableProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        _sqlTableProvider.Setup(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new SerializeResult { RowsSerialized = 10, TableName = "EcomOrderFlow" });
        _sqlTableProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 3, Updated = 2, Skipped = 1, TableName = "EcomOrderFlow" });
        _sqlTableProvider.Setup(p => p.BuildManifestEntry(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns((ProviderPredicateDefinition pred, string _, IReadOnlyList<string> _) => pred.ToManifestEntry());

        _registry = new ProviderRegistry();
        _registry.Register(_contentProvider.Object);
        _registry.Register(_sqlTableProvider.Object);

        _orchestrator = new SerializerOrchestrator(_registry);
    }

    /// <summary>
    /// Phase 43 / DESER-03 helper: configure a fresh mock provider with the BuildManifestEntry
    /// bridge setup the legacy DeserializeAll(predicates, ...) overload depends on. Used by
    /// the per-test mock providers below.
    /// </summary>
    private static void StubBuildManifestEntry(Mock<ISerializationProvider> mock)
    {
        mock.Setup(p => p.BuildManifestEntry(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
            .Returns((ProviderPredicateDefinition pred, string _, IReadOnlyList<string> _) => pred.ToManifestEntry());
    }

    // --- SerializeAll tests ---

    [Fact]
    public void SerializeAll_TwoContentPredicates_DispatchesBothToContentProvider()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, ContentPred2 };

        var result = _orchestrator.SerializeAll(predicates, "/output");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        _contentProvider.Verify(p => p.Serialize(ContentPred2, "/output", It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        Assert.Equal(2, result.SerializeResults.Count);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void SerializeAll_MixedPredicates_DispatchesToCorrectProviders()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        Assert.Equal(2, result.SerializeResults.Count);
    }

    [Fact]
    public void SerializeAll_FilterContent_SkipsSqlTablePredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: "Content");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Never);
        Assert.Single(result.SerializeResults);
    }

    [Fact]
    public void SerializeAll_FilterSqlTable_SkipsContentPredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: "SqlTable");

        _contentProvider.Verify(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Never);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        Assert.Single(result.SerializeResults);
    }

    [Fact]
    public void SerializeAll_NullFilter_DispatchesAllPredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: null);

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        Assert.Equal(2, result.SerializeResults.Count);
    }

    [Fact]
    public void SerializeAll_UnknownProviderType_LogsErrorAndContinues()
    {
        var unknownPred = new ProviderPredicateDefinition
        {
            Name = "Unknown",
            ProviderType = "Nonexistent"
        };
        var predicates = new List<ProviderPredicateDefinition> { unknownPred, ContentPred1 };
        var logs = new List<string>();

        var result = _orchestrator.SerializeAll(predicates, "/output", log: msg => logs.Add(msg));

        // Unknown predicate should be skipped with error, Content should still be processed
        Assert.Single(result.SerializeResults);
        Assert.Single(result.Errors);
        Assert.Contains("Nonexistent", result.Errors[0]);
        Assert.Contains("WARNING", logs.First(l => l.Contains("Nonexistent")));
    }

    // Phase 43 / DESER-03: SerializeAll_FailedValidation_SkipsWithErrorLogged removed.
    // ValidatePredicate is no longer on ISerializationProvider — the orchestrator now uses
    // a typed-dispatch helper (ValidateBeforeSerialize) which routes only to concrete
    // ContentProvider / SqlTableProvider. Mock providers don't get validation pre-flight;
    // the test was probing the deprecated Mock-based plumbing.

    // --- DeserializeAll tests ---

    [Fact]
    public void DeserializeAll_MixedPredicates_DispatchesToCorrectProviders()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

#pragma warning disable CS0618 // [Obsolete] predicate-typed overload — Phase 44 deletes
        var result = _orchestrator.DeserializeAll(predicates, "/input");

        // Phase 43: DeserializeAll(predicates, ...) bridges via BuildManifestEntry, so the
        // dispatched ManifestEntry is a synthetic ContentEntry/SqlTableEntry, not the predicate.
        // Verify against the entry's discriminator field instead.
        _contentProvider.Verify(p => p.Deserialize(It.Is<ManifestEntry>(e => e.ProviderType == "Content"), "/input", It.IsAny<Action<string>?>(), false, It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Deserialize(It.Is<ManifestEntry>(e => e.ProviderType == "SqlTable"), "/input", It.IsAny<Action<string>?>(), false, It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        Assert.Equal(2, result.DeserializeResults.Count);
    }

    [Fact]
    public void DeserializeAll_FilterAndDryRun_PassesThroughCorrectly()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.DeserializeAll(predicates, "/input", isDryRun: true, providerFilter: "SqlTable");

        _contentProvider.Verify(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Never);
        _sqlTableProvider.Verify(p => p.Deserialize(It.Is<ManifestEntry>(e => e.ProviderType == "SqlTable"), "/input", It.IsAny<Action<string>?>(), true, It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()), Times.Once);
        Assert.Single(result.DeserializeResults);
    }

    [Fact]
    public void DeserializeAll_UnknownProviderType_LogsErrorAndContinues()
    {
        var unknownPred = new ProviderPredicateDefinition
        {
            Name = "Unknown",
            ProviderType = "Nonexistent"
        };
        var predicates = new List<ProviderPredicateDefinition> { unknownPred, SqlTablePred };

        var result = _orchestrator.DeserializeAll(predicates, "/input");

        Assert.Single(result.DeserializeResults);
        Assert.Single(result.Errors);
        Assert.Contains("Nonexistent", result.Errors[0]);
    }

    // Phase 43 / DESER-03: DeserializeAll_FailedValidation_SkipsWithErrorLogged removed.
    // Same reason as SerializeAll_FailedValidation_* above — the deprecated ValidatePredicate
    // mock setup never reached the production code; validation moves to manifest read time.

    // --- OrchestratorResult tests ---

    [Fact]
    public void OrchestratorResult_HasErrors_TrueWhenErrorsExist()
    {
        var result = new OrchestratorResult { Errors = new List<string> { "fail" } };
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void OrchestratorResult_HasErrors_TrueWhenSerializeResultHasErrors()
    {
        var result = new OrchestratorResult
        {
            SerializeResults = new List<SerializeResult>
            {
                new() { Errors = new[] { "serialize error" } }
            }
        };
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void OrchestratorResult_HasErrors_FalseWhenNoErrors()
    {
        var result = new OrchestratorResult
        {
            SerializeResults = new List<SerializeResult> { new() { RowsSerialized = 5 } }
        };
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void OrchestratorResult_Summary_AggregatesCounts()
    {
        var result = new OrchestratorResult
        {
            SerializeResults = new List<SerializeResult>
            {
                new() { RowsSerialized = 5, TableName = "Content" },
                new() { RowsSerialized = 10, TableName = "EcomOrderFlow" }
            }
        };

        Assert.Contains("15", result.Summary);
    }

    // === NEW Phase 15 Tests: FK Ordering and Cache Invalidation ===

    /// <summary>
    /// Helper: set up ISqlExecutor mock to return FK edges for FkDependencyResolver.
    /// </summary>
    private static FkDependencyResolver CreateFkResolver(params (string Child, string Parent)[] edges)
    {
        var dataTable = new DataTable();
        dataTable.Columns.Add("ChildTable", typeof(string));
        dataTable.Columns.Add("ParentTable", typeof(string));
        foreach (var (child, parent) in edges)
            dataTable.Rows.Add(child, parent);

        var mockExecutor = new Mock<ISqlExecutor>();
        mockExecutor.Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns(() => dataTable.CreateDataReader());

        return new FkDependencyResolver(mockExecutor.Object);
    }

    [Fact]
    [Trait("Category", "Phase15")]
    public void DeserializeAll_FkOrdering_SqlTablePredicatesReorderedByDependency()
    {
        // A depends on B, B depends on C => deserialization order: C, B, A
        var predC = new ProviderPredicateDefinition { Name = "C", ProviderType = "SqlTable", Table = "C" };
        var predB = new ProviderPredicateDefinition { Name = "B", ProviderType = "SqlTable", Table = "B" };
        var predA = new ProviderPredicateDefinition { Name = "A", ProviderType = "SqlTable", Table = "A" };

        var fkResolver = CreateFkResolver(("A", "B"), ("B", "C"));

        var callOrder = new List<string>();
        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ManifestEntry e, string _, Action<string>? _, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                callOrder.Add(((SqlTableEntry)e).Table);
                return new ProviderDeserializeResult { Created = 1, TableName = ((SqlTableEntry)e).Table };
            });

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        // Pass predicates in wrong order: A, B, C (should be reordered to C, B, A)
        var orchestrator = new SerializerOrchestrator(registry, fkResolver);
        orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { predA, predB, predC }, "/input");

        Assert.Equal(3, callOrder.Count);
        Assert.True(callOrder.IndexOf("C") < callOrder.IndexOf("B"),
            $"Expected C before B, got: {string.Join(", ", callOrder)}");
        Assert.True(callOrder.IndexOf("B") < callOrder.IndexOf("A"),
            $"Expected B before A, got: {string.Join(", ", callOrder)}");
    }

    [Fact]
    [Trait("Category", "Phase15")]
    public void DeserializeAll_FkOrdering_ContentPredicatesUnaffected()
    {
        // Mixed predicates: Content + SqlTable. Content stays at front, SqlTable reordered.
        var contentPred = new ProviderPredicateDefinition { Name = "Pages", ProviderType = "Content", Path = "/", AreaId = 1 };
        var sqlPredB = new ProviderPredicateDefinition { Name = "B", ProviderType = "SqlTable", Table = "B" };
        var sqlPredA = new ProviderPredicateDefinition { Name = "A", ProviderType = "SqlTable", Table = "A" };

        var fkResolver = CreateFkResolver(("A", "B")); // A depends on B => B before A

        var callOrder = new List<string>();

        var contentProvider = new Mock<ISerializationProvider>();
        contentProvider.Setup(p => p.ProviderType).Returns("Content");
        StubBuildManifestEntry(contentProvider);
        contentProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ManifestEntry e, string _, Action<string>? _, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                callOrder.Add($"Content:{e.EntryId}");
                return new ProviderDeserializeResult { Created = 1, TableName = "Content" };
            });

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ManifestEntry e, string _, Action<string>? _, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                callOrder.Add($"SqlTable:{((SqlTableEntry)e).Table}");
                return new ProviderDeserializeResult { Created = 1, TableName = ((SqlTableEntry)e).Table };
            });

        var registry = new ProviderRegistry();
        registry.Register(contentProvider.Object);
        registry.Register(sqlProvider.Object);

        // Order: sqlPredA, contentPred, sqlPredB — SqlTable in FK order (B, A) first, then Content
        var orchestrator = new SerializerOrchestrator(registry, fkResolver);
        orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { sqlPredA, contentPred, sqlPredB }, "/input");

        Assert.Equal(3, callOrder.Count);
        // SqlTable B before SqlTable A (B is parent), then Content last.
        // Phase 43: Content entry id is "content/area-{AreaId}" per ToManifestEntry helper —
        // the assertion adapts to the entry-driven dispatch path.
        Assert.Equal("SqlTable:B", callOrder[0]);
        Assert.Equal("SqlTable:A", callOrder[1]);
        Assert.Equal("Content:content/area-1", callOrder[2]);
    }

    [Fact]
    [Trait("Category", "Phase15")]
    public void DeserializeAll_CacheInvalidation_CalledAfterEachSuccessfulDeserialize()
    {
        var pred1 = new ProviderPredicateDefinition
        {
            Name = "Payments",
            ProviderType = "SqlTable",
            Table = "EcomPayments",
            ServiceCaches = new List<string> { "CacheA", "CacheB" }
        };
        var pred2 = new ProviderPredicateDefinition
        {
            Name = "Shippings",
            ProviderType = "SqlTable",
            Table = "EcomShippings",
            ServiceCaches = new List<string> { "CacheC" }
        };

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 1, TableName = "Test" });

        // Phase 37-04: CacheInvalidator resolves via DwCacheServiceRegistry-shaped
        // entries. Use fake typed entries so we can count Invoke() calls without
        // triggering real DW ClearCache() side-effects on the typed service singletons.
        var invokeCount = 0;
        DwCacheServiceRegistry.CacheClearEntry MakeFake(string n) =>
            new(n, $"Test.{n}", () => invokeCount++);

        var cacheInvalidator = new CacheInvalidator(name => MakeFake(name));

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        var orchestrator = new SerializerOrchestrator(registry, cacheInvalidator: cacheInvalidator);
        orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { pred1, pred2 }, "/input");

        // CacheA, CacheB from pred1, CacheC from pred2 = 3 cache clears
        Assert.Equal(3, invokeCount);
    }

    [Fact]
    [Trait("Category", "Phase15")]
    public void DeserializeAll_DryRun_DoesNotCallCacheInvalidator()
    {
        var pred = new ProviderPredicateDefinition
        {
            Name = "Payments",
            ProviderType = "SqlTable",
            Table = "EcomPayments",
            ServiceCaches = new List<string> { "CacheA" }
        };

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 1, TableName = "EcomPayments" });

        var invokeCount = 0;
        var cacheInvalidator = new CacheInvalidator(name =>
            new DwCacheServiceRegistry.CacheClearEntry(name, $"Test.{name}", () => invokeCount++));

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        var orchestrator = new SerializerOrchestrator(registry, cacheInvalidator: cacheInvalidator);
        orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { pred }, "/input", isDryRun: true);

        // No cache invalidation during dry-run
        Assert.Equal(0, invokeCount);
    }

    [Fact]
    [Trait("Category", "Phase15")]
    public void SerializeAll_DoesNotReorderPredicates()
    {
        // FK ordering only applies to DeserializeAll, not SerializeAll
        var predA = new ProviderPredicateDefinition { Name = "A", ProviderType = "SqlTable", Table = "A" };
        var predB = new ProviderPredicateDefinition { Name = "B", ProviderType = "SqlTable", Table = "B" };

        var fkResolver = CreateFkResolver(("A", "B")); // A depends on B

        var callOrder = new List<string>();
        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ProviderPredicateDefinition pred, string _, Action<string>? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                callOrder.Add(pred.Table!);
                return new SerializeResult { RowsSerialized = 1, TableName = pred.Table! };
            });

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        var orchestrator = new SerializerOrchestrator(registry, fkResolver);
        orchestrator.SerializeAll(new List<ProviderPredicateDefinition> { predA, predB }, "/output");

        // Original order preserved: A, B (not reordered to B, A)
        Assert.Equal(new[] { "A", "B" }, callOrder);
    }

    [Fact]
    [Trait("Category", "Phase15")]
    public void DeserializeAll_EmptyServiceCaches_SucceedsWithoutCacheCall()
    {
        var pred = new ProviderPredicateDefinition
        {
            Name = "OrderFlows",
            ProviderType = "SqlTable",
            Table = "EcomOrderFlow",
            ServiceCaches = new List<string>() // empty
        };

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 1, TableName = "EcomOrderFlow" });

        var resolverCalls = 0;
        var cacheInvalidator = new CacheInvalidator(_ =>
        {
            resolverCalls++;
            return null;
        });

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        var orchestrator = new SerializerOrchestrator(registry, cacheInvalidator: cacheInvalidator);
        var result = orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { pred }, "/input");

        Assert.Single(result.DeserializeResults);
        Assert.False(result.HasErrors);
        // Empty ServiceCaches → orchestrator short-circuits before calling the resolver.
        Assert.Equal(0, resolverCalls);
    }

    // === Phase 25 Tests: Schema Sync ===

    [Fact]
    [Trait("Category", "Phase25")]
    public void DeserializeAll_CallsSchemaSyncAfterPredicateWithSchemaSyncConfig()
    {
        var pred = new ProviderPredicateDefinition
        {
            Name = "EcomProductGroupField",
            ProviderType = "SqlTable",
            Table = "EcomProductGroupField",
            SchemaSync = "EcomGroupFields"
        };

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 3, TableName = "EcomProductGroupField" });

        var mockSchemaSync = new Mock<EcomGroupFieldSchemaSync>(MockBehavior.Loose, new object[] { new Mock<ISqlExecutor>().Object });
        mockSchemaSync.Setup(s => s.SyncSchema(It.IsAny<Action<string>?>()));

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        var orchestrator = new SerializerOrchestrator(registry, ecomSchemaSync: mockSchemaSync.Object);
        orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { pred }, "/input");

        mockSchemaSync.Verify(s => s.SyncSchema(It.IsAny<Action<string>?>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Phase25")]
    public void DeserializeAll_DryRun_DoesNotCallSchemaSync()
    {
        var pred = new ProviderPredicateDefinition
        {
            Name = "EcomProductGroupField",
            ProviderType = "SqlTable",
            Table = "EcomProductGroupField",
            SchemaSync = "EcomGroupFields"
        };

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 3, TableName = "EcomProductGroupField" });

        var mockSchemaSync = new Mock<EcomGroupFieldSchemaSync>(MockBehavior.Loose, new object[] { new Mock<ISqlExecutor>().Object });

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        var orchestrator = new SerializerOrchestrator(registry, ecomSchemaSync: mockSchemaSync.Object);
        orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { pred }, "/input", isDryRun: true);

        mockSchemaSync.Verify(s => s.SyncSchema(It.IsAny<Action<string>?>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Phase25")]
    public void DeserializeAll_NoSchemaSyncProperty_DoesNotCallSchemaSync()
    {
        var pred = new ProviderPredicateDefinition
        {
            Name = "EcomOrderFlow",
            ProviderType = "SqlTable",
            Table = "EcomOrderFlow"
            // No SchemaSync property
        };

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 1, TableName = "EcomOrderFlow" });

        var mockSchemaSync = new Mock<EcomGroupFieldSchemaSync>(MockBehavior.Loose, new object[] { new Mock<ISqlExecutor>().Object });

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        var orchestrator = new SerializerOrchestrator(registry, ecomSchemaSync: mockSchemaSync.Object);
        orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { pred }, "/input");

        mockSchemaSync.Verify(s => s.SyncSchema(It.IsAny<Action<string>?>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Phase15")]
    public void DeserializeAll_CacheInvalidationFailure_LoggedButDoesNotBlockOtherPredicates()
    {
        var pred1 = new ProviderPredicateDefinition
        {
            Name = "Payments",
            ProviderType = "SqlTable",
            Table = "EcomPayments",
            ServiceCaches = new List<string> { "BadCache" }
        };
        var pred2 = new ProviderPredicateDefinition
        {
            Name = "Shippings",
            ProviderType = "SqlTable",
            Table = "EcomShippings",
            ServiceCaches = new List<string> { "GoodCache" }
        };

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>(), It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns(new ProviderDeserializeResult { Created = 1, TableName = "Test" });

        // Phase 37-04: CacheInvalidator that throws on "BadCache" (resolver returns null → throw)
        // but resolves "GoodCache" to a test entry whose Invoke tracks that it ran.
        var goodInvoked = 0;
        var cacheInvalidator = new CacheInvalidator(name =>
            name.Equals("GoodCache", StringComparison.OrdinalIgnoreCase)
                ? new DwCacheServiceRegistry.CacheClearEntry("GoodCache", "Test.GoodCache", () => goodInvoked++)
                : null);

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);

        var logs = new List<string>();
        var orchestrator = new SerializerOrchestrator(registry, cacheInvalidator: cacheInvalidator);
        var result = orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { pred1, pred2 }, "/input", log: msg => logs.Add(msg));

        // Both predicates should have been processed
        Assert.Equal(2, result.DeserializeResults.Count);
        // Cache failure was logged
        Assert.Contains(logs, l => l.Contains("WARNING") && l.Contains("Cache invalidation failed"));
        // Good cache was still cleared
        Assert.Equal(1, goodInvoked);
    }

    // =========================================================================
    // Phase 43 Layer A acceptance tests — SC-1, SC-2, SC-3, SC-6
    // These exercise the new manifest-driven DeserializeAll path via the
    // internal DeserializeEntries test seam (avoids on-disk manifest setup).
    // =========================================================================

    /// <summary>
    /// SC-1: orchestrator dispatches every entry from a manifest, populating EntryOutcomes
    /// with one outcome per dispatched entry.
    /// </summary>
    [Fact]
    [Trait("Category", "Phase43")]
    public void DeserializeAll_ManifestDriven_DispatchesEachEntry_SC1()
    {
        var entries = new List<ManifestEntry> { ContentEntry1, SqlTableEntryFx };

        var result = _orchestrator.DeserializeEntries(
            entries,
            modeRoot: "/tmp/modeRoot",
            mode: DeploymentMode.Deploy,
            strategy: ConflictStrategy.SourceWins,
            log: null,
            isDryRun: false,
            providerFilter: null,
            escalator: null,
            excludeFieldsByItemType: null,
            excludeXmlElementsByType: null);

        Assert.Equal(2, result.EntryOutcomes.Count);
        Assert.Contains(result.EntryOutcomes, o => o.EntryId == "content/area-1" && o.Status == EntryStatus.Succeeded);
        Assert.Contains(result.EntryOutcomes, o => o.EntryId == "sql/EcomOrderFlow" && o.Status == EntryStatus.Succeeded);
        Assert.False(result.HasErrors);
    }

    /// <summary>
    /// SC-2: providerFilter exclusion produces an EntryStatus.Skipped outcome (today's
    /// silent-skip class becomes observable per REPORT-01 / D-02).
    /// </summary>
    [Fact]
    [Trait("Category", "Phase43")]
    public void DeserializeAll_ProviderFilterExclusion_ReportsSkipped_SC2()
    {
        var entries = new List<ManifestEntry> { ContentEntry1, SqlTableEntryFx };

        var result = _orchestrator.DeserializeEntries(
            entries,
            modeRoot: "/tmp/modeRoot",
            mode: DeploymentMode.Deploy,
            strategy: ConflictStrategy.SourceWins,
            log: null,
            isDryRun: false,
            providerFilter: "Content",
            escalator: null,
            excludeFieldsByItemType: null,
            excludeXmlElementsByType: null);

        Assert.Contains(result.EntryOutcomes, o => o.EntryId == "sql/EcomOrderFlow" && o.Status == EntryStatus.Skipped);
        Assert.Contains(result.EntryOutcomes, o => o.EntryId == "content/area-1" && o.Status == EntryStatus.Succeeded);
        // Skipped is not a failure → HasErrors stays false.
        Assert.False(result.HasErrors);
    }

    /// <summary>
    /// SC-3a: HasErrors is true when at least one EntryOutcome has Status == Failed.
    /// Direct test of the aggregation invariant in OrchestratorResult.
    /// </summary>
    [Fact]
    [Trait("Category", "Phase43")]
    public void OrchestratorResult_HasErrors_TrueWhenAnyEntryFailed_SC3()
    {
        var result = new OrchestratorResult
        {
            EntryOutcomes = new List<EntryOutcome>
            {
                EntryOutcome.Skipped(ContentEntry1, "filtered"),
                EntryOutcome.Failed(SqlTableEntryFx, "FK violation")
            }
        };
        Assert.True(result.HasErrors);
    }

    /// <summary>
    /// SC-3b: HasErrors is false when no entry outcome failed, even with mixed Skipped
    /// + Succeeded outcomes.
    /// </summary>
    [Fact]
    [Trait("Category", "Phase43")]
    public void OrchestratorResult_HasErrors_FalseWhenAllSucceededOrSkipped_SC3()
    {
        var result = new OrchestratorResult
        {
            EntryOutcomes = new List<EntryOutcome>
            {
                EntryOutcome.Skipped(ContentEntry1, "filtered"),
                EntryOutcome.From(SqlTableEntryFx,
                    new ProviderDeserializeResult { Created = 5, TableName = "EcomOrderFlow" },
                    TimeSpan.FromMilliseconds(50))
            }
        };
        Assert.False(result.HasErrors);
    }

    /// <summary>
    /// SC-6: FK ordering operates on entries[] live-recomputed regardless of input order.
    /// Two different input orderings produce identical dispatch order.
    /// </summary>
    [Fact]
    [Trait("Category", "Phase43")]
    public void DeserializeAll_ShuffledManifestEntries_ProducesSameDispatchOrder_SC6()
    {
        // 3 SqlTableEntries A, B, C where FK requires C before B before A.
        var entryA = new SqlTableEntry { EntryId = "sql/A", Files = Array.Empty<string>(), Table = "A" };
        var entryB = new SqlTableEntry { EntryId = "sql/B", Files = Array.Empty<string>(), Table = "B" };
        var entryC = new SqlTableEntry { EntryId = "sql/C", Files = Array.Empty<string>(), Table = "C" };

        var fkResolver = CreateFkResolver(("A", "B"), ("B", "C"));

        var dispatchedOrder = new List<string>();
        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        StubBuildManifestEntry(sqlProvider);
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ManifestEntry>(),
                It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(),
                It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>?>(),
                It.IsAny<IReadOnlyDictionary<string, List<string>>?>()))
            .Returns((ManifestEntry e, string _, Action<string>? _, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _, IReadOnlyDictionary<string, List<string>>? _, IReadOnlyDictionary<string, List<string>>? _) =>
            {
                dispatchedOrder.Add(((SqlTableEntry)e).Table);
                return new ProviderDeserializeResult { Created = 1, TableName = ((SqlTableEntry)e).Table };
            });

        var registry = new ProviderRegistry();
        registry.Register(sqlProvider.Object);
        var orchestrator = new SerializerOrchestrator(registry, fkResolver);

        // Run with ABC ordering.
        var unshuffled = new List<ManifestEntry> { entryA, entryB, entryC };
        orchestrator.DeserializeEntries(unshuffled, "/tmp", DeploymentMode.Deploy, ConflictStrategy.SourceWins,
            log: null, isDryRun: false, providerFilter: null, escalator: null,
            excludeFieldsByItemType: null, excludeXmlElementsByType: null);
        var orderUnshuffled = dispatchedOrder.ToList();
        dispatchedOrder.Clear();

        // Run with shuffled ordering — FK reorder must produce the same dispatch sequence.
        var shuffled = new List<ManifestEntry> { entryC, entryA, entryB };
        orchestrator.DeserializeEntries(shuffled, "/tmp", DeploymentMode.Deploy, ConflictStrategy.SourceWins,
            log: null, isDryRun: false, providerFilter: null, escalator: null,
            excludeFieldsByItemType: null, excludeXmlElementsByType: null);
        var orderShuffled = dispatchedOrder.ToList();

        Assert.Equal(orderUnshuffled, orderShuffled);
        Assert.Equal(new[] { "C", "B", "A" }, orderUnshuffled);
    }
}

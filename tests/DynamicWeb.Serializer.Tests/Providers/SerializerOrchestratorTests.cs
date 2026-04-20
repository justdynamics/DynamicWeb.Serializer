using System.Data;
using DynamicWeb.Serializer.Configuration;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Models;
using DynamicWeb.Serializer.Providers;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

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

    public SerializerOrchestratorTests()
    {
        _contentProvider = new Mock<ISerializationProvider>();
        _contentProvider.Setup(p => p.ProviderType).Returns("Content");
        _contentProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        _contentProvider.Setup(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()))
            .Returns(new SerializeResult { RowsSerialized = 5, TableName = "Content" });
        _contentProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
            .Returns(new ProviderDeserializeResult { Created = 2, Updated = 1, TableName = "Content" });

        _sqlTableProvider = new Mock<ISerializationProvider>();
        _sqlTableProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        _sqlTableProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        _sqlTableProvider.Setup(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()))
            .Returns(new SerializeResult { RowsSerialized = 10, TableName = "EcomOrderFlow" });
        _sqlTableProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
            .Returns(new ProviderDeserializeResult { Created = 3, Updated = 2, Skipped = 1, TableName = "EcomOrderFlow" });

        _registry = new ProviderRegistry();
        _registry.Register(_contentProvider.Object);
        _registry.Register(_sqlTableProvider.Object);

        _orchestrator = new SerializerOrchestrator(_registry);
    }

    // --- SerializeAll tests ---

    [Fact]
    public void SerializeAll_TwoContentPredicates_DispatchesBothToContentProvider()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, ContentPred2 };

        var result = _orchestrator.SerializeAll(predicates, "/output");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>()), Times.Once);
        _contentProvider.Verify(p => p.Serialize(ContentPred2, "/output", It.IsAny<Action<string>?>()), Times.Once);
        Assert.Equal(2, result.SerializeResults.Count);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void SerializeAll_MixedPredicates_DispatchesToCorrectProviders()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>()), Times.Once);
        Assert.Equal(2, result.SerializeResults.Count);
    }

    [Fact]
    public void SerializeAll_FilterContent_SkipsSqlTablePredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: "Content");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()), Times.Never);
        Assert.Single(result.SerializeResults);
    }

    [Fact]
    public void SerializeAll_FilterSqlTable_SkipsContentPredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: "SqlTable");

        _contentProvider.Verify(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()), Times.Never);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>()), Times.Once);
        Assert.Single(result.SerializeResults);
    }

    [Fact]
    public void SerializeAll_NullFilter_DispatchesAllPredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: null);

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>()), Times.Once);
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

    [Fact]
    public void SerializeAll_FailedValidation_SkipsWithErrorLogged()
    {
        var invalidPred = new ProviderPredicateDefinition
        {
            Name = "BadPred",
            ProviderType = "Content",
            Path = "",
            AreaId = 0
        };
        _contentProvider.Setup(p => p.ValidatePredicate(invalidPred))
            .Returns(ValidationResult.Failure("Path is required"));

        var predicates = new List<ProviderPredicateDefinition> { invalidPred, SqlTablePred };
        var logs = new List<string>();

        var result = _orchestrator.SerializeAll(predicates, "/output", log: msg => logs.Add(msg));

        // Invalid predicate should be skipped, SqlTable should proceed
        Assert.Single(result.SerializeResults);
        Assert.Single(result.Errors);
        Assert.Contains("Path is required", result.Errors[0]);
    }

    // --- DeserializeAll tests ---

    [Fact]
    public void DeserializeAll_MixedPredicates_DispatchesToCorrectProviders()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.DeserializeAll(predicates, "/input");

        _contentProvider.Verify(p => p.Deserialize(ContentPred1, "/input", It.IsAny<Action<string>?>(), false, It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Deserialize(SqlTablePred, "/input", It.IsAny<Action<string>?>(), false, It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()), Times.Once);
        Assert.Equal(2, result.DeserializeResults.Count);
    }

    [Fact]
    public void DeserializeAll_FilterAndDryRun_PassesThroughCorrectly()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.DeserializeAll(predicates, "/input", isDryRun: true, providerFilter: "SqlTable");

        _contentProvider.Verify(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()), Times.Never);
        _sqlTableProvider.Verify(p => p.Deserialize(SqlTablePred, "/input", It.IsAny<Action<string>?>(), true, It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()), Times.Once);
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

    [Fact]
    public void DeserializeAll_FailedValidation_SkipsWithErrorLogged()
    {
        var invalidPred = new ProviderPredicateDefinition
        {
            Name = "BadPred",
            ProviderType = "SqlTable",
            Table = ""
        };
        _sqlTableProvider.Setup(p => p.ValidatePredicate(invalidPred))
            .Returns(ValidationResult.Failure("Table is required"));

        var predicates = new List<ProviderPredicateDefinition> { invalidPred, ContentPred1 };

        var result = _orchestrator.DeserializeAll(predicates, "/input");

        Assert.Single(result.DeserializeResults);
        Assert.Single(result.Errors);
        Assert.Contains("Table is required", result.Errors[0]);
    }

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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
            .Returns((ProviderPredicateDefinition pred, string _, Action<string>? _, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _) =>
            {
                callOrder.Add(pred.Table!);
                return new ProviderDeserializeResult { Created = 1, TableName = pred.Table! };
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
        contentProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        contentProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
            .Returns((ProviderPredicateDefinition pred, string _, Action<string>? _, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _) =>
            {
                callOrder.Add($"Content:{pred.Name}");
                return new ProviderDeserializeResult { Created = 1, TableName = "Content" };
            });

        var sqlProvider = new Mock<ISerializationProvider>();
        sqlProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
            .Returns((ProviderPredicateDefinition pred, string _, Action<string>? _, bool _, ConflictStrategy _, DynamicWeb.Serializer.Serialization.InternalLinkResolver? _) =>
            {
                callOrder.Add($"SqlTable:{pred.Table}");
                return new ProviderDeserializeResult { Created = 1, TableName = pred.Table! };
            });

        var registry = new ProviderRegistry();
        registry.Register(contentProvider.Object);
        registry.Register(sqlProvider.Object);

        // Order: sqlPredA, contentPred, sqlPredB — SqlTable in FK order (B, A) first, then Content
        var orchestrator = new SerializerOrchestrator(registry, fkResolver);
        orchestrator.DeserializeAll(new List<ProviderPredicateDefinition> { sqlPredA, contentPred, sqlPredB }, "/input");

        Assert.Equal(3, callOrder.Count);
        // SqlTable B before SqlTable A (B is parent), then Content last
        Assert.Equal("SqlTable:B", callOrder[0]);
        Assert.Equal("SqlTable:A", callOrder[1]);
        Assert.Equal("Content:Pages", callOrder[2]);
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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()))
            .Returns((ProviderPredicateDefinition pred, string _, Action<string>? _) =>
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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
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
        sqlProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        sqlProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>(), It.IsAny<ConflictStrategy>(), It.IsAny<DynamicWeb.Serializer.Serialization.InternalLinkResolver?>()))
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
}

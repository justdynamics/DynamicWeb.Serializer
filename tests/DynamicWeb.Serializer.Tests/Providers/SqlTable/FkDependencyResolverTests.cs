using System.Data;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;
using Moq;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Providers.SqlTable;

[Trait("Category", "Phase15")]
public class FkDependencyResolverTests
{
    /// <summary>
    /// Helper: set up ISqlExecutor mock to return a reader with the given FK edges.
    /// Each edge is (ChildTable, ParentTable).
    /// </summary>
    private static Mock<ISqlExecutor> SetupExecutor(params (string Child, string Parent)[] edges)
    {
        var mockExecutor = new Mock<ISqlExecutor>();
        var dataTable = new DataTable();
        dataTable.Columns.Add("ChildTable", typeof(string));
        dataTable.Columns.Add("ParentTable", typeof(string));

        foreach (var (child, parent) in edges)
        {
            dataTable.Rows.Add(child, parent);
        }

        mockExecutor
            .Setup(x => x.ExecuteReader(It.IsAny<CommandBuilder>()))
            .Returns(() => dataTable.CreateDataReader());

        return mockExecutor;
    }

    [Fact]
    public void GetDeserializationOrder_NoFKs_ReturnsAllTables()
    {
        var mockExecutor = SetupExecutor(); // no edges
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var result = resolver.GetDeserializationOrder(new[] { "TableA", "TableB", "TableC" });

        Assert.Equal(3, result.Count);
        Assert.Contains("TableA", result);
        Assert.Contains("TableB", result);
        Assert.Contains("TableC", result);
    }

    [Fact]
    public void GetDeserializationOrder_SingleFK_ParentBeforeChild()
    {
        // A references B (A is child, B is parent) => B before A
        var mockExecutor = SetupExecutor(("A", "B"));
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var result = resolver.GetDeserializationOrder(new[] { "A", "B" });

        Assert.Equal(2, result.Count);
        Assert.True(result.IndexOf("B") < result.IndexOf("A"),
            $"Expected B before A, got: {string.Join(", ", result)}");
    }

    [Fact]
    public void GetDeserializationOrder_Chain_ReturnsCorrectOrder()
    {
        // A->B->C => C, B, A
        var mockExecutor = SetupExecutor(("A", "B"), ("B", "C"));
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var result = resolver.GetDeserializationOrder(new[] { "A", "B", "C" });

        Assert.Equal(3, result.Count);
        Assert.True(result.IndexOf("C") < result.IndexOf("B"),
            $"Expected C before B, got: {string.Join(", ", result)}");
        Assert.True(result.IndexOf("B") < result.IndexOf("A"),
            $"Expected B before A, got: {string.Join(", ", result)}");
    }

    [Fact]
    public void GetDeserializationOrder_Diamond_RespectsAllDependencies()
    {
        // A->B, A->C, B->D, C->D => D before B and C, B and C before A
        var mockExecutor = SetupExecutor(("A", "B"), ("A", "C"), ("B", "D"), ("C", "D"));
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var result = resolver.GetDeserializationOrder(new[] { "A", "B", "C", "D" });

        Assert.Equal(4, result.Count);
        Assert.True(result.IndexOf("D") < result.IndexOf("B"),
            $"Expected D before B, got: {string.Join(", ", result)}");
        Assert.True(result.IndexOf("D") < result.IndexOf("C"),
            $"Expected D before C, got: {string.Join(", ", result)}");
        Assert.True(result.IndexOf("B") < result.IndexOf("A"),
            $"Expected B before A, got: {string.Join(", ", result)}");
        Assert.True(result.IndexOf("C") < result.IndexOf("A"),
            $"Expected C before A, got: {string.Join(", ", result)}");
    }

    [Fact]
    public void GetDeserializationOrder_SelfReferencingFK_SkippedNoCycleError()
    {
        // Self-ref: A->A should be skipped. Also A->B normal edge.
        var mockExecutor = SetupExecutor(("A", "A"), ("A", "B"));
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var result = resolver.GetDeserializationOrder(new[] { "A", "B" });

        Assert.Equal(2, result.Count);
        Assert.True(result.IndexOf("B") < result.IndexOf("A"),
            $"Expected B before A, got: {string.Join(", ", result)}");
    }

    [Fact]
    public void GetDeserializationOrder_ExternalFK_FilteredOut()
    {
        // A->B, but B is NOT in the predicate set => edge filtered, A stands alone
        var mockExecutor = SetupExecutor(("A", "B"));
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var result = resolver.GetDeserializationOrder(new[] { "A" });

        Assert.Single(result);
        Assert.Equal("A", result[0]);
    }

    [Fact]
    public void GetDeserializationOrder_CircularDependency_ThrowsInvalidOperationException()
    {
        // A->B, B->A => cycle
        var mockExecutor = SetupExecutor(("A", "B"), ("B", "A"));
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var ex = Assert.Throws<InvalidOperationException>(
            () => { resolver.GetDeserializationOrder(new[] { "A", "B" }); });

        Assert.Contains("Circular FK dependency detected", ex.Message);
        Assert.Contains("A", ex.Message);
        Assert.Contains("B", ex.Message);
    }

    [Fact]
    public void GetDeserializationOrder_EmptyTableList_ReturnsEmptyList()
    {
        var mockExecutor = SetupExecutor();
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var result = resolver.GetDeserializationOrder(Array.Empty<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void GetDeserializationOrder_CaseInsensitive_MatchesTables()
    {
        // FK edge uses different casing than table names in predicate set
        var mockExecutor = SetupExecutor(("tablea", "TABLEB"));
        var resolver = new FkDependencyResolver(mockExecutor.Object);

        var result = resolver.GetDeserializationOrder(new[] { "TableA", "TableB" });

        Assert.Equal(2, result.Count);
        Assert.True(result.IndexOf("TableB") < result.IndexOf("TableA"),
            $"Expected TableB before TableA, got: {string.Join(", ", result)}");
    }
}

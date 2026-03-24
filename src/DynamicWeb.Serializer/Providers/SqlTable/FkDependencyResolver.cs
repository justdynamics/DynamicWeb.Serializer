using Dynamicweb.Data;

namespace DynamicWeb.Serializer.Providers.SqlTable;

/// <summary>
/// Queries sys.foreign_keys to discover FK relationships between tables,
/// then applies Kahn's algorithm to produce a deserialization order
/// where parent tables come before child tables (per D-04, D-05, D-06).
/// </summary>
public class FkDependencyResolver
{
    private readonly ISqlExecutor _sqlExecutor;

    public FkDependencyResolver(ISqlExecutor sqlExecutor)
        => _sqlExecutor = sqlExecutor ?? throw new ArgumentNullException(nameof(sqlExecutor));

    /// <summary>
    /// Return tables in FK dependency order (parents first, children last).
    /// Tables with no FK relationships are placed at the front.
    /// Throws InvalidOperationException if circular dependencies are detected (per D-06).
    /// </summary>
    public List<string> GetDeserializationOrder(IEnumerable<string> tableNames)
    {
        var tables = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
        if (tables.Count == 0)
            return new List<string>();

        var edges = QueryForeignKeyEdges(tables);
        return TopologicalSort(tables, edges);
    }

    /// <summary>
    /// Query sys.foreign_keys for FK edges between the given tables.
    /// Filters: only edges where BOTH parent and child are in the table set.
    /// Skips self-referencing FKs (parent == child) per Pitfall 1.
    /// </summary>
    private List<(string Child, string Parent)> QueryForeignKeyEdges(HashSet<string> tables)
    {
        var edges = new List<(string Child, string Parent)>();

        var cb = new CommandBuilder();
        cb.Add(@"SELECT DISTINCT
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) <> OBJECT_NAME(fk.referenced_object_id)");

        using var reader = _sqlExecutor.ExecuteReader(cb);
        while (reader.Read())
        {
            var child = reader["ChildTable"]?.ToString();
            var parent = reader["ParentTable"]?.ToString();

            if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(parent))
                continue;

            // Skip self-referencing FKs (defense in depth — SQL WHERE also filters these)
            if (string.Equals(child, parent, StringComparison.OrdinalIgnoreCase))
                continue;

            // Only include edges where both tables are in our predicate set
            if (tables.Contains(child) && tables.Contains(parent))
            {
                edges.Add((child, parent));
            }
        }

        return edges;
    }

    /// <summary>
    /// Kahn's algorithm: iteratively remove nodes with in-degree 0.
    /// If nodes remain after processing, they form a cycle -> throw with cycle info (per D-06).
    /// </summary>
    private List<string> TopologicalSort(HashSet<string> tables, List<(string Child, string Parent)> edges)
    {
        // Build in-degree and adjacency (parent -> children)
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            inDegree[table] = 0;
            adjacency[table] = new List<string>();
        }

        foreach (var (child, parent) in edges)
        {
            // Edge: parent -> child (parent must come before child)
            // Map using the canonical casing from tables set
            var canonChild = GetCanonical(tables, child);
            var canonParent = GetCanonical(tables, parent);

            inDegree[canonChild]++;
            adjacency[canonParent].Add(canonChild);
        }

        // Seed queue with nodes that have no incoming edges
        var queue = new Queue<string>();
        foreach (var (table, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(table);
        }

        var result = new List<string>();

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            result.Add(node);

            foreach (var child in adjacency[node])
            {
                inDegree[child]--;
                if (inDegree[child] == 0)
                    queue.Enqueue(child);
            }
        }

        if (result.Count < tables.Count)
        {
            var remainingTables = inDegree
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => kvp.Key)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase);
            throw new InvalidOperationException(
                $"Circular FK dependency detected among tables: {string.Join(", ", remainingTables)}");
        }

        return result;
    }

    /// <summary>
    /// Find the canonical (original-cased) table name from the set.
    /// </summary>
    private static string GetCanonical(HashSet<string> tables, string name)
    {
        foreach (var t in tables)
        {
            if (string.Equals(t, name, StringComparison.OrdinalIgnoreCase))
                return t;
        }
        return name; // shouldn't happen since we pre-filter
    }
}

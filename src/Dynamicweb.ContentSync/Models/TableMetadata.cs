namespace Dynamicweb.ContentSync.Models;

/// <summary>
/// Parsed DataGroup metadata for a single SQL table.
/// Populated from DataGroup XML ProviderParameters and schema introspection.
/// </summary>
public record TableMetadata
{
    /// <summary>SQL table name (e.g., "EcomOrderFlow").</summary>
    public required string TableName { get; init; }

    /// <summary>Column used for human-readable naming (e.g., "OrderFlowName"). Empty if not specified.</summary>
    public string NameColumn { get; init; } = "";

    /// <summary>Columns used for change detection checksum. Empty if not specified (falls back to all non-identity columns).</summary>
    public string CompareColumns { get; init; } = "";

    /// <summary>Primary key columns from sp_pkeys.</summary>
    public required IReadOnlyList<string> KeyColumns { get; init; }

    /// <summary>Identity (auto-increment) columns.</summary>
    public required IReadOnlyList<string> IdentityColumns { get; init; }

    /// <summary>All columns in the table.</summary>
    public required IReadOnlyList<string> AllColumns { get; init; }
}

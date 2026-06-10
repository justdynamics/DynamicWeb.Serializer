namespace Truvio.Commerce.Serializer.Models;

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
    public required List<string> KeyColumns { get; init; }

    /// <summary>Identity (auto-increment) columns.</summary>
    public required List<string> IdentityColumns { get; init; }

    /// <summary>All columns in the table.</summary>
    public required List<string> AllColumns { get; init; }

    /// <summary>
    /// Full column schema definitions for CREATE TABLE on targets where the table is missing.
    /// Populated during serialization from INFORMATION_SCHEMA; empty list if not available.
    /// </summary>
    public List<ColumnDefinition> ColumnDefinitions { get; init; } = [];
}

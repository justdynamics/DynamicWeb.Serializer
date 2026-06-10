namespace Truvio.Commerce.Serializer.Models;

/// <summary>
/// SQL column schema definition stored in _meta.yml for table creation on targets
/// where the table does not yet exist.
/// </summary>
public record ColumnDefinition
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public int MaxLength { get; init; }
    public int Precision { get; init; }
    public int Scale { get; init; }
    public bool IsNullable { get; init; }
    public bool IsIdentity { get; init; }
}

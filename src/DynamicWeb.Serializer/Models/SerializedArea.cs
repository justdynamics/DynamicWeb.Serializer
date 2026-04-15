namespace DynamicWeb.Serializer.Models;

public record SerializedArea
{
    /// <summary>
    /// The Area's UniqueId GUID, captured from the source environment during serialization.
    /// Informational only — NOT used for identity resolution during deserialization.
    /// The target area is resolved by the numeric AreaId from the predicate configuration.
    /// This GUID is preserved for traceability and potential future cross-environment matching.
    /// </summary>
    public required Guid AreaId { get; init; }
    public required string Name { get; init; }
    public required int SortOrder { get; init; }
    public string? ItemType { get; init; }
    public Dictionary<string, object> ItemFields { get; init; } = new();
    public List<SerializedPage> Pages { get; init; } = new();
}

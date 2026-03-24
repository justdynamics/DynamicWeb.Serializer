namespace DynamicWeb.Serializer.Models;

public record SerializedPermission
{
    public required string Owner { get; init; }
    public required string OwnerType { get; init; }
    public string? OwnerId { get; init; }
    public required string Level { get; init; }
    public required int LevelValue { get; init; }
}

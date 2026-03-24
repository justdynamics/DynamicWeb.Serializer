namespace DynamicWeb.Serializer.Models;

public record SerializedParagraph
{
    public required Guid ParagraphUniqueId { get; init; }
    public required int SortOrder { get; init; }
    public string? ItemType { get; init; }
    public string? Header { get; init; }
    public string? Template { get; init; }
    public string? ColorSchemeId { get; init; }
    public string? ModuleSystemName { get; init; }
    public string? ModuleSettings { get; init; }
    public Dictionary<string, object> Fields { get; init; } = new();
    public DateTime? CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public string? CreatedBy { get; init; }
    public string? UpdatedBy { get; init; }
    public int? ColumnId { get; init; }
}

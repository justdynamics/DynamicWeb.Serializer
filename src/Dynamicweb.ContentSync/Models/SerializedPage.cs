namespace Dynamicweb.ContentSync.Models;

public record SerializedPage
{
    public required Guid PageUniqueId { get; init; }
    public required string Name { get; init; }
    public required string MenuText { get; init; }
    public required string UrlName { get; init; }
    public required int SortOrder { get; init; }
    public bool IsActive { get; init; }
    public string? ItemType { get; init; }
    public string? Layout { get; init; }
    public bool LayoutApplyToSubPages { get; init; }
    public bool IsFolder { get; init; }
    public string? TreeSection { get; init; }
    public DateTime? CreatedDate { get; init; }
    public DateTime? UpdatedDate { get; init; }
    public string? CreatedBy { get; init; }
    public string? UpdatedBy { get; init; }
    public Dictionary<string, object> Fields { get; init; } = new();
    public Dictionary<string, object> PropertyFields { get; init; } = new();
    public List<SerializedPermission> Permissions { get; init; } = new();
    public List<SerializedGridRow> GridRows { get; init; } = new();
    public List<SerializedPage> Children { get; init; } = new();
}

namespace Truvio.Commerce.Serializer.Models;

public record SerializedPage
{
    public required Guid PageUniqueId { get; init; }
    public int? SourcePageId { get; init; }
    public required string Name { get; init; }
    public required string MenuText { get; init; }
    public required string UrlName { get; init; }
    public required int SortOrder { get; init; }
    public bool IsActive { get; init; }
    public string? ItemType { get; init; }
    public string? Layout { get; init; }
    public bool LayoutApplyToSubPages { get; init; }
    public bool IsFolder { get; init; }
    /// <summary>
    /// DW page-preset/template flag. Must round-trip: DW's SavePage forces
    /// MenuText = item Title for every NON-template page, so a preset whose menu text
    /// diverges from its item Title (e.g. Swift's "Home preset" with Title "Home") gets
    /// silently renamed on the first post-write save unless this flag is preserved.
    /// </summary>
    public bool IsTemplate { get; init; }
    public string? TreeSection { get; init; }
    public string? NavigationTag { get; init; }
    public string? ShortCut { get; init; }
    public bool Hidden { get; init; }
    public bool Allowclick { get; init; } = true;
    public bool Allowsearch { get; init; } = true;
    public bool ShowInSitemap { get; init; } = true;
    public bool ShowInLegend { get; init; } = true;
    public int SslMode { get; init; }
    public string? ColorSchemeId { get; init; }
    public string? ExactUrl { get; init; }
    public string? ContentType { get; init; }
    public string? TopImage { get; init; }
    public string? DisplayMode { get; init; }
    public DateTime? ActiveFrom { get; init; }
    public DateTime? ActiveTo { get; init; }
    public int PermissionType { get; init; }
    /// <summary>
    /// GUID of the master page when this page is a language-layer copy (Page.MasterPageId > 0).
    /// The master page lives in the master area; deserialize resolves it back to the target's
    /// numeric page ID in the post-write link pass.
    /// </summary>
    public Guid? MasterPageGuid { get; init; }
    /// <summary>DW MasterType (None/Lock/Inherit) for language-layer pages; null when not a language copy.</summary>
    public string? MasterType { get; init; }
    public SerializedSeoSettings? Seo { get; init; }
    public SerializedUrlSettings? UrlSettings { get; init; }
    public SerializedVisibilitySettings? Visibility { get; init; }
    public SerializedNavigationSettings? NavigationSettings { get; init; }
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

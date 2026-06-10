namespace Truvio.Commerce.Serializer.Models;

public record SerializedNavigationSettings
{
    public bool UseEcomGroups { get; init; }
    public string? ParentType { get; init; }
    public string? Groups { get; init; }
    public string? ShopID { get; init; }
    public int MaxLevels { get; init; }
    public string? ProductPage { get; init; }
    public string? NavigationProvider { get; init; }
    public bool IncludeProducts { get; init; }
}

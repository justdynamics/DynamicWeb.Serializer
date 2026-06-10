namespace Truvio.Commerce.Serializer.Models;

public record SerializedVisibilitySettings
{
    public bool HideForPhones { get; init; }
    public bool HideForTablets { get; init; }
    public bool HideForDesktops { get; init; }
}

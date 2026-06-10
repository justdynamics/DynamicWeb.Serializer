namespace Truvio.Commerce.Serializer.Models;

public record SerializedSeoSettings
{
    public string? MetaTitle { get; init; }
    public string? MetaCanonical { get; init; }
    public string? Description { get; init; }
    public string? Keywords { get; init; }
    public bool Noindex { get; init; }
    public bool Nofollow { get; init; }
    public bool Robots404 { get; init; }
}

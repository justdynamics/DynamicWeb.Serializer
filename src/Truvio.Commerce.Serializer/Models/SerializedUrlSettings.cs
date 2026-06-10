namespace Truvio.Commerce.Serializer.Models;

public record SerializedUrlSettings
{
    public string? UrlDataProviderTypeName { get; init; }
    public string? UrlDataProviderParameters { get; init; }
    public bool UrlIgnoreForChildren { get; init; }
    public bool UrlUseAsWritten { get; init; }
}

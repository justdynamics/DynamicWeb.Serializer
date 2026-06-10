namespace Truvio.Commerce.Serializer.Models;

public record SerializedGridColumn
{
    public required int Id { get; init; }
    public int Width { get; init; }
    public List<SerializedParagraph> Paragraphs { get; init; } = new();
}

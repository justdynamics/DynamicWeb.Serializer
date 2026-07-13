namespace Truvio.Commerce.Serializer.AdminUI.Models;

/// <summary>
/// Result payload for <see cref="Commands.SerializerUploadRootCommand"/>: the mode whose tree
/// was replaced, the number of files extracted, and the engine-owned target path the zip was
/// expanded into.
/// </summary>
public sealed class SerializerUploadRootResultModel
{
    public string Mode { get; set; } = "";
    public int FileCount { get; set; }
    public string TargetPath { get; set; } = "";
}

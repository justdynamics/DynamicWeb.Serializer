using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Commands;

/// <summary>
/// Runs after the FileUpload editor on DeserializeZipUploadScreen completes:
/// receives the uploaded file path(s) via WithCommandProperty binding, resolves the
/// target area from the screen's query (DW posts it as QueryData → GetModel()), and
/// routes the first .zip through the existing <see cref="DeserializeFromZipCommand"/> pipeline.
/// </summary>
public sealed class DeserializeUploadedZipCommand : CommandBase<DeserializeZipUploadModel>
{
    /// <summary>Uploaded file paths, bound by the FileUpload editor via WithCommandProperty.</summary>
    public IEnumerable<string> Files { get; set; } = [];

    public override CommandResult Handle()
    {
        var model = GetModel();
        if (model is null || model.TargetAreaId <= 0)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Target area is required" };

        var zip = Files.FirstOrDefault(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (zip is null)
            return new() { Status = CommandResult.ResultType.Invalid, Message = "Upload a .zip file produced by Serialize subtree" };

        // FileUpload reports /Files-rooted virtual paths (e.g. /Files/System/Serializer/Upload/x.zip);
        // normalize defensively in case only a bare file name arrives.
        var filePath = zip.StartsWith('/')
            ? zip
            : $"/Files/System/Serializer/Upload/{Path.GetFileName(zip)}";

        var import = new DeserializeFromZipCommand
        {
            FilePath = filePath,
            TargetAreaId = model.TargetAreaId,
            IsAdminUiInvocation = true
        };

        return import.Handle();
    }
}

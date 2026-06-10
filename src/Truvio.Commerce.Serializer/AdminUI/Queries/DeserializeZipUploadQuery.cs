using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Queries;

/// <summary>
/// Query passed from the content tree right-click menu to DeserializeZipUploadScreen.
/// Carries the target area of the clicked tree node.
/// </summary>
public sealed class DeserializeZipUploadQuery : DataQueryModelBase<DeserializeZipUploadModel>
{
    public int TargetAreaId { get; set; }

    public override DeserializeZipUploadModel? GetModel()
    {
        return DeserializeZipUploadModel.Load(TargetAreaId);
    }
}

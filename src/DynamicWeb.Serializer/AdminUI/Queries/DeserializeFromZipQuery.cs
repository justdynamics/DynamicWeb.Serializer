using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;

namespace DynamicWeb.Serializer.AdminUI.Queries;

/// <summary>
/// Query passed from the FileOverviewScreen injector to DeserializeFromZipScreen.
/// Carries the selected zip file path. TargetAreaId is set via ShadowEdit overlay on the model.
/// </summary>
public sealed class DeserializeFromZipQuery : DataQueryModelBase<DeserializeFromZipModel>
{
    public string FilePath { get; set; } = "";

    public override DeserializeFromZipModel? GetModel()
    {
        return DeserializeFromZipModel.Load(FilePath);
    }
}

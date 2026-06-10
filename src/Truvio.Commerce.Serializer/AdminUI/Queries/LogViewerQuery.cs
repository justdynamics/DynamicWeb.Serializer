using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Queries;

public sealed class LogViewerQuery : DataQueryModelBase<LogViewerModel>
{
    public string? SelectedFileName { get; set; }

    public override LogViewerModel? GetModel()
    {
        return LogViewerModel.Load(SelectedFileName);
    }
}

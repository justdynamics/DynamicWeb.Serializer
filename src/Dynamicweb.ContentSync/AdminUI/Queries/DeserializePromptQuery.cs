using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.CoreUI.Data;

namespace Dynamicweb.ContentSync.AdminUI.Queries;

public sealed class DeserializePromptQuery : DataQueryModelBase<DeserializeModel>
{
    public int PageId { get; set; }
    public int AreaId { get; set; }

    public override DeserializeModel GetModel()
    {
        return new DeserializeModel
        {
            PageId = PageId,
            AreaId = AreaId,
            ImportMode = "children"
        };
    }
}

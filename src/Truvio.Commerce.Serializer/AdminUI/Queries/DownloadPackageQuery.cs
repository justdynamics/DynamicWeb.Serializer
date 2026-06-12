using Dynamicweb.Content;
using Dynamicweb.CoreUI.Data;
using Truvio.Commerce.Serializer.AdminUI.Models;

namespace Truvio.Commerce.Serializer.AdminUI.Queries;

public sealed class DownloadPackageQuery : DataQueryModelBase<DownloadPackageModel>
{
    public int PageId { get; set; }
    public int AreaId { get; set; }

    public override DownloadPackageModel? GetModel()
    {
        string pageName;
        try
        {
            pageName = Services.Pages.GetPage(PageId)?.MenuText ?? $"Page {PageId}";
        }
        catch
        {
            pageName = $"Page {PageId}";
        }

        return new DownloadPackageModel
        {
            PageId = PageId,
            AreaId = AreaId,
            PageName = pageName
        };
    }
}

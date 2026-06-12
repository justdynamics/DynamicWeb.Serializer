using Dynamicweb.Content.UI.Models;
using Dynamicweb.Content.UI.Screens;
using Truvio.Commerce.Serializer.AdminUI.Commands;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Screens;

namespace Truvio.Commerce.Serializer.AdminUI.Injectors;

/// <summary>
/// Injects the "Download Package…" action into the page edit screen's Actions menu
/// (alongside Preview, Paragraphs, etc.). Auto-discovered by DW's AddInManager.
/// Permission-gated like the tree context menu (PackageAccess.CanDownload).
/// </summary>
public sealed class SerializerPageEditInjector : EditScreenInjector<PageEditScreen, PageDataModel>
{
    public override IEnumerable<ActionGroup>? GetScreenActions()
    {
        var model = Screen?.Model;
        if (model == null || model.Id <= 0)
            return null;

        if (!Truvio.Commerce.Serializer.AdminUI.Security.PackageAccess.CanDownload(
                Dynamicweb.Content.Services.Pages.GetPage(model.Id)))
            return null;

        return new[]
        {
            new ActionGroup
            {
                Name = "Serialize",
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Download Package…",
                        Icon = Icon.DownloadAlt,
                        NodeAction = OpenSlideOverAction.To<Screens.DownloadPackageScreen>()
                            .With(new Queries.DownloadPackageQuery { PageId = model.Id, AreaId = model.AreaId })
                    }
                }
            }
        };
    }
}

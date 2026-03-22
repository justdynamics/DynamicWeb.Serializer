using Dynamicweb.Content.UI.Models;
using Dynamicweb.Content.UI.Screens;
using Dynamicweb.ContentSync.AdminUI.Commands;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Screens;

namespace Dynamicweb.ContentSync.AdminUI.Injectors;

/// <summary>
/// Injects "Serialize subtree" action into the page edit screen's Actions menu
/// (alongside Preview, Paragraphs, etc.). Auto-discovered by DW's AddInManager.
/// </summary>
public sealed class ContentSyncPageEditInjector : EditScreenInjector<PageEditScreen, PageDataModel>
{
    public override IEnumerable<ActionGroup>? GetScreenActions()
    {
        var model = Screen?.Model;
        if (model == null || model.Id <= 0)
            return null;

        return new[]
        {
            new ActionGroup
            {
                Name = "Content Sync",
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Serialize subtree",
                        Icon = Icon.DownloadAlt,
                        NodeAction = DownloadFileAction.Using(
                            new SerializeSubtreeCommand { PageId = model.Id, AreaId = model.AreaId }
                        )
                    }
                }
            }
        };
    }
}

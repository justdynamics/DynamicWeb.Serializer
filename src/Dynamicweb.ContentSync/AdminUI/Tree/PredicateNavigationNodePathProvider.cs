using Dynamicweb.Application.UI;
using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.CoreUI.Navigation;

namespace Dynamicweb.ContentSync.AdminUI.Tree;

public sealed class PredicateNavigationNodePathProvider : NavigationNodePathProvider<PredicateListModel>
{
    public PredicateNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(PredicateListModel? model) =>
        new([
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(AreasSection).FullName,
            "Content_Settings",
            SyncSettingsNodeProvider.SyncNodeId,
            SyncSettingsNodeProvider.PredicatesNodeId
        ]);
}

using Dynamicweb.Application.UI;
using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.CoreUI.Navigation;

namespace Dynamicweb.ContentSync.AdminUI.Tree;

public sealed class SyncNavigationNodePathProvider : NavigationNodePathProvider<SyncSettingsModel>
{
    public SyncNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(SyncSettingsModel? model) =>
        new([
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(AreasSection).FullName,
            "Content_Settings",
            SyncSettingsNodeProvider.SyncNodeId
        ]);
}

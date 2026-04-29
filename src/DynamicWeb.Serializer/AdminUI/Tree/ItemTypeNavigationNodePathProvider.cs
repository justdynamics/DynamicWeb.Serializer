using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Navigation;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class ItemTypeNavigationNodePathProvider : NavigationNodePathProvider<ItemTypeListModel>
{
    public ItemTypeNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(ItemTypeListModel? model)
    {
        // Phase 40 D-06: single Item Types subtree (mode-agnostic — exclusions are top-level dicts).
        // Path walks: Settings → System → Developer → Serialize → Item Types.
        return new NavigationNodePath(new[]
        {
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            SerializerSettingsNodeProvider.DeveloperRootId,
            SerializerSettingsNodeProvider.SerializeNodeId,
            SerializerSettingsNodeProvider.ItemTypesNodeId
        });
    }
}

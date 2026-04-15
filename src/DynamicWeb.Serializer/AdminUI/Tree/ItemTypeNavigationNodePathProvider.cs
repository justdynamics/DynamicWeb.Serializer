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

    protected override NavigationNodePath GetNavigationNodePathInternal(ItemTypeListModel? model) =>
        new([
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            "Settings_Database",
            SerializerSettingsNodeProvider.SerializeNodeId,
            SerializerSettingsNodeProvider.ItemTypesNodeId
        ]);
}

using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Navigation;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class ItemTypeEditNavigationNodePathProvider : NavigationNodePathProvider<ItemTypeEditModel>
{
    public ItemTypeEditNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(ItemTypeEditModel? model) =>
        new([
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            "Settings_Database",
            SerializerSettingsNodeProvider.SerializeNodeId,
            SerializerSettingsNodeProvider.ItemTypesNodeId
        ]);
}

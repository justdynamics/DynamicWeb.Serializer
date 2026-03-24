using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Navigation;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class SerializerNavigationNodePathProvider : NavigationNodePathProvider<SerializerSettingsModel>
{
    public SerializerNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(SerializerSettingsModel? model) =>
        new([
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(AreasSection).FullName,
            "Content_Settings",
            SerializerSettingsNodeProvider.SyncNodeId
        ]);
}

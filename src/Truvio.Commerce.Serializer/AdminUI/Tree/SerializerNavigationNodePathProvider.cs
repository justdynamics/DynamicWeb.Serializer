using Dynamicweb.Application.UI;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Navigation;

namespace Truvio.Commerce.Serializer.AdminUI.Tree;

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
            typeof(SystemSection).FullName,
            SerializerSettingsNodeProvider.DeveloperRootId,
            SerializerSettingsNodeProvider.SerializeNodeId
        ]);
}

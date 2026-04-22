using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
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
        // Terminate at the per-mode Item Types node. Path walks: Settings -> System ->
        // Developer -> Serialize -> {Deploy|Seed group} -> Item Types.
        var mode = model?.Mode ?? DeploymentMode.Deploy;
        var groupNode = mode == DeploymentMode.Deploy
            ? SerializerSettingsNodeProvider.DeployGroupNodeId
            : SerializerSettingsNodeProvider.SeedGroupNodeId;
        var terminal = mode == DeploymentMode.Deploy
            ? SerializerSettingsNodeProvider.DeployItemTypesNodeId
            : SerializerSettingsNodeProvider.SeedItemTypesNodeId;

        return new NavigationNodePath(new[]
        {
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            SerializerSettingsNodeProvider.DeveloperRootId,
            SerializerSettingsNodeProvider.SerializeNodeId,
            groupNode,
            terminal
        });
    }
}

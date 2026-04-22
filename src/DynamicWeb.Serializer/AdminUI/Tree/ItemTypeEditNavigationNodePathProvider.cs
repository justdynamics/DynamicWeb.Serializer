using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Navigation;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class ItemTypeEditNavigationNodePathProvider : NavigationNodePathProvider<ItemTypeEditModel>
{
    public ItemTypeEditNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(ItemTypeEditModel? model)
    {
        // Terminate at the per-mode Item Types node so the tree highlights the correct
        // Deploy/Seed subtree. Path walks: Settings -> System -> Developer -> Serialize ->
        // {Deploy|Seed group} -> Item Types. Null model defaults to Deploy.
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

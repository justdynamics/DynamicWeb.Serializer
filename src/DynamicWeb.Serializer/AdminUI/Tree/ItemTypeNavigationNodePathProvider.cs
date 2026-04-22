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
        // Phase 37-01.1 Task 2: terminate at the per-mode Item Types node.
        var mode = model?.Mode ?? DeploymentMode.Deploy;
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
            terminal
        });
    }
}

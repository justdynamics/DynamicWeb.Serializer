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
        // Phase 37-01.1 Task 2: terminate at the per-mode Item Types node so the tree highlights
        // the correct Deploy/Seed subtree. Null model defaults to Deploy to match the pre-split
        // behaviour.
        var mode = model?.Mode ?? DeploymentMode.Deploy;
        var terminal = mode == DeploymentMode.Deploy
            ? SerializerSettingsNodeProvider.DeployItemTypesNodeId
            : SerializerSettingsNodeProvider.SeedItemTypesNodeId;

        return new NavigationNodePath(new[]
        {
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            "Settings_Database",
            SerializerSettingsNodeProvider.SerializeNodeId,
            terminal
        });
    }
}

using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Navigation;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class PredicateNavigationNodePathProvider : NavigationNodePathProvider<PredicateListModel>
{
    public PredicateNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(PredicateListModel? model)
    {
        // Phase 37-01 D-02: terminate the path at the correct per-mode predicate-group node so
        // the tree highlights Deploy or Seed correctly.
        var mode = model?.Mode ?? DeploymentMode.Deploy;
        var terminalNode = mode == DeploymentMode.Deploy
            ? SerializerSettingsNodeProvider.DeployPredicatesNodeId
            : SerializerSettingsNodeProvider.SeedPredicatesNodeId;

        return new NavigationNodePath(new[]
        {
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            "Settings_Database",
            SerializerSettingsNodeProvider.SerializeNodeId,
            terminalNode
        });
    }
}

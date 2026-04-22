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
        // Terminate the path at the correct per-mode predicate-group node so the tree
        // highlights Deploy or Seed correctly. Path walks: Settings -> System -> Developer ->
        // Serialize -> {Deploy|Seed group} -> Predicates.
        var mode = model?.Mode ?? DeploymentMode.Deploy;
        var groupNode = mode == DeploymentMode.Deploy
            ? SerializerSettingsNodeProvider.DeployGroupNodeId
            : SerializerSettingsNodeProvider.SeedGroupNodeId;
        var terminalNode = mode == DeploymentMode.Deploy
            ? SerializerSettingsNodeProvider.DeployPredicatesNodeId
            : SerializerSettingsNodeProvider.SeedPredicatesNodeId;

        return new NavigationNodePath(new[]
        {
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            SerializerSettingsNodeProvider.DeveloperRootId,
            SerializerSettingsNodeProvider.SerializeNodeId,
            groupNode,
            terminalNode
        });
    }
}

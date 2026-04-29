using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
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
        // Phase 40 D-06: single predicate subtree — no Deploy/Seed group split.
        // Path: Settings → System → Developer → Serialize → Predicates.
        return new NavigationNodePath(new[]
        {
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            SerializerSettingsNodeProvider.DeveloperRootId,
            SerializerSettingsNodeProvider.SerializeNodeId,
            SerializerSettingsNodeProvider.PredicatesNodeId
        });
    }
}

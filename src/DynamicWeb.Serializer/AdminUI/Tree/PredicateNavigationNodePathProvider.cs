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

    protected override NavigationNodePath GetNavigationNodePathInternal(PredicateListModel? model) =>
        new([
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(AreasSection).FullName,
            "Content_Settings",
            SerializerSettingsNodeProvider.SyncNodeId,
            SerializerSettingsNodeProvider.PredicatesNodeId
        ]);
}

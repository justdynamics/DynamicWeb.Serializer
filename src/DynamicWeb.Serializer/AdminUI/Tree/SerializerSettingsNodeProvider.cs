using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Queries;
using DynamicWeb.Serializer.AdminUI.Screens;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Navigation;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class SerializerSettingsNodeProvider : NavigationNodeProvider<AreasSection>
{
    // The Content root node ID from Dynamicweb.Content.UI.SettingsNodeProvider
    // This is constructed from $"{PREFIX}Settings" where PREFIX = "Content_"
    private const string ContentRootId = "Content_Settings";
    internal const string SyncNodeId = "Serializer_Settings";
    internal const string PredicatesNodeId = "Serializer_Predicates";

    public override IEnumerable<NavigationNode> GetRootNodes()
    {
        // We do NOT create a root node -- "Content" already exists
        yield break;
    }

    public override IEnumerable<NavigationNode> GetSubNodes(NavigationNodePath parentNodePath)
    {
        if (parentNodePath.Last == ContentRootId)
        {
            yield return new NavigationNode
            {
                Id = SyncNodeId,
                Name = "Serialize",
                Sort = 100,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<SerializerSettingsEditScreen>()
                    .With(new SerializerSettingsQuery())
            };
        }
        else if (parentNodePath.Last == SyncNodeId)
        {
            yield return new NavigationNode
            {
                Id = PredicatesNodeId,
                Name = "Predicates",
                Sort = 10,
                HasSubNodes = false,
                NodeAction = NavigateScreenAction.To<PredicateListScreen>()
                    .With(new PredicateListQuery())
            };
        }
    }
}

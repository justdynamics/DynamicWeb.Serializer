using Dynamicweb.Application.UI;
using Dynamicweb.ContentSync.AdminUI.Queries;
using Dynamicweb.ContentSync.AdminUI.Screens;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Navigation;

namespace Dynamicweb.ContentSync.AdminUI.Tree;

public sealed class SyncSettingsNodeProvider : NavigationNodeProvider<AreasSection>
{
    // The Content root node ID from Dynamicweb.Content.UI.SettingsNodeProvider
    // This is constructed from $"{PREFIX}Settings" where PREFIX = "Content_"
    private const string ContentRootId = "Content_Settings";
    internal const string SyncNodeId = "ContentSync_Settings";
    internal const string PredicatesNodeId = "ContentSync_Predicates";

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
                Name = "Sync",
                Sort = 100,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<SyncSettingsEditScreen>()
                    .With(new SyncSettingsQuery())
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

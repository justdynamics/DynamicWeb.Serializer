using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Queries;
using Truvio.Commerce.Serializer.AdminUI.Screens;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using Truvio.Commerce.Serializer.Serialization;
using Dynamicweb.Application.UI.TreeNavigation;
using Dynamicweb.Content;
using Dynamicweb.Content.UI;
using Dynamicweb.CoreUI;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Navigation;
using Dynamicweb.CoreUI.Screens;

namespace Truvio.Commerce.Serializer.AdminUI.Injectors;

/// <summary>
/// Decorates Content-tree page nodes on tree expansion / section loads (TreeNodesScreen):
/// adds a "Truvio Serializer" right-click group with Serialize subtree / Deserialize from zip,
/// and an annotation icon on pages covered by a deploy-mode content predicate.
/// Auto-discovered by DW's AddInManager. The initial full-tree render goes through
/// <see cref="TreeScreen"/> instead — covered by <see cref="SerializerTreeInjector"/>.
/// </summary>
public sealed class SerializerTreeNodesInjector : ScreenInjector<TreeNodesScreen>
{
    public override void OnAfter(TreeNodesScreen screen, UiComponentBase content)
    {
        if (!TreeNodeDecorator.IsContentAreaPath(screen?.Model?.Path))
            return;

        if (!content.TryGet<TreeNodes>(out var tree) || tree is null)
            return;

        var evaluator = TreeNodeDecorator.TryCreateEvaluator();
        foreach (var node in tree.Nodes)
            TreeNodeDecorator.Decorate(node, evaluator);
    }
}

/// <summary>
/// Same decoration for the initial full-tree render (sections + root nodes), which goes
/// through <see cref="TreeScreen"/> rather than <see cref="TreeNodesScreen"/>.
/// </summary>
public sealed class SerializerTreeInjector : ScreenInjector<TreeScreen>
{
    public override void OnAfter(TreeScreen screen, UiComponentBase content)
    {
        if (!TreeNodeDecorator.IsContentAreaPath(screen?.Model?.Path))
            return;

        if (!content.TryGet<Dynamicweb.CoreUI.Navigation.Tree>(out var tree) || tree is null)
            return;

        var evaluator = TreeNodeDecorator.TryCreateEvaluator();
        foreach (var section in tree.Sections)
            foreach (var node in section.Nodes)
                TreeNodeDecorator.Decorate(node, evaluator);
    }
}

/// <summary>
/// Shared node decoration for the two tree screens.
/// </summary>
internal static class TreeNodeDecorator
{
    public static bool IsContentAreaPath(NavigationNodePath? path)
        => path is not null
           && string.Equals(path.First, typeof(ContentArea).FullName, StringComparison.Ordinal);

    public static void Decorate(NavigationNode node, ContentCoverageEvaluator? evaluator)
    {
        if (int.TryParse(node.Id, out var pageId) && pageId > 0)
        {
            var page = Services.Pages.GetPage(pageId);
            if (page is not null)
            {
                node.ContextActionGroups = node.ContextActionGroups.Append(new ActionGroup
                {
                    Nodes =
                    [
                        new ActionNode
                        {
                            Name = "Serialize subtree",
                            Icon = Icon.DownloadAlt,
                            NodeAction = DownloadFileAction.Using(
                                new SerializeSubtreeCommand { PageId = pageId, AreaId = page.AreaId })
                        },
                        new ActionNode
                        {
                            Name = "Deserialize from zip",
                            Icon = Icon.UploadAlt,
                            NodeAction = OpenDialogAction.To<DeserializeZipUploadScreen>()
                                .With(new DeserializeZipUploadQuery { TargetAreaId = page.AreaId })
                        }
                    ]
                });

                if (evaluator is not null)
                {
                    // Language-layer pages are matched in their master's path space — predicate
                    // paths are authored against the master area (same rule as serialize time).
                    var checkPath = GetPredicateCheckPath(page);
                    var result = evaluator.Evaluate(checkPath, page.AreaId);
                    if (result.Coverage != ContentCoverage.None)
                    {
                        node.Annotations.Add(new ActionNode
                        {
                            Name = result.Explanation,
                            Icon = result.Coverage == ContentCoverage.Full ? Icon.Sync : Icon.SyncSlash,
                            Sort = 200
                        });
                    }
                }
            }
        }

        // OpenTo deep-links and section loads can deliver pre-expanded children.
        foreach (var child in node.Nodes)
            Decorate(child, evaluator);
    }

    /// <summary>
    /// Builds the coverage evaluator from the deploy-mode Content predicates, expanded for
    /// language layers (so language-area pages report coverage like their masters).
    /// Built once per tree render; null when no config / no deploy Content predicates exist.
    /// </summary>
    public static ContentCoverageEvaluator? TryCreateEvaluator()
    {
        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return null;

            var predicates = ConfigLoader.Load(configPath).Predicates
                .Where(p => p.Mode == DeploymentMode.Deploy
                    && string.Equals(p.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (predicates.Count == 0)
                return null;

            var expanded = LanguageLayerExpander.Expand(
                predicates, LanguageLayerExpander.GetLanguageAreaIdsFromDw);
            return new ContentCoverageEvaluator(expanded);
        }
        catch
        {
            // Tree decoration is best-effort; never break the admin tree over config issues.
            return null;
        }
    }

    /// <summary>
    /// Predicate paths live in the master area's path space; for a language-layer page
    /// (MasterPageId > 0) rebuild the path from the master chain, otherwise use its own path.
    /// </summary>
    private static string GetPredicateCheckPath(Page page)
    {
        if (page.MasterPageId <= 0)
            return SerializeSubtreeCommand.BuildContentPath(page);

        try
        {
            var master = Services.Pages.GetPage(page.MasterPageId);
            return master is not null
                ? SerializeSubtreeCommand.BuildContentPath(master)
                : SerializeSubtreeCommand.BuildContentPath(page);
        }
        catch
        {
            return SerializeSubtreeCommand.BuildContentPath(page);
        }
    }
}

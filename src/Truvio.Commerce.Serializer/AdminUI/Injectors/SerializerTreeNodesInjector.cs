using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Queries;
using Truvio.Commerce.Serializer.AdminUI.Screens;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
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

        var matcher = TreeNodeDecorator.DeployPredicateMatcher.TryCreate();
        foreach (var node in tree.Nodes)
            TreeNodeDecorator.Decorate(node, matcher);
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

        var matcher = TreeNodeDecorator.DeployPredicateMatcher.TryCreate();
        foreach (var section in tree.Sections)
            foreach (var node in section.Nodes)
                TreeNodeDecorator.Decorate(node, matcher);
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

    public static void Decorate(NavigationNode node, DeployPredicateMatcher? matcher)
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

                if (matcher is not null)
                {
                    var contentPath = SerializeSubtreeCommand.BuildContentPath(page);
                    if (matcher.IsManagedAtDeploy(contentPath, page.AreaId))
                    {
                        node.Annotations.Add(new ActionNode
                        {
                            Name = "Managed by Truvio Serializer at deploy",
                            Icon = Icon.Sync,
                            Sort = 200
                        });
                    }
                }
            }
        }

        // OpenTo deep-links and section loads can deliver pre-expanded children.
        foreach (var child in node.Nodes)
            Decorate(child, matcher);
    }

    /// <summary>
    /// Evaluates whether a content path/area is covered by any deploy-mode Content predicate.
    /// Built once per tree render; null when no config exists on this solution.
    /// </summary>
    internal sealed class DeployPredicateMatcher
    {
        private readonly List<ContentPredicate> _predicates;

        private DeployPredicateMatcher(List<ContentPredicate> predicates)
        {
            _predicates = predicates;
        }

        public static DeployPredicateMatcher? TryCreate()
        {
            try
            {
                var configPath = ConfigPathResolver.FindConfigFile();
                if (configPath == null)
                    return null;

                var config = ConfigLoader.Load(configPath);
                var predicates = config.Predicates
                    .Where(p => p.Mode == DeploymentMode.Deploy
                        && string.Equals(p.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
                    .Select(p => new ContentPredicate(p))
                    .ToList();

                return predicates.Count > 0 ? new DeployPredicateMatcher(predicates) : null;
            }
            catch
            {
                // Tree decoration is best-effort; never break the admin tree over config issues.
                return null;
            }
        }

        public bool IsManagedAtDeploy(string contentPath, int areaId)
            => _predicates.Any(p => p.ShouldInclude(contentPath, areaId));
    }
}

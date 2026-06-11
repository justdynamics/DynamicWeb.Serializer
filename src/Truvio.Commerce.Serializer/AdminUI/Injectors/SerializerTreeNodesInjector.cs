using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Models;
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

        var evaluators = TreeNodeDecorator.TryCreateEvaluators();
        foreach (var node in tree.Nodes)
            TreeNodeDecorator.Decorate(node, evaluators);
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

        var evaluators = TreeNodeDecorator.TryCreateEvaluators();
        foreach (var section in tree.Sections)
            foreach (var node in section.Nodes)
                TreeNodeDecorator.Decorate(node, evaluators);
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

    /// <summary>
    /// Per-mode coverage evaluators for tree annotations and edit-screen alerts.
    /// <paramref name="ShowSeedIndicators"/> gates every seed-mode cue — the tree's flower
    /// icon AND the seed info alert on editing screens (config: showSeedIndicators, default
    /// off — broad seed coverage drowns the deploy/partial cues, which carry the actionable
    /// signal). The exclusion dicts ride along so per-page field-level carve-outs (e.g. the
    /// cart page's eCom_CartV2 settings) can downgrade a "fully managed" verdict to partial.
    /// </summary>
    internal sealed record CoverageEvaluators(
        ContentCoverageEvaluator? Deploy,
        ContentCoverageEvaluator? Seed,
        bool ShowSeedIndicators,
        IReadOnlyDictionary<string, List<string>> ExcludeFieldsByItemType,
        IReadOnlyDictionary<string, List<string>> ExcludeXmlElementsByType,
        DateTime? LastDeployUtc);

    public static void Decorate(NavigationNode node, CoverageEvaluators? evaluators)
    {
        if (int.TryParse(node.Id, out var pageId) && pageId > 0)
        {
            var page = Services.Pages.GetPage(pageId);
            if (page is not null)
            {
                IReadOnlyList<FieldCarveOut> carveOuts = Array.Empty<FieldCarveOut>();

                if (evaluators is not null)
                {
                    // Language-layer pages are matched in their master's path space — predicate
                    // paths are authored against the master area (same rule as serialize time).
                    var checkPath = GetPredicateCheckPath(page);

                    var deploy = evaluators.Deploy?.Evaluate(checkPath, page.AreaId);
                    var seed = evaluators.ShowSeedIndicators
                        ? evaluators.Seed?.Evaluate(checkPath, page.AreaId)
                        : null;

                    // Field-level carve-outs apply only when the page ITSELF is managed —
                    // the "contains managed subtrees" flavour of Partial carries no page
                    // content to carve from.
                    var deployManagesPage = deploy is not null && deploy.Coverage != ContentCoverage.None
                        && evaluators.Deploy!.GetManagingPredicateNames(checkPath, page.AreaId).Count > 0;
                    var seedManagesPage = seed is not null && seed.Coverage != ContentCoverage.None
                        && evaluators.Seed!.GetManagingPredicateNames(checkPath, page.AreaId).Count > 0;
                    if (deployManagesPage || seedManagesPage)
                        carveOuts = GetFieldCarveOuts(page, evaluators);

                    if (deploy is not null && deploy.Coverage != ContentCoverage.None)
                    {
                        var explanation = deploy.Explanation;
                        var isPartial = deploy.Coverage != ContentCoverage.Full;
                        if (deployManagesPage && carveOuts.Count > 0)
                        {
                            // Cart-page case: path algebra says fully managed, but excluded
                            // fields/settings on this page stay local — show partial.
                            isPartial = true;
                            explanation = $"Partially managed — {explanation}; excluded on this page: "
                                + string.Join("; ", carveOuts.Select(c => c.Label))
                                + ". Right-click > Truvio Serializer to view the excluded fields.";
                        }
                        if (deployManagesPage && IsEditedSinceLastDeploy(page, evaluators.LastDeployUtc))
                        {
                            explanation += " — changed on this environment after the last deploy; the next deploy will overwrite those changes";
                        }
                        node.Annotations.Add(new ActionNode
                        {
                            Name = explanation,
                            Icon = isPartial ? Icon.SyncSlash : Icon.Sync,
                            Sort = 200
                        });
                    }

                    if (seed is not null && seed.Coverage != ContentCoverage.None)
                    {
                        var explanation = $"{seed.Explanation} (seed fills empty fields once; edits on this environment are preserved)";
                        if (seedManagesPage && carveOuts.Count > 0)
                            explanation += $" — never filled (excluded by type): {string.Join("; ", carveOuts.Select(c => c.Label))}";
                        node.Annotations.Add(new ActionNode
                        {
                            Name = explanation,
                            Icon = Icon.Flower,
                            Sort = 210
                        });
                    }
                }

                // Context menu: serializer actions + a click-through per carve-out type, so
                // "WHICH 21 settings?" is one right-click away from the icon that raised it.
                var groupNodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Serialize subtree",
                        Icon = Icon.DownloadAlt,
                        NodeAction = DownloadFileAction.Using(
                            new SerializeSubtreeCommand { PageId = pageId, AreaId = page.AreaId })
                    },
                    new()
                    {
                        Name = "Deserialize from zip",
                        Icon = Icon.UploadAlt,
                        NodeAction = OpenDialogAction.To<DeserializeZipUploadScreen>()
                            .With(new DeserializeZipUploadQuery { TargetAreaId = page.AreaId })
                    }
                };
                if (carveOuts.Count > 0)
                {
                    // ONE short entry regardless of how many types are carved out — per-type
                    // labels in the context menu bloat it; the detail SlideOver lists them all.
                    groupNodes.Add(new ActionNode
                    {
                        Name = "View excluded fields",
                        Icon = Icon.ListUl,
                        NodeAction = OpenSlideOverAction.To<CarveOutDetailScreen>()
                            .With(new CarveOutDetailQuery { Kind = CarveOutDetailModel.KindPage, PageId = pageId })
                    });
                }

                node.ContextActionGroups = node.ContextActionGroups.Append(new ActionGroup
                {
                    Nodes = groupNodes
                });
            }
        }

        // OpenTo deep-links and section loads can deliver pre-expanded children.
        foreach (var child in node.Nodes)
            Decorate(child, evaluators);
    }

    /// <summary>
    /// Drift v1: was this page edited on THIS environment after the last deploy landed?
    /// Timestamp-based (page audit date vs. the newest deploy-received log summary) with a
    /// 5-minute grace margin so pages the deploy itself wrote don't read as drifted.
    /// Occasional false positives from system touches are the accepted v1 tradeoff; the
    /// honest alternative is a per-page YAML diff.
    /// </summary>
    internal static bool IsEditedSinceLastDeploy(Page page, DateTime? lastDeployUtc)
    {
        if (lastDeployUtc is null)
            return false;
        try
        {
            var threshold = lastDeployUtc.Value.ToLocalTime() + TimeSpan.FromMinutes(5);
            return page.Audit?.LastModifiedAt > threshold;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Click-through to the exact exclusion list behind a carve-out. Opens the read-only
    /// "Stays local" SlideOver (CarveOutDetailScreen) — works from every context including
    /// SlideOver-hosted editors (where navigation actions are suppressed by the frontend)
    /// and needs no Settings permission. Used by the editing-screen header chips.
    /// </summary>
    internal static Dynamicweb.CoreUI.Actions.ActionBase CreateCarveOutNavigation(FieldCarveOut carveOut) =>
        OpenSlideOverAction.To<CarveOutDetailScreen>()
            .With(new CarveOutDetailQuery
            {
                TypeName = carveOut.TypeName,
                Kind = carveOut.Kind == CarveOutKind.XmlElements
                    ? CarveOutDetailModel.KindXmlElements
                    : CarveOutDetailModel.KindItemTypeFields
            });

    /// <summary>
    /// Builds per-mode coverage evaluators from the Content predicates, expanded for
    /// language layers (so language-area pages report coverage like their masters).
    /// Built once per tree render; null when no config / no Content predicates exist.
    /// </summary>
    internal static CoverageEvaluators? TryCreateEvaluators()
    {
        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
                return null;

            var config = ConfigLoader.Load(configPath);
            var contentPredicates = config.Predicates
                .Where(p => string.Equals(p.ProviderType, "Content", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (contentPredicates.Count == 0)
                return null;

            ContentCoverageEvaluator? Build(DeploymentMode mode, string word)
            {
                var modePredicates = contentPredicates.Where(p => p.Mode == mode).ToList();
                if (modePredicates.Count == 0)
                    return null;
                var expanded = LanguageLayerExpander.Expand(
                    modePredicates, LanguageLayerExpander.GetLanguageAreaIdsFromDw);
                return new ContentCoverageEvaluator(expanded, word);
            }

            var deploy = Build(DeploymentMode.Deploy, "deploy");
            var seed = Build(DeploymentMode.Seed, "seed");
            return deploy is null && seed is null
                ? null
                : new CoverageEvaluators(deploy, seed, config.ShowSeedIndicators,
                    config.ExcludeFieldsByItemType, config.ExcludeXmlElementsByType,
                    Reporting.LastRunResolver.FindLastDeployReceivedUtc());
        }
        catch
        {
            // Tree decoration is best-effort; never break the admin tree over config issues.
            return null;
        }
    }

    /// <summary>
    /// Field-level carve-outs for one page: types found on the page (page item type, URL
    /// provider, paragraph item types / module settings) that have non-empty entries in the
    /// global exclusion dicts. Loads the page's paragraphs — call only for pages a predicate
    /// actually manages. Best-effort: an unreadable page reports no carve-outs.
    /// </summary>
    internal static IReadOnlyList<FieldCarveOut> GetFieldCarveOuts(Page page, CoverageEvaluators evaluators)
    {
        if (evaluators.ExcludeFieldsByItemType.Count == 0 && evaluators.ExcludeXmlElementsByType.Count == 0)
            return Array.Empty<FieldCarveOut>();

        try
        {
            var paragraphs = Services.Paragraphs.GetParagraphsByPageId(page.ID)
                .Select(p => ((string?)p.ItemType, (string?)p.ModuleSystemName));
            return FieldExclusionInspector.Describe(
                page.ItemType, page.UrlDataProviderTypeName, paragraphs,
                evaluators.ExcludeFieldsByItemType, evaluators.ExcludeXmlElementsByType);
        }
        catch
        {
            return Array.Empty<FieldCarveOut>();
        }
    }

    /// <summary>
    /// Predicate paths live in the master area's path space; for a language-layer page
    /// (MasterPageId > 0) rebuild the path from the master chain, otherwise use its own path.
    /// </summary>
    internal static string GetPredicateCheckPath(Page page)
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

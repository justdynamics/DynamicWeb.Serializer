using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Queries;
using DynamicWeb.Serializer.AdminUI.Screens;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.Content.Items;
using Dynamicweb.Content.Items.Metadata;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Navigation;
using Dynamicweb.Core.UI.Icons;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class SerializerSettingsNodeProvider : NavigationNodeProvider<SystemSection>
{
    // Parent node ID under Settings > System > Developer. Moved from Settings > Database
    // to Settings > Developer because Serialize is a developer/deployment tool, not a
    // database-administration tool.
    public const string DeveloperRootId = "Settings_Developer";
    public const string SerializeNodeId = "Serializer_Settings";
    // Phase 37-01 D-02: the flat Predicates node is split into two mode-scoped nodes.
    // The legacy PredicatesNodeId is kept as a constant for backwards compatibility of any
    // path-provider that has not migrated, but it is no longer emitted under SerializeNodeId.
    public const string PredicatesNodeId = "Serializer_Predicates";
    public const string DeployPredicatesNodeId = "Serializer_Deploy_Predicates";
    public const string SeedPredicatesNodeId = "Serializer_Seed_Predicates";
    // Phase 37-01.1 Task 2: Item Types and XML Types are split per-mode so Deploy.* and Seed.*
    // ModeConfig.ExcludeFieldsByItemType / ExcludeXmlElementsByType can be edited independently
    // through the admin UI tree. The legacy shared ItemTypesNodeId + EmbeddedXmlNodeId constants
    // are removed (0.x beta, no backcompat per memory/feedback_no_backcompat.md).
    public const string DeployItemTypesNodeId = "Serializer_Deploy_ItemTypes";
    public const string SeedItemTypesNodeId = "Serializer_Seed_ItemTypes";
    public const string DeployXmlTypesNodeId = "Serializer_Deploy_XmlTypes";
    public const string SeedXmlTypesNodeId = "Serializer_Seed_XmlTypes";
    public const string LogViewerNodeId = "Serializer_LogViewer";

    // Per-mode prefixes ensure leaf node IDs never collide between Deploy and Seed subtrees.
    private const string DeployItemTypeCatPrefix = "Serializer_Deploy_ItemType_Cat_";
    private const string SeedItemTypeCatPrefix = "Serializer_Seed_ItemType_Cat_";
    private const string DeployItemTypeLeafPrefix = "Serializer_Deploy_ItemType_";
    private const string SeedItemTypeLeafPrefix = "Serializer_Seed_ItemType_";
    // Node IDs cannot contain '/' — DW NavigationNodePath splits on it.
    // Use '~' as separator in node IDs and convert back to '/' for category matching.
    private const char NodeIdCategorySeparator = '~';

    public override IEnumerable<NavigationNode> GetRootNodes()
    {
        // We do NOT create a root node -- "Content" already exists
        yield break;
    }

    public override IEnumerable<NavigationNode> GetSubNodes(NavigationNodePath parentNodePath)
    {
        if (parentNodePath.Last == DeveloperRootId)
        {
            yield return new NavigationNode
            {
                Id = SerializeNodeId,
                Name = "Serialize",
                Icon = Icon.Exchange,
                Sort = 100,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<SerializerSettingsEditScreen>()
                    .With(new SerializerSettingsQuery())
            };
        }
        else if (parentNodePath.Last == SerializeNodeId)
        {
            // Phase 37-01 D-02: Deploy + Seed are two sibling predicate-group nodes.
            yield return new NavigationNode
            {
                Id = DeployPredicatesNodeId,
                Name = "Deploy Predicates",
                Icon = Icon.Filter,
                Sort = 10,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<PredicateListScreen>()
                    .With(new PredicateListQuery { Mode = DeploymentMode.Deploy })
            };

            yield return new NavigationNode
            {
                Id = SeedPredicatesNodeId,
                Name = "Seed Predicates",
                Icon = Icon.Flask, // Flask = experimental / lab-like, fits one-time seed-content role
                Sort = 11,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<PredicateListScreen>()
                    .With(new PredicateListQuery { Mode = DeploymentMode.Seed })
            };

            // Phase 37-01.1 Task 2: Item Types and XML Types are per-mode. The old shared
            // ItemTypesNodeId / EmbeddedXmlNodeId roots are replaced by four nodes — one Item
            // Types and one XML Types node each for Deploy and Seed — so the two ModeConfig
            // exclusion dictionaries are independently editable.
            yield return new NavigationNode
            {
                Id = DeployItemTypesNodeId,
                Name = "Deploy Item Types",
                Icon = Icon.ListUl,
                Sort = 12,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
                    .With(new ItemTypeListQuery { Mode = DeploymentMode.Deploy })
            };

            yield return new NavigationNode
            {
                Id = SeedItemTypesNodeId,
                Name = "Seed Item Types",
                Icon = Icon.ListUl,
                Sort = 13,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
                    .With(new ItemTypeListQuery { Mode = DeploymentMode.Seed })
            };

            yield return new NavigationNode
            {
                Id = DeployXmlTypesNodeId,
                Name = "Deploy Embedded XML",
                Icon = Icon.BracketsCurly,
                Sort = 15,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<XmlTypeListScreen>()
                    .With(new XmlTypeListQuery { Mode = DeploymentMode.Deploy })
            };

            yield return new NavigationNode
            {
                Id = SeedXmlTypesNodeId,
                Name = "Seed Embedded XML",
                Icon = Icon.BracketsCurly,
                Sort = 16,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<XmlTypeListScreen>()
                    .With(new XmlTypeListQuery { Mode = DeploymentMode.Seed })
            };

            yield return new NavigationNode
            {
                Id = LogViewerNodeId,
                Name = "Log Viewer",
                Icon = Icon.History,
                Sort = 20,
                HasSubNodes = false,
                NodeAction = NavigateScreenAction.To<LogViewerScreen>()
                    .With(new LogViewerQuery())
            };
        }
        else if (parentNodePath.Last == DeployPredicatesNodeId)
        {
            foreach (var node in GetModePredicateNodes(DeploymentMode.Deploy, "Deploy"))
                yield return node;
        }
        else if (parentNodePath.Last == SeedPredicatesNodeId)
        {
            foreach (var node in GetModePredicateNodes(DeploymentMode.Seed, "Seed"))
                yield return node;
        }
        else if (parentNodePath.Last == DeployXmlTypesNodeId)
        {
            foreach (var node in GetXmlTypeNodes(DeploymentMode.Deploy))
                yield return node;
        }
        else if (parentNodePath.Last == SeedXmlTypesNodeId)
        {
            foreach (var node in GetXmlTypeNodes(DeploymentMode.Seed))
                yield return node;
        }
        else if (parentNodePath.Last == DeployItemTypesNodeId)
        {
            foreach (var node in GetItemTypeCategoryNodes(DeploymentMode.Deploy, null))
                yield return node;
        }
        else if (parentNodePath.Last == SeedItemTypesNodeId)
        {
            foreach (var node in GetItemTypeCategoryNodes(DeploymentMode.Seed, null))
                yield return node;
        }
        else if (parentNodePath.Last.StartsWith(DeployItemTypeCatPrefix, StringComparison.Ordinal))
        {
            var categoryPath = parentNodePath.Last[DeployItemTypeCatPrefix.Length..].Replace(NodeIdCategorySeparator, '/');
            foreach (var node in GetItemTypeCategoryNodes(DeploymentMode.Deploy, categoryPath))
                yield return node;
        }
        else if (parentNodePath.Last.StartsWith(SeedItemTypeCatPrefix, StringComparison.Ordinal))
        {
            var categoryPath = parentNodePath.Last[SeedItemTypeCatPrefix.Length..].Replace(NodeIdCategorySeparator, '/');
            foreach (var node in GetItemTypeCategoryNodes(DeploymentMode.Seed, categoryPath))
                yield return node;
        }
    }

    /// <summary>
    /// Emits one XML-type leaf per key in the given mode's <see cref="ModeConfig.ExcludeXmlElementsByType"/>
    /// dict. Leaf node IDs include the mode prefix so Deploy and Seed subtrees don't collide.
    /// </summary>
    private static IEnumerable<NavigationNode> GetXmlTypeNodes(DeploymentMode mode)
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null) yield break;

        SerializerConfiguration config;
        try { config = ConfigLoader.Load(configPath); }
        catch { yield break; }

        var modeConfig = config.GetMode(mode);
        var modeName = mode.ToString();
        var sort = 0;
        foreach (var typeName in modeConfig.ExcludeXmlElementsByType.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            yield return new NavigationNode
            {
                Id = $"Serializer_{modeName}_XmlType_{typeName}",
                Name = typeName,
                Icon = Icon.BracketsCurly,
                Sort = sort++,
                HasSubNodes = false,
                NodeAction = NavigateScreenAction.To<XmlTypeEditScreen>()
                    .With(new XmlTypeByNameQuery { ModelIdentifier = typeName, Mode = mode })
            };
        }
    }

    /// <summary>
    /// Emits a child predicate node for each predicate in the given mode's ModeConfig. Node IDs
    /// use the <c>Serializer_{ModeName}_Predicate_{index}</c> prefix so
    /// <see cref="PredicateNavigationNodePathProvider"/> (and the query itself) can distinguish
    /// which mode the predicate belongs to when the user drills into its edit screen.
    /// </summary>
    private static IEnumerable<NavigationNode> GetModePredicateNodes(DeploymentMode mode, string modeDisplayName)
    {
        var configPath = ConfigPathResolver.FindConfigFile();
        if (configPath == null) yield break;

        SerializerConfiguration config;
        try { config = ConfigLoader.Load(configPath); }
        catch { yield break; }

        var modeConfig = config.GetMode(mode);
        for (var i = 0; i < modeConfig.Predicates.Count; i++)
        {
            var pred = modeConfig.Predicates[i];
            yield return new NavigationNode
            {
                Id = $"Serializer_{modeDisplayName}_Predicate_{i}",
                Name = pred.Name,
                Icon = pred.ProviderType == "SqlTable" ? Icon.Table : Icon.FileAlt,
                Sort = i,
                HasSubNodes = false,
                NodeAction = NavigateScreenAction.To<PredicateEditScreen>()
                    .With(new PredicateByIndexQuery
                    {
                        ModelIdentifier = (i + 1).ToString(),
                        Mode = mode
                    })
            };
        }
    }

    /// <summary>
    /// Emits the Item Type category tree for one <see cref="DeploymentMode"/>. Same shape as the
    /// pre-37-01.1 shared tree — just scoped so Deploy and Seed have independent node IDs (no
    /// collision) and leaves open an <see cref="ItemTypeBySystemNameQuery"/> pinned to the mode.
    /// </summary>
    private static IEnumerable<NavigationNode> GetItemTypeCategoryNodes(DeploymentMode mode, string? parentCategory)
    {
        var catPrefix = mode == DeploymentMode.Deploy ? DeployItemTypeCatPrefix : SeedItemTypeCatPrefix;
        var leafPrefix = mode == DeploymentMode.Deploy ? DeployItemTypeLeafPrefix : SeedItemTypeLeafPrefix;

        List<ItemType> allTypes;
        try
        {
            var metadata = ItemManager.Metadata.GetMetadata();
            if (metadata?.Items == null || metadata.Items.Count == 0)
                yield break;
            allTypes = metadata.Items.ToList();
        }
        catch
        {
            // Graceful degradation if DW runtime not initialized
            yield break;
        }

        if (parentCategory == null)
        {
            // Top-level: group by first category segment
            var grouped = allTypes
                .GroupBy(t => GetTopLevelCategory(t.Category?.FullName))
                .OrderBy(g => g.Key == "Uncategorized" ? 1 : 0)
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            var sort = 0;
            foreach (var group in grouped)
            {
                yield return new NavigationNode
                {
                    Id = catPrefix + group.Key.Replace('/', NodeIdCategorySeparator),
                    Name = group.Key,
                    Icon = Icon.Folder,
                    Sort = sort++,
                    HasSubNodes = true,
                    NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
                        .With(new ItemTypeListQuery { Mode = mode })
                };
            }
        }
        else
        {
            // Sub-category: find types matching this category path
            var matchingTypes = allTypes
                .Where(t => (t.Category?.FullName ?? "") == parentCategory
                    || (t.Category?.FullName ?? "").StartsWith(parentCategory + "/"))
                .ToList();

            // Find distinct next-level sub-categories
            var subCategories = matchingTypes
                .Where(t => (t.Category?.FullName ?? "") != parentCategory
                    && (t.Category?.FullName ?? "").StartsWith(parentCategory + "/"))
                .Select(t => GetNextSegment(t.Category?.FullName ?? "", parentCategory))
                .Where(s => s != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sort = 0;

            // Emit sub-category folders
            foreach (var subCat in subCategories)
            {
                var fullPath = parentCategory + "/" + subCat;
                yield return new NavigationNode
                {
                    Id = catPrefix + fullPath.Replace('/', NodeIdCategorySeparator),
                    Name = subCat!,
                    Icon = Icon.Folder,
                    Sort = sort++,
                    HasSubNodes = true,
                    NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
                        .With(new ItemTypeListQuery { Mode = mode })
                };
            }

            // Emit leaf item type nodes for types whose category exactly matches
            var leafTypes = matchingTypes
                .Where(t => (t.Category?.FullName ?? "") == parentCategory)
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var itemType in leafTypes)
            {
                var icon = itemType.Icon != KnownIcon.None
                    ? IconMapper.KnownIconToIcon(itemType.Icon)
                    : Icon.FileAlt;

                yield return new NavigationNode
                {
                    Id = leafPrefix + itemType.SystemName,
                    Name = itemType.Name,
                    Icon = icon,
                    Sort = sort++,
                    HasSubNodes = false,
                    NodeAction = NavigateScreenAction.To<ItemTypeEditScreen>()
                        .With(new ItemTypeBySystemNameQuery { ModelIdentifier = itemType.SystemName, Mode = mode })
                };
            }
        }
    }

    private static string GetTopLevelCategory(string? fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return "Uncategorized";

        var slashIndex = fullName.IndexOf('/');
        return slashIndex > 0 ? fullName[..slashIndex] : fullName;
    }

    private static string? GetNextSegment(string fullPath, string parentPath)
    {
        if (!fullPath.StartsWith(parentPath + "/"))
            return null;

        var remainder = fullPath[(parentPath.Length + 1)..];
        var slashIndex = remainder.IndexOf('/');
        return slashIndex > 0 ? remainder[..slashIndex] : remainder;
    }
}

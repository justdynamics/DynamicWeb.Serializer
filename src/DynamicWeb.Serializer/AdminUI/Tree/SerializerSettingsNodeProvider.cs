using Dynamicweb.Application.UI;
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
    // The Database root node ID under Settings > System > Database
    private const string DatabaseRootId = "Settings_Database";
    internal const string SerializeNodeId = "Serializer_Settings";
    internal const string PredicatesNodeId = "Serializer_Predicates";
    internal const string EmbeddedXmlNodeId = "Serializer_EmbeddedXml";
    internal const string ItemTypesNodeId = "Serializer_ItemTypes";
    internal const string LogViewerNodeId = "Serializer_LogViewer";

    private const string ItemTypeCatPrefix = "Serializer_ItemType_Cat_";
    private const string ItemTypeLeafPrefix = "Serializer_ItemType_";
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
        if (parentNodePath.Last == DatabaseRootId)
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
            yield return new NavigationNode
            {
                Id = PredicatesNodeId,
                Name = "Predicates",
                Icon = Icon.Filter,
                Sort = 10,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<PredicateListScreen>()
                    .With(new PredicateListQuery())
            };

            yield return new NavigationNode
            {
                Id = ItemTypesNodeId,
                Name = "Item Types",
                Icon = Icon.ListUl,
                Sort = 12,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
                    .With(new ItemTypeListQuery())
            };

            yield return new NavigationNode
            {
                Id = EmbeddedXmlNodeId,
                Name = "Embedded XML",
                Icon = Icon.BracketsCurly,
                Sort = 15,
                HasSubNodes = true,
                NodeAction = NavigateScreenAction.To<XmlTypeListScreen>()
                    .With(new XmlTypeListQuery())
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
        else if (parentNodePath.Last == PredicatesNodeId)
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath != null)
            {
                SerializerConfiguration config;
                try { config = ConfigLoader.Load(configPath); }
                catch { yield break; }

                for (var i = 0; i < config.Predicates.Count; i++)
                {
                    var pred = config.Predicates[i];
                    yield return new NavigationNode
                    {
                        Id = $"Serializer_Predicate_{i}",
                        Name = pred.Name,
                        Icon = pred.ProviderType == "SqlTable" ? Icon.Table : Icon.FileAlt,
                        Sort = i,
                        HasSubNodes = false,
                        NodeAction = NavigateScreenAction.To<PredicateEditScreen>()
                            .With(new PredicateByIndexQuery { ModelIdentifier = (i + 1).ToString() })
                    };
                }
            }
        }
        else if (parentNodePath.Last == EmbeddedXmlNodeId)
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath != null)
            {
                SerializerConfiguration config;
                try { config = ConfigLoader.Load(configPath); }
                catch { yield break; }

                var sort = 0;
                foreach (var typeName in config.ExcludeXmlElementsByType.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    yield return new NavigationNode
                    {
                        Id = $"Serializer_XmlType_{typeName}",
                        Name = typeName,
                        Icon = Icon.BracketsCurly,
                        Sort = sort++,
                        HasSubNodes = false,
                        NodeAction = NavigateScreenAction.To<XmlTypeEditScreen>()
                            .With(new XmlTypeByNameQuery { ModelIdentifier = typeName })
                    };
                }
            }
        }
        else if (parentNodePath.Last == ItemTypesNodeId)
        {
            foreach (var node in GetItemTypeCategoryNodes(null))
                yield return node;
        }
        else if (parentNodePath.Last.StartsWith(ItemTypeCatPrefix, StringComparison.Ordinal))
        {
            // Convert node ID separator (~) back to category separator (/)
            var categoryPath = parentNodePath.Last[ItemTypeCatPrefix.Length..].Replace(NodeIdCategorySeparator, '/');
            foreach (var node in GetItemTypeCategoryNodes(categoryPath))
                yield return node;
        }
    }

    private static IEnumerable<NavigationNode> GetItemTypeCategoryNodes(string? parentCategory)
    {
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
                    Id = ItemTypeCatPrefix + group.Key.Replace('/', NodeIdCategorySeparator),
                    Name = group.Key,
                    Icon = Icon.Folder,
                    Sort = sort++,
                    HasSubNodes = true,
                    NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
                        .With(new ItemTypeListQuery())
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
                    Id = ItemTypeCatPrefix + fullPath.Replace('/', NodeIdCategorySeparator),
                    Name = subCat!,
                    Icon = Icon.Folder,
                    Sort = sort++,
                    HasSubNodes = true,
                    NodeAction = NavigateScreenAction.To<ItemTypeListScreen>()
                        .With(new ItemTypeListQuery())
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
                    Id = ItemTypeLeafPrefix + itemType.SystemName,
                    Name = itemType.Name,
                    Icon = icon,
                    Sort = sort++,
                    HasSubNodes = false,
                    NodeAction = NavigateScreenAction.To<ItemTypeEditScreen>()
                        .With(new ItemTypeBySystemNameQuery { ModelIdentifier = itemType.SystemName })
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

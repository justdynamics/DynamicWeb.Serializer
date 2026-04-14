using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Queries;
using DynamicWeb.Serializer.AdminUI.Screens;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Navigation;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class SerializerSettingsNodeProvider : NavigationNodeProvider<SystemSection>
{
    // The Database root node ID under Settings > System > Database
    private const string DatabaseRootId = "Settings_Database";
    internal const string SerializeNodeId = "Serializer_Settings";
    internal const string PredicatesNodeId = "Serializer_Predicates";
    internal const string EmbeddedXmlNodeId = "Serializer_EmbeddedXml";
    internal const string LogViewerNodeId = "Serializer_LogViewer";

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
                var config = ConfigLoader.Load(configPath);
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
                var config = ConfigLoader.Load(configPath);
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
    }
}

using Dynamicweb.Application.UI;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Navigation;

namespace DynamicWeb.Serializer.AdminUI.Tree;

public sealed class LogViewerNavigationNodePathProvider : NavigationNodePathProvider<LogViewerModel>
{
    public LogViewerNavigationNodePathProvider()
    {
        AllowNullModel = true;
    }

    protected override NavigationNodePath GetNavigationNodePathInternal(LogViewerModel? model) =>
        new([
            typeof(SettingsArea).FullName,
            NavigationContext.Empty,
            typeof(SystemSection).FullName,
            SerializerSettingsNodeProvider.DeveloperRootId,
            SerializerSettingsNodeProvider.SerializeNodeId,
            SerializerSettingsNodeProvider.LogViewerNodeId
        ]);
}

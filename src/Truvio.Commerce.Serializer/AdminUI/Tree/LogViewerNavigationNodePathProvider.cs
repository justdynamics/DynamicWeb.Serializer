using Dynamicweb.Application.UI;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Navigation;

namespace Truvio.Commerce.Serializer.AdminUI.Tree;

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

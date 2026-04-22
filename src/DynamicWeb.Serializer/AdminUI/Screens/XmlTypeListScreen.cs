using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Queries;
using DynamicWeb.Serializer.Configuration;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Lists;
using Dynamicweb.CoreUI.Lists.ViewMappings;
using Dynamicweb.CoreUI.Screens;

namespace DynamicWeb.Serializer.AdminUI.Screens;

public sealed class XmlTypeListScreen : ListScreenBase<XmlTypeListModel>
{
    protected override string GetScreenName() => "Embedded XML Types";

    // Read the mode off the query so Scan and row-click actions stay in the current subtree.
    // Falls back to Deploy when Query is null (defensive — the framework should always inject it).
    private DeploymentMode CurrentMode => (Query as XmlTypeListQuery)?.Mode ?? DeploymentMode.Deploy;

    protected override IEnumerable<ListViewMapping> GetViewMappings() =>
    [
        new RowViewMapping
        {
            Columns =
            [
                CreateMapping(m => m.TypeName),
                CreateMapping(m => m.ExcludedElementCount)
            ]
        }
    ];

    protected override ActionBase GetListItemPrimaryAction(XmlTypeListModel model) =>
        NavigateScreenAction.To<XmlTypeEditScreen>()
            .With(new XmlTypeByNameQuery { ModelIdentifier = model.TypeName, Mode = model.Mode });

    protected override IEnumerable<ActionGroup>? GetScreenActions() =>
    [
        new()
        {
            Name = "Actions",
            Nodes =
            [
                new()
                {
                    Name = "Scan for XML types",
                    Icon = Icon.Refresh,
                    NodeAction = RunCommandAction.For(new ScanXmlTypesCommand { Mode = CurrentMode })
                        .WithReloadOnSuccess()
                }
            ]
        }
    ];

    protected override ActionNode? GetItemCreateAction() =>
        new()
        {
            Name = "Scan for XML types",
            Icon = Icon.Refresh,
            NodeAction = RunCommandAction.For(new ScanXmlTypesCommand { Mode = CurrentMode })
                .WithReloadOnSuccess()
        };
}

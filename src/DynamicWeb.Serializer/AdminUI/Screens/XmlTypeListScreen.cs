using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Queries;
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
            .With(new XmlTypeByNameQuery { ModelIdentifier = model.TypeName });

    protected override IEnumerable<ActionNode>? GetToolbarActions() =>
    [
        new()
        {
            Name = "Scan for XML types",
            Icon = Icon.Refresh,
            NodeAction = RunCommandAction.For<ScanXmlTypesCommand>()
                .WithReloadOnSuccess()
        }
    ];
}

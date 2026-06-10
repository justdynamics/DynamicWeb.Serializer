using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.AdminUI.Queries;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Lists;
using Dynamicweb.CoreUI.Lists.ViewMappings;
using Dynamicweb.CoreUI.Screens;

namespace Truvio.Commerce.Serializer.AdminUI.Screens;

public sealed class XmlTypeListScreen : ListScreenBase<XmlTypeListModel>
{
    protected override string GetScreenName() => "Embedded XML Excludes";

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
                    NodeAction = RunCommandAction.For(new ScanXmlTypesCommand())
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
            NodeAction = RunCommandAction.For(new ScanXmlTypesCommand())
                .WithReloadOnSuccess()
        };
}

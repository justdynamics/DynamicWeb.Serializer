using Dynamicweb.Application.UI.Helpers;
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

public sealed class PredicateListScreen : ListScreenBase<PredicateListModel>
{
    protected override string GetScreenName() => "Serializer Predicates";

    protected override IEnumerable<ListViewMapping> GetViewMappings() =>
    [
        new RowViewMapping
        {
            Columns =
            [
                CreateMapping(m => m.ModeDisplay),  // Phase 40 D-06: mode badge
                CreateMapping(m => m.Name),
                CreateMapping(m => m.Type),
                CreateMapping(m => m.Target)
            ]
        }
    ];

    protected override ActionBase GetListItemPrimaryAction(PredicateListModel model) =>
        NavigateScreenAction.To<PredicateEditScreen>()
            .With(new PredicateByIndexQuery { ModelIdentifier = (model.Index + 1).ToString() });

    protected override IEnumerable<ActionGroup>? GetListItemContextActions(PredicateListModel model) =>
    [
        new()
        {
            Nodes =
            [
                ActionBuilder.Edit<PredicateEditScreen>(new PredicateByIndexQuery
                    { ModelIdentifier = (model.Index + 1).ToString() }),
                ActionBuilder.Delete(
                    new DeletePredicateCommand { Index = model.Index },
                    "Delete predicate?",
                    $"Are you sure you want to delete predicate '{model.Name}'?")
            ]
        }
    ];

    protected override ActionNode GetItemCreateAction() =>
        new()
        {
            Name = "New predicate",
            Icon = Icon.PlusSquare,
            NodeAction = NavigateScreenAction.To<PredicateEditScreen>()
        };
}

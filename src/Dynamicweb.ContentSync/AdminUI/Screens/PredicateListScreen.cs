using Dynamicweb.Application.UI.Helpers;
using Dynamicweb.ContentSync.AdminUI.Commands;
using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.ContentSync.AdminUI.Queries;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Lists;
using Dynamicweb.CoreUI.Lists.ViewMappings;
using Dynamicweb.CoreUI.Screens;

namespace Dynamicweb.ContentSync.AdminUI.Screens;

public sealed class PredicateListScreen : ListScreenBase<PredicateListModel>
{
    protected override string GetScreenName() => "Content Sync Predicates";

    protected override IEnumerable<ListViewMapping> GetViewMappings() =>
    [
        new RowViewMapping
        {
            Columns =
            [
                CreateMapping(m => m.Name),
                CreateMapping(m => m.Path),
                CreateMapping(m => m.AreaName)
            ]
        }
    ];

    protected override ActionBase GetListItemPrimaryAction(PredicateListModel model) =>
        NavigateScreenAction.To<PredicateEditScreen>()
            .With(new PredicateByIndexQuery { Index = model.Index });

    protected override IEnumerable<ActionGroup>? GetListItemContextActions(PredicateListModel model) =>
    [
        new()
        {
            Nodes =
            [
                ActionBuilder.Edit<PredicateEditScreen>(new PredicateByIndexQuery { Index = model.Index }),
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

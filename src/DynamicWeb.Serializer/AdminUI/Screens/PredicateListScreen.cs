using Dynamicweb.Application.UI.Helpers;
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

public sealed class PredicateListScreen : ListScreenBase<PredicateListModel>
{
    /// <summary>
    /// Title reflects the mode the screen was opened for (Phase 37-01 D-02). Falls back to the
    /// generic name when no row data is loaded yet — the tree query will immediately rehydrate.
    /// </summary>
    protected override string GetScreenName()
    {
        // The List screen receives model rows via the query; any row carries the Mode back.
        // When the list is empty (fresh config) we still want a mode-specific title so fall back
        // to Deploy — the tree entry points never navigate here without a mode set on the query.
        return "Serializer Predicates";
    }

    protected override IEnumerable<ListViewMapping> GetViewMappings() =>
    [
        new RowViewMapping
        {
            Columns =
            [
                CreateMapping(m => m.Name),
                CreateMapping(m => m.Type),
                CreateMapping(m => m.Target)
            ]
        }
    ];

    protected override ActionBase GetListItemPrimaryAction(PredicateListModel model) =>
        NavigateScreenAction.To<PredicateEditScreen>()
            .With(new PredicateByIndexQuery
            {
                ModelIdentifier = (model.Index + 1).ToString(),
                Mode = model.Mode
            });

    protected override IEnumerable<ActionGroup>? GetListItemContextActions(PredicateListModel model) =>
    [
        new()
        {
            Nodes =
            [
                ActionBuilder.Edit<PredicateEditScreen>(new PredicateByIndexQuery
                {
                    ModelIdentifier = (model.Index + 1).ToString(),
                    Mode = model.Mode
                }),
                ActionBuilder.Delete(
                    new DeletePredicateCommand { Index = model.Index, Mode = model.Mode },
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

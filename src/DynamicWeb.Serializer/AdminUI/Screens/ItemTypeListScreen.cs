using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Queries;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Lists;
using Dynamicweb.CoreUI.Lists.ViewMappings;
using Dynamicweb.CoreUI.Screens;

namespace DynamicWeb.Serializer.AdminUI.Screens;

public sealed class ItemTypeListScreen : ListScreenBase<ItemTypeListModel>
{
    protected override string GetScreenName() => "Item Types";

    protected override IEnumerable<ListViewMapping> GetViewMappings() =>
    [
        new RowViewMapping
        {
            Columns =
            [
                CreateMapping(m => m.SystemName),
                CreateMapping(m => m.DisplayName),
                CreateMapping(m => m.Category),
                CreateMapping(m => m.FieldCount),
                CreateMapping(m => m.ExcludedFieldCount)
            ]
        }
    ];

    protected override ActionBase GetListItemPrimaryAction(ItemTypeListModel model) =>
        NavigateScreenAction.To<ItemTypeEditScreen>()
            .With(new ItemTypeBySystemNameQuery { ModelIdentifier = model.SystemName });
}

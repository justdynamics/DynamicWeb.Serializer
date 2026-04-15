using DynamicWeb.Serializer.AdminUI.Models;
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
}

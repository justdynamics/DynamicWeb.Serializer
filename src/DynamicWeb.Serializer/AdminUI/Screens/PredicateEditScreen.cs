using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Inputs;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Editors.Selectors;
using Dynamicweb.CoreUI.Screens;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

namespace DynamicWeb.Serializer.AdminUI.Screens;

public sealed class PredicateEditScreen : EditScreenBase<PredicateEditModel>
{
    protected override void BuildEditScreen()
    {
        // Shared fields always visible
        var sharedFields = new List<EditorBase>
        {
            EditorFor(m => m.Name),
            EditorFor(m => m.ProviderType)
        };

        var groups = new List<LayoutWrapper>
        {
            new("Configuration", sharedFields)
        };

        // Per D-09: only show provider-specific fields when ProviderType is selected
        if (Model?.ProviderType == "Content")
        {
            groups.Add(new("Content Settings", new List<EditorBase>
            {
                EditorFor(m => m.AreaId),
                EditorFor(m => m.PageId),
                EditorFor(m => m.Excludes)
            }));
        }
        else if (Model?.ProviderType == "SqlTable")
        {
            groups.Add(new("SQL Table Settings", new List<EditorBase>
            {
                EditorFor(m => m.Table),
                EditorFor(m => m.NameColumn),
                EditorFor(m => m.CompareColumns),
                EditorFor(m => m.ServiceCaches)
            }));
        }
        // else: no ProviderType selected — show nothing below Configuration (D-09)

        AddComponents("Predicate", groups);
    }

    protected override EditorBase? GetEditor(string property) => property switch
    {
        nameof(PredicateEditModel.ProviderType) => CreateProviderTypeSelect(),
        nameof(PredicateEditModel.AreaId) => SelectorBuilder.CreateAreaSelector(
            value: Model?.AreaId > 0 ? Model.AreaId : null,
            hideDeactivated: true
        ).WithReloadOnChange(),
        nameof(PredicateEditModel.PageId) => SelectorBuilder.CreatePageSelector(
            value: Model?.PageId > 0 ? Model.PageId : null,
            areaId: Model?.AreaId > 0 ? Model.AreaId : null,
            hint: "Select root page for this predicate"
        ),
        nameof(PredicateEditModel.Excludes) => new Textarea
        {
            Label = "Excludes",
            Explanation = "One path per line. Pages under these paths will be excluded from sync."
        },
        nameof(PredicateEditModel.ServiceCaches) => new Textarea
        {
            Label = "Service Caches",
            Explanation = "One fully-qualified DW cache type per line. Cleared after deserialization."
        },
        _ => null
    };

    private Select CreateProviderTypeSelect()
    {
        var select = new Select
        {
            SortOrder = OrderBy.Default,
            Options = new List<ListOption>
            {
                new() { Value = "Content", Label = "Content" },
                new() { Value = "SqlTable", Label = "SQL Table" }
            }
        };

        // D-02: ProviderType locked after creation — only reload on change for new predicates
        if (Model?.Index < 0)
            return select.WithReloadOnChange();

        // For existing predicates, show current value but don't trigger reload
        // (SavePredicateCommand preserves original ProviderType on updates)
        return select;
    }

    protected override string GetScreenName() =>
        Model?.Index >= 0 ? $"Edit Predicate: {Model.Name}" : "New Predicate";

    protected override CommandBase<PredicateEditModel> GetSaveCommand() => new SavePredicateCommand();
}

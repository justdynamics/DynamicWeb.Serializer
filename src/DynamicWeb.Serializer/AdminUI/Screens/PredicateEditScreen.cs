using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
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

            groups.Add(new("Area Column Filtering", new List<EditorBase>
            {
                EditorFor(m => m.ExcludeAreaColumns)
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
        nameof(PredicateEditModel.ExcludeAreaColumns) => CreateAreaColumnSelectMultiDual(
            Model?.AreaId, Model?.ExcludeAreaColumns,
            "Exclude Area Columns", "Select area table columns to exclude from serialization."),
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

    private SelectMultiDual CreateAreaColumnSelectMultiDual(int? areaId, string? currentValue, string label, string explanation)
    {
        var editor = new SelectMultiDual
        {
            Label = label,
            Explanation = explanation,
            SortOrder = OrderBy.Default
        };

        if (areaId is null or <= 0)
        {
            editor.Explanation = "Select an area to see available columns.";
            return editor;
        }

        try
        {
            var metadataReader = new DataGroupMetadataReader(new DwSqlExecutor());
            var columnTypes = metadataReader.GetColumnTypes("Area");

            if (columnTypes.Count == 0)
            {
                editor.Explanation = "Area table not found in database.";
                return editor;
            }

            // Filter columns already captured by named DTO properties (per RESEARCH pitfall 4)
            var dtoColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AreaID", "AreaName", "AreaSort", "AreaItemType", "AreaItemId", "AreaUniqueId"
            };

            editor.Options = columnTypes.Keys
                .Where(c => !dtoColumns.Contains(c))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ListOption { Value = c, Label = c })
                .ToList();

            var selected = (currentValue ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToArray();

            if (selected.Length > 0)
                editor.Value = selected;
        }
        catch (Exception ex)
        {
            editor.Explanation = $"Could not query area columns: {ex.Message}";
        }

        return editor;
    }

    protected override string GetScreenName() =>
        Model?.Index >= 0 ? $"Edit Predicate: {Model.Name}" : "New Predicate";

    protected override CommandBase<PredicateEditModel> GetSaveCommand() => new SavePredicateCommand();
}

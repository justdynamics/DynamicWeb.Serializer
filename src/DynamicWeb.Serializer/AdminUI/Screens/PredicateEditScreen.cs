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
            groups.Add(new("Filtering", new List<EditorBase>
            {
                EditorFor(m => m.ExcludeFields),
                EditorFor(m => m.ExcludeXmlElements)
            }));
        }
        else if (Model?.ProviderType == "SqlTable")
        {
            groups.Add(new("SQL Table Settings", new List<EditorBase>
            {
                EditorFor(m => m.Table).WithReloadOnChange(),
                EditorFor(m => m.NameColumn),
                EditorFor(m => m.CompareColumns),
                EditorFor(m => m.ServiceCaches)
            }));
            groups.Add(new("Filtering", new List<EditorBase>
            {
                EditorFor(m => m.XmlColumns),
                EditorFor(m => m.ExcludeFields),
                EditorFor(m => m.ExcludeXmlElements)
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
        nameof(PredicateEditModel.ExcludeFields) => Model?.ProviderType == "SqlTable"
            ? CreateColumnCheckboxList(Model?.Table, Model?.ExcludeFields,
                "Exclude Fields", "Select columns to exclude from serialization.")
            : new Textarea
            {
                Label = "Exclude Fields",
                Explanation = "One field name per line. These fields will be omitted from serialization."
            },
        nameof(PredicateEditModel.XmlColumns) => Model?.ProviderType == "SqlTable"
            ? CreateColumnCheckboxList(Model?.Table, Model?.XmlColumns,
                "XML Columns", "Select columns containing XML to pretty-print in YAML.")
            : new Textarea
            {
                Label = "XML Columns",
                Explanation = "One column name per line. SQL table columns containing XML to pretty-print in YAML."
            },
        nameof(PredicateEditModel.ExcludeXmlElements) => new Textarea
        {
            Label = "Exclude XML Elements",
            Explanation = "One element name per line. These XML elements will be stripped from embedded XML blobs."
        },
        _ => null
    };

    private CheckboxList CreateColumnCheckboxList(string? tableName, string? currentValue, string label, string explanation)
    {
        var editor = new CheckboxList
        {
            Label = label,
            Explanation = explanation,
            SortOrder = OrderBy.Default
        };

        if (string.IsNullOrWhiteSpace(tableName))
        {
            editor.Explanation = "Enter a table name to see available columns.";
            return editor;
        }

        // Validate table name to prevent SQL injection via INFORMATION_SCHEMA queries
        if (!System.Text.RegularExpressions.Regex.IsMatch(tableName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            editor.Explanation = "Invalid table name format.";
            return editor;
        }

        try
        {
            var metadataReader = new DataGroupMetadataReader(new DwSqlExecutor());
            var columnTypes = metadataReader.GetColumnTypes(tableName);

            if (columnTypes.Count == 0)
            {
                editor.Explanation = "Table not found in database. Verify the table name.";
                return editor;
            }

            editor.Options = columnTypes.Keys
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ListOption { Value = c, Label = c })
                .ToList();

            // Pre-check existing values
            var selected = (currentValue ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToList();

            if (selected.Count > 0)
                editor.Value = selected;
        }
        catch (Exception ex)
        {
            editor.Explanation = $"Could not query database columns: {ex.Message}";
        }

        return editor;
    }

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

using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.Content.Items;
using Dynamicweb.Content.Items.Metadata;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

namespace DynamicWeb.Serializer.AdminUI.Screens;

public sealed class ItemTypeEditScreen : EditScreenBase<ItemTypeEditModel>
{
    protected override void BuildEditScreen()
    {
        if (Model is null)
        {
            AddComponents("Error", new List<LayoutWrapper>
            {
                new("Not Found", new List<EditorBase>
                {
                    new Dynamicweb.CoreUI.Editors.Inputs.Text
                    {
                        Label = "Item type not found",
                        Explanation = "This item type no longer exists. It may have been removed from the solution.",
                        Readonly = true
                    }
                })
            });
            return;
        }

        // Section 1: Read-only item type metadata
        var infoFields = new List<EditorBase>
        {
            EditorFor(m => m.SystemName),
            EditorFor(m => m.DisplayName),
            EditorFor(m => m.Category),
            new Dynamicweb.CoreUI.Editors.Inputs.Text
            {
                Label = "Total Fields",
                Value = Model.FieldCount.ToString(),
                Readonly = true
            }
        };

        // Section 2: Field exclusion selector
        var exclusionFields = new List<EditorBase>
        {
            EditorFor(m => m.ExcludedFields)
        };

        AddComponents("Item Type Configuration", new List<LayoutWrapper>
        {
            new("Item Type Information", infoFields),
            new("Field Exclusions", exclusionFields)
        });
    }

    protected override EditorBase? GetEditor(string property) => property switch
    {
        nameof(ItemTypeEditModel.SystemName) => new Dynamicweb.CoreUI.Editors.Inputs.Text
        {
            Label = "System Name",
            Readonly = true
        },
        nameof(ItemTypeEditModel.DisplayName) => new Dynamicweb.CoreUI.Editors.Inputs.Text
        {
            Label = "Display Name",
            Readonly = true
        },
        nameof(ItemTypeEditModel.Category) => new Dynamicweb.CoreUI.Editors.Inputs.Text
        {
            Label = "Category",
            Readonly = true
        },
        nameof(ItemTypeEditModel.ExcludedFields) => CreateFieldSelector(),
        _ => null
    };

    private SelectMultiDual CreateFieldSelector()
    {
        var editor = new SelectMultiDual
        {
            Label = "Exclude Fields",
            Explanation = "Select fields to exclude from serialization for this item type.",
            SortOrder = OrderBy.Default
        };

        if (string.IsNullOrWhiteSpace(Model?.SystemName))
            return editor;

        try
        {
            var itemType = ItemManager.Metadata.GetItemType(Model.SystemName);
            if (itemType == null)
            {
                editor.Explanation = "Item type not found. Fields cannot be loaded.";
                return editor;
            }

            // Use GetItemFields to include inherited fields (not itemType.Fields directly)
            var allFields = ItemManager.Metadata.GetItemFields(itemType);

            editor.Options = allFields
                .Where(f => !string.IsNullOrEmpty(f.SystemName))
                .OrderBy(f => f.SystemName, StringComparer.OrdinalIgnoreCase)
                .Select(f => new ListOption { Value = f.SystemName, Label = $"{f.Name} ({f.SystemName})" })
                .ToList();

            // Pre-select currently excluded fields
            var selected = (Model.ExcludedFields ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToArray();

            if (selected.Length > 0)
                editor.Value = selected;
        }
        catch (Exception ex)
        {
            editor.Explanation = $"Could not load fields: {ex.Message}";
        }

        return editor;
    }

    protected override string GetScreenName() =>
        !string.IsNullOrWhiteSpace(Model?.SystemName) ? $"Item Type: {Model.SystemName}" : "Item Type";

    protected override CommandBase<ItemTypeEditModel> GetSaveCommand() => new SaveItemTypeCommand();
}

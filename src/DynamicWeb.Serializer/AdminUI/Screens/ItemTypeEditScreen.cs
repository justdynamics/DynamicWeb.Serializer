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

        // Union of (a) live ItemManager.Metadata fields and (b) saved exclusions, so saved
        // values always render even when live metadata is empty. Value is bound by
        // EditScreenBase.BuildEditor from Model.ExcludedFields (List<string>) after this returns.
        var allFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Track per-field display labels separately so the live-discovered set keeps its
        // "{Name} ({SystemName})" format while saved-only entries fall back to the system name.
        var fieldLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var itemType = ItemManager.Metadata.GetItemType(Model.SystemName);
            if (itemType != null)
            {
                var liveFields = ItemManager.Metadata.GetItemFields(itemType);
                foreach (var f in liveFields)
                {
                    if (string.IsNullOrEmpty(f.SystemName))
                        continue;
                    allFields.Add(f.SystemName);
                    fieldLabels[f.SystemName] = $"{f.Name} ({f.SystemName})";
                }
            }
        }
        catch (Exception ex)
        {
            editor.Explanation = $"Could not load fields from live metadata: {ex.Message}";
        }

        foreach (var s in Model.ExcludedFields ?? new())
        {
            if (string.IsNullOrWhiteSpace(s))
                continue;
            var trimmed = s.Trim();
            allFields.Add(trimmed);
            if (!fieldLabels.ContainsKey(trimmed))
                fieldLabels[trimmed] = trimmed;
        }

        if (allFields.Count == 0)
        {
            editor.Explanation = "Item type not found in metadata and no saved exclusions yet.";
            return editor;
        }

        editor.Options = allFields
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new ListOption { Value = f, Label = fieldLabels[f] })
            .ToList();

        return editor;
    }

    protected override string GetScreenName() =>
        // Phase 41 D-01: page manages field exclusions for the item type, not the item type itself.
        !string.IsNullOrWhiteSpace(Model?.SystemName) ? $"Item Type Excludes - {Model.SystemName}" : "Item Type Excludes";

    protected override CommandBase<ItemTypeEditModel> GetSaveCommand() =>
        // Phase 40 D-04: top-level exclusion dict -- no per-mode routing on save.
        new SaveItemTypeCommand();
}

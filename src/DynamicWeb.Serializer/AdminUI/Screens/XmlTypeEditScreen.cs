using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Infrastructure;
using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

namespace DynamicWeb.Serializer.AdminUI.Screens;

public sealed class XmlTypeEditScreen : EditScreenBase<XmlTypeEditModel>
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
                        Label = "Type not found",
                        Explanation = "This XML type no longer exists in configuration. Run 'Scan for XML types' to refresh.",
                        Readonly = true
                    }
                })
            });
            return;
        }

        var fields = new List<EditorBase>();

        // Type name as read-only label (not editable -- the type is the key)
        fields.Add(EditorFor(m => m.TypeName));

        // Element exclusion selector
        fields.Add(EditorFor(m => m.ExcludedElements));

        AddComponents("XML Type Configuration", new List<LayoutWrapper>
        {
            new("Element Exclusions", fields)
        });

        // Show a raw XML sample so the user understands the structure
        if (!string.IsNullOrWhiteSpace(Model?.TypeName))
        {
            try
            {
                var discovery = new XmlTypeDiscovery(new DwSqlExecutor());
                var sample = discovery.GetSampleXml(Model.TypeName);
                if (!string.IsNullOrWhiteSpace(sample))
                {
                    AddComponents("Reference", new List<LayoutWrapper>
                    {
                        new("XML Sample", new List<EditorBase>
                        {
                            new Dynamicweb.CoreUI.Editors.Inputs.Textarea
                            {
                                Label = "Sample XML from database",
                                Explanation = "This is a sample of the raw XML found in the database for this type. The element or parameter names shown in the exclusion list above correspond to the structure you see here.",
                                Value = sample,
                                Readonly = true
                            }
                        })
                    });
                }
            }
            catch
            {
                // Non-critical — skip sample if it fails
            }
        }
    }

    protected override EditorBase? GetEditor(string property) => property switch
    {
        nameof(XmlTypeEditModel.TypeName) => new Dynamicweb.CoreUI.Editors.Inputs.Text
        {
            Label = "XML Type",
            Explanation = "The module system name or URL data provider type.",
            Readonly = true
        },
        nameof(XmlTypeEditModel.ExcludedElements) => CreateElementSelector(),
        _ => null
    };

    private SelectMultiDual CreateElementSelector()
    {
        var editor = new SelectMultiDual
        {
            Label = "Exclude Elements",
            Explanation = "Select XML elements to exclude from serialization for this type.",
            SortOrder = OrderBy.Default
        };

        if (string.IsNullOrWhiteSpace(Model?.TypeName))
            return editor;

        try
        {
            // Discover all elements for this type from live DB XML (per D-07)
            var discovery = new XmlTypeDiscovery(new DwSqlExecutor());
            var allElements = discovery.DiscoverElementsForType(Model.TypeName);

            if (allElements.Count == 0)
            {
                editor.Explanation = "No XML data found in database for this type. Elements will appear after data is available.";
                return editor;
            }

            editor.Options = allElements
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .Select(e => new ListOption { Value = e, Label = e })
                .ToList();

            // Pre-select currently excluded elements (per ScreenPresetEditScreen.cs pattern: use .ToArray())
            var selected = (Model.ExcludedElements ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .ToArray();

            if (selected.Length > 0)
                editor.Value = selected;
        }
        catch (Exception ex)
        {
            editor.Explanation = $"Could not discover elements: {ex.Message}";
        }

        return editor;
    }

    protected override string GetScreenName() =>
        !string.IsNullOrWhiteSpace(Model?.TypeName) ? $"XML Type: {Model.TypeName}" : "XML Type";

    protected override CommandBase<XmlTypeEditModel> GetSaveCommand() => new SaveXmlTypeCommand();
}

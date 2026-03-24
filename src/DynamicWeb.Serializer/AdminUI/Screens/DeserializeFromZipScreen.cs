using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.Content;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Inputs;
using Dynamicweb.CoreUI.Editors.Lists;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;
using Dynamicweb.CoreUI.Screens;

namespace DynamicWeb.Serializer.AdminUI.Screens;

/// <summary>
/// Full-page screen for importing content from a zip file.
/// Shows area selector with reload-on-change, then zip content preview and save/import button.
/// Uses EditScreenBase because WithReloadOnChange does not work inside DW dialogs.
/// </summary>
public sealed class DeserializeFromZipScreen : EditScreenBase<DeserializeFromZipModel>
{
    protected override string GetScreenName() => "Import to Database";

    protected override void BuildEditScreen()
    {
        var model = Model;
        if (model == null)
            return;

        // Build all sections in a single group (single tab)
        var sections = new List<LayoutWrapper>
        {
            new("Import Details",
            [
                EditorFor(m => m.FileName),
                EditorFor(m => m.TargetAreaId)
            ])
        };

        if (!string.IsNullOrEmpty(model.ValidationError))
        {
            sections.Add(new("Validation",
            [
                EditorFor(m => m.ValidationError)
            ]));
            AddComponents("Import to Database", sections);
            return;
        }

        if (!model.IsValid)
        {
            AddComponents("Import to Database", sections);
            return;
        }

        if (model.TargetAreaIdParsed <= 0)
        {
            AddComponents("Import to Database", sections);
            return;
        }

        // Extract zip and scan content after ShadowEdit sets TargetAreaId
        model.ReloadWithArea();

        if (!string.IsNullOrEmpty(model.ValidationError))
        {
            sections.Add(new("Validation",
            [
                EditorFor(m => m.ValidationError)
            ]));
        }
        else if (!string.IsNullOrEmpty(model.DryRunText))
        {
            sections.Add(new("Import Preview",
            [
                EditorFor(m => m.DryRunText)
            ]));
        }

        AddComponents("Import to Database", sections);
    }

    protected override EditorBase? GetEditor(string property)
    {
        return property switch
        {
            nameof(DeserializeFromZipModel.FileName) => new Text { Readonly = true },
            nameof(DeserializeFromZipModel.TargetAreaId) => CreateAreaSelect(),
            nameof(DeserializeFromZipModel.DryRunText) => new Textarea { Readonly = true },
            nameof(DeserializeFromZipModel.ValidationError) => new Textarea { Readonly = true },
            _ => null
        };
    }

    protected override CommandBase<DeserializeFromZipModel> GetSaveCommand()
    {
        if (Model?.IsValid != true || Model.TargetAreaIdParsed <= 0)
            return null!;

        return new DeserializeFromZipCommand { FilePath = Model.FilePath, TargetAreaId = Model.TargetAreaIdParsed };
    }

    private Select CreateAreaSelect()
    {
        var options = new List<ListOption>
        {
            new() { Value = "", Label = "-- Select area --" }
        };

        try
        {
            var areas = Services.Areas.GetAreas();
            foreach (var area in areas)
            {
                options.Add(new ListOption { Value = area.ID.ToString(), Label = area.Name });
            }
        }
        catch
        {
            // DW runtime not available
        }

        return new Select
        {
            SortOrder = OrderBy.Default,
            Options = options
        }.WithReloadOnChange();
    }
}

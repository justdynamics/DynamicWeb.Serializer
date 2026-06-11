using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.AdminUI.Queries;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Screens;

namespace Truvio.Commerce.Serializer.AdminUI.Screens;

/// <summary>
/// Read-only SlideOver behind every carve-out cue: lists exactly WHICH fields/settings stay
/// local for a type or predicate. Opened via OpenSlideOverAction so it works from every
/// context the cues live in — including the paragraph editor, which is itself a SlideOver
/// (NavigateScreenAction is suppressed inside dialogs/SlideOvers by the admin frontend).
/// No save command: content editors get the information without needing Settings access;
/// administrators additionally get a "Manage exclusions" action into the settings editor.
/// </summary>
public sealed class CarveOutDetailScreen : EditScreenBase<CarveOutDetailModel>
{
    protected override void BuildEditScreen()
    {
        var fields = new List<EditorBase?>
        {
            EditorFor(m => m.TypeName),
            EditorFor(m => m.Summary),
            EditorFor(m => m.ExcludedFields)
        };

        AddComponents("Stays local", new List<LayoutWrapper>
        {
            new("Excluded from sync", fields.Where(f => f is not null).Select(f => f!).ToList())
        });

        // SlideOver chrome doesn't render the screen Actions toolbar, so admins get the
        // manage link as an in-content button too (GetScreenActions still covers the
        // standalone render).
        if (Model?.CanManage == true && CreateManageAction(Model) is { } inlineManageAction)
        {
            AddComponent("Stays local", "Manage", new Dynamicweb.CoreUI.Layout.ButtonGroup
            {
                Buttons =
                {
                    new Dynamicweb.CoreUI.Actions.Button
                    {
                        Name = "Manage exclusions",
                        Icon = Icon.Cog,
                        NodeAction = inlineManageAction
                    }
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(Model?.SampleXml))
        {
            AddComponents("Reference", new List<LayoutWrapper>
            {
                new("XML Sample", new List<EditorBase>
                {
                    new Dynamicweb.CoreUI.Editors.Inputs.Textarea
                    {
                        Label = "Sample XML from database",
                        Explanation = "Raw XML for this type — the excluded element names above appear in this structure.",
                        Value = Model.SampleXml,
                        Readonly = true,
                        Rows = 20
                    }
                })
            });
        }
    }

    protected override EditorBase? GetEditor(string property) => property switch
    {
        nameof(CarveOutDetailModel.TypeName) => new Dynamicweb.CoreUI.Editors.Inputs.Text { Readonly = true },
        nameof(CarveOutDetailModel.Summary) => new Dynamicweb.CoreUI.Editors.Inputs.Textarea { Readonly = true, Rows = 3 },
        nameof(CarveOutDetailModel.ExcludedFields) => new Dynamicweb.CoreUI.Editors.Inputs.Textarea
        {
            Label = "Excluded fields",
            Explanation = "These values are never written by a sync — each environment keeps its own.",
            Readonly = true,
            Rows = 14
        },
        _ => null
    };

    protected override IEnumerable<ActionGroup>? GetScreenActions()
    {
        if (Model is null || !Model.CanManage)
            return null;

        var manageAction = CreateManageAction(Model);
        if (manageAction is null)
            return null;

        return new[]
        {
            new ActionGroup
            {
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Manage exclusions",
                        Icon = Icon.Cog,
                        NodeAction = manageAction
                    }
                }
            }
        };
    }

    /// <summary>Admin-only deep link to the settings editor that owns this exclusion list.
    /// Opens as a SlideOver too — navigation actions are unreliable from SlideOver context.</summary>
    private static ActionBase? CreateManageAction(CarveOutDetailModel model) => model.Kind switch
    {
        CarveOutDetailModel.KindXmlElements => OpenSlideOverAction.To<XmlTypeEditScreen>()
            .With(new XmlTypeByNameQuery { ModelIdentifier = model.TypeName }),
        CarveOutDetailModel.KindItemTypeFields => OpenSlideOverAction.To<ItemTypeEditScreen>()
            .With(new ItemTypeBySystemNameQuery { ModelIdentifier = model.TypeName }),
        CarveOutDetailModel.KindPredicate when model.PredicateIndex > 0 => OpenSlideOverAction.To<PredicateEditScreen>()
            .With(new PredicateByIndexQuery { ModelIdentifier = model.PredicateIndex.ToString() }),
        _ => null
    };

    protected override string GetScreenName() =>
        string.IsNullOrWhiteSpace(Model?.TypeName) ? "Stays local" : $"Stays local: {Model.TypeName}";

    protected override CommandBase<CarveOutDetailModel>? GetSaveCommand() => null;
}

using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Selectors;
using Dynamicweb.CoreUI.Screens;

namespace Truvio.Commerce.Serializer.AdminUI.Screens;

/// <summary>
/// "Start from the Swift starter" SlideOver: pick the website, Save applies the embedded
/// starter configuration with the Content predicates rebound to it. Only offered while
/// the configuration is empty (see SerializerSettingsEditScreen Get-started actions).
/// </summary>
public sealed class StarterConfigScreen : EditScreenBase<StarterConfigModel>
{
    protected override void BuildEditScreen()
    {
        AddComponents("Swift starter", new List<LayoutWrapper>
        {
            new("Get started", new List<EditorBase>
            {
                EditorFor(m => m.Summary),
                EditorFor(m => m.AreaId)
            })
        });
    }

    protected override EditorBase? GetEditor(string property) => property switch
    {
        nameof(StarterConfigModel.Summary) => new Dynamicweb.CoreUI.Editors.Inputs.Textarea { Readonly = true, Rows = 5 },
        nameof(StarterConfigModel.AreaId) => SelectorBuilder.CreateAreaSelector(
            value: Model?.AreaId > 0 ? Model.AreaId : null,
            hideDeactivated: true),
        _ => null
    };

    protected override string GetScreenName() => "Start from the Swift starter";

    protected override CommandBase<StarterConfigModel> GetSaveCommand() => new ApplySwiftStarterCommand();
}

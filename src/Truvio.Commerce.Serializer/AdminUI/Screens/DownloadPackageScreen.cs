using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Inputs;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Truvio.Commerce.Serializer.Serialization;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

namespace Truvio.Commerce.Serializer.AdminUI.Screens;

/// <summary>
/// "Download Package" dialog: choose the content scope and whether referenced assets
/// (images/files) are bundled, then download the zip. Follows the ProductExportPromptScreen
/// pattern — OK runs a download action bound to this dialog's model.
/// </summary>
public sealed class DownloadPackageScreen : PromptScreenBase<DownloadPackageModel>
{
    protected override string GetScreenName() => "Download Package";

    protected override string GetOkActionName() => "Download";

    protected override ActionBase GetOkAction() => DownloadFileAction.Using(new DownloadPackageCommand())
        .With(Query)
        .WithOnSuccess(ClosePopupAction.Default);

    protected override void BuildPromptScreen()
    {
        AddComponent(EditorFor(m => m.PageName), "");
        AddComponent(EditorFor(m => m.Scope), "");
        AddComponent(EditorFor(m => m.IncludeAssets), "");
    }

    protected override EditorBase? GetEditor(string propertyName) => propertyName switch
    {
        nameof(DownloadPackageModel.PageName) => new Text { Readonly = true },
        nameof(DownloadPackageModel.Scope) => new Radio
        {
            SortOrder = OrderBy.Default,
            Options = new List<ListOption>
            {
                new() { Value = PackageBuilder.ScopePageAndSubpages, Label = "This page and all subpages" },
                new() { Value = PackageBuilder.ScopePageOnly, Label = "Only this page" },
                new() { Value = PackageBuilder.ScopeSubpagesOnly, Label = "Only the subpages" }
            }
        },
        _ => null
    };
}

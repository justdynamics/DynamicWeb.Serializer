using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Inputs;
using Dynamicweb.CoreUI.Screens;

namespace Truvio.Commerce.Serializer.AdminUI.Screens;

/// <summary>
/// Dialog opened from the content tree right-click menu: upload a zip produced by
/// "Serialize subtree" and import it into the clicked node's area. The upload lands in
/// Files/System/Serializer/Upload; on completion DeserializeUploadedZipCommand routes
/// the file through the shared zip-import pipeline.
/// </summary>
public sealed class DeserializeZipUploadScreen : PromptScreenBase<DeserializeZipUploadModel>
{
    protected override string GetScreenName() => "Deserialize from zip";

    protected override void BuildPromptScreen()
    {
        if (Model is null)
            return;

        if (!string.IsNullOrEmpty(Model.ValidationError))
        {
            AddComponent(EditorFor(m => m.ValidationError), "");
            return;
        }

        AddComponent(EditorFor(m => m.AreaName), "");
        AddComponent(new FileUpload
        {
            Path = Model.UploadFolder,
            Accept = { ".zip" },
            OnUploadCompleteAction = RunCommandAction
                .For<DeserializeUploadedZipCommand>()
                .With(Query)
                .WithCommandProperty(nameof(DeserializeUploadedZipCommand.Files))
                .WithReloadOnSuccess()
        }, "");
    }

    protected override EditorBase? GetEditor(string propertyName) => propertyName switch
    {
        nameof(DeserializeZipUploadModel.AreaName) => new Text { Readonly = true, Label = "Target area" },
        nameof(DeserializeZipUploadModel.ValidationError) => new Textarea { Readonly = true },
        _ => null
    };
}

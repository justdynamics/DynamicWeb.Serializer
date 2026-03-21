using Dynamicweb.ContentSync.AdminUI.Commands;
using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Screens;

namespace Dynamicweb.ContentSync.AdminUI.Screens;

public sealed class SyncSettingsEditScreen : EditScreenBase<SyncSettingsModel>
{
    protected override void BuildEditScreen()
    {
        AddComponents("Settings",
        [
            new("Content Sync",
            [
                EditorFor(m => m.OutputDirectory),
                EditorFor(m => m.LogLevel)
            ])
        ]);
    }

    protected override string GetScreenName() => "Content Sync Settings";
    protected override CommandBase<SyncSettingsModel> GetSaveCommand() => new SaveSyncSettingsCommand();
}

using Dynamicweb.ContentSync.AdminUI.Commands;
using Dynamicweb.ContentSync.AdminUI.Models;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

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
                EditorFor(m => m.LogLevel),
                EditorFor(m => m.DryRun),
                EditorFor(m => m.ConflictStrategy)
            ])
        ]);
    }

    protected override EditorBase? GetEditor(string property)
    {
        return property switch
        {
            nameof(SyncSettingsModel.LogLevel) => CreateLogLevelSelect(),
            nameof(SyncSettingsModel.ConflictStrategy) => CreateConflictStrategySelect(),
            _ => null
        };
    }

    private static Select CreateLogLevelSelect()
    {
        return new Select
        {
            SortOrder = OrderBy.Default,
            Options = new List<ListOption>
            {
                new() { Value = "info", Label = "Info" },
                new() { Value = "debug", Label = "Debug" },
                new() { Value = "warn", Label = "Warn" },
                new() { Value = "error", Label = "Error" }
            }
        };
    }

    private static Select CreateConflictStrategySelect()
    {
        return new Select
        {
            SortOrder = OrderBy.Default,
            Options = new List<ListOption>
            {
                new() { Value = "source-wins", Label = "Source Wins" }
            }
        };
    }

    protected override string GetScreenName() => "Content Sync Settings";
    protected override CommandBase<SyncSettingsModel> GetSaveCommand() => new SaveSyncSettingsCommand();
}

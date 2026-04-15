using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Screens;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

namespace DynamicWeb.Serializer.AdminUI.Screens;

public sealed class SerializerSettingsEditScreen : EditScreenBase<SerializerSettingsModel>
{
    protected override void BuildEditScreen()
    {
        AddComponents("Settings",
        [
            new("Serialize",
            [
                EditorFor(m => m.OutputDirectory),
                EditorFor(m => m.LogLevel),
                EditorFor(m => m.DryRun),
                EditorFor(m => m.ConflictStrategy)
            ]),
            new("Information",
            [
                EditorFor(m => m.ConfigFilePath),
                EditorFor(m => m.PredicatesSummary)
            ])
        ]);
    }

    protected override IEnumerable<ActionGroup>? GetScreenActions()
    {
        return new[]
        {
            new ActionGroup
            {
                Name = "Actions",
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Serialize",
                        Icon = Icon.DownloadAlt,
                        NodeAction = RunCommandAction.For<SerializerSerializeCommand>().WithReloadOnSuccess()
                    },
                    new()
                    {
                        Name = "Deserialize",
                        Icon = Icon.UploadAlt,
                        NodeAction = RunCommandAction.For<SerializerDeserializeCommand>().WithReloadOnSuccess()
                    }
                }
            }
        };
    }

    protected override EditorBase? GetEditor(string property)
    {
        return property switch
        {
            nameof(SerializerSettingsModel.LogLevel) => CreateLogLevelSelect(),
            nameof(SerializerSettingsModel.ConflictStrategy) => CreateConflictStrategySelect(),
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

    protected override string GetScreenName() => "Serialize Settings";
    protected override CommandBase<SerializerSettingsModel> GetSaveCommand() => new SaveSerializerSettingsCommand();
}

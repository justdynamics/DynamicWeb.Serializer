using Truvio.Commerce.Serializer.AdminUI.Commands;
using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Icons;
using Dynamicweb.CoreUI.Screens;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

namespace Truvio.Commerce.Serializer.AdminUI.Screens;

public sealed class SerializerSettingsEditScreen : EditScreenBase<SerializerSettingsModel>
{
    protected override void BuildEditScreen()
    {
        AddComponents("Settings",
        [
            new("Serialize",
            [
                EditorFor(m => m.OutputDirectory)
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
                Name = "Deploy Actions",
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Serialize",
                        Icon = Icon.DownloadAlt,
                        NodeAction = RunCommandAction.For(new SerializerSerializeCommand { Mode = "deploy" }).WithReloadOnSuccess()
                    },
                    new()
                    {
                        Name = "Deserialize",
                        Icon = Icon.UploadAlt,
                        // Phase 37-04 D-16: admin UI is the interactive entry point — flip
                        // IsAdminUiInvocation so the resolver falls back to AdminUi default (OFF).
                        NodeAction = RunCommandAction.For(new SerializerDeserializeCommand { Mode = "deploy", IsAdminUiInvocation = true }).WithReloadOnSuccess()
                    }
                }
            },
            // Phase 37-01 D-04: Seed requires explicit opt-in — expose via a dedicated action group
            // so admins can't trigger a destination-wins deserialize by accident.
            new ActionGroup
            {
                Name = "Seed Actions",
                Nodes = new List<ActionNode>
                {
                    new()
                    {
                        Name = "Serialize (Seed)",
                        Icon = Icon.DownloadAlt,
                        NodeAction = RunCommandAction.For(new SerializerSerializeCommand { Mode = "seed" }).WithReloadOnSuccess()
                    },
                    new()
                    {
                        Name = "Deserialize (Seed)",
                        Icon = Icon.UploadAlt,
                        // Phase 37-04 D-16: admin UI triggered — resolver uses AdminUi default (OFF).
                        NodeAction = RunCommandAction.For(new SerializerDeserializeCommand { Mode = "seed", IsAdminUiInvocation = true }).WithReloadOnSuccess()
                    }
                }
            }
        };
    }

    protected override string GetScreenName() => "Serialize Settings";
    protected override CommandBase<SerializerSettingsModel> GetSaveCommand() => new SaveSerializerSettingsCommand();
}

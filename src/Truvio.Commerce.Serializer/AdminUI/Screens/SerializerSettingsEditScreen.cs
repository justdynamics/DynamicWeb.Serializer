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
                EditorFor(m => m.OutputDirectory),
                EditorFor(m => m.DeployOutputSubfolder),
                EditorFor(m => m.SeedOutputSubfolder),
                EditorFor(m => m.ShowDeployIndicators),
                EditorFor(m => m.ShowSeedIndicators)
            ]),
            new("Information",
            [
                EditorFor(m => m.ConfigFilePath),
                EditorFor(m => m.LastRunsSummary),
                EditorFor(m => m.CoverageSummary),
                EditorFor(m => m.ItemTypeExcludesSummary),
                EditorFor(m => m.XmlExcludesSummary),
                EditorFor(m => m.PredicatesSummary)
            ])
        ]);
    }

    protected override IEnumerable<ActionGroup>? GetScreenActions()
    {
        // First-run: no config or no predicates — getting started is the only sensible
        // action, so the sync actions make way for it (running them would just error).
        if (Model?.NeedsSetup == true)
        {
            return new[]
            {
                new ActionGroup
                {
                    Name = "Get started",
                    Nodes = new List<ActionNode>
                    {
                        new()
                        {
                            Name = "Start from the Swift starter…",
                            Icon = Icon.Rocket,
                            NodeAction = OpenSlideOverAction.To<StarterConfigScreen>()
                                .With(new Truvio.Commerce.Serializer.AdminUI.Queries.StarterConfigQuery())
                        },
                        new()
                        {
                            Name = "Create empty configuration",
                            Icon = Icon.FileAlt,
                            NodeAction = RunCommandAction.For(new CreateEmptyConfigCommand()).WithReloadOnSuccess()
                        }
                    }
                }
            };
        }

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
                        // Dry run: full pipeline, nothing written — the answer to "what
                        // would happen if I deserialized right now?" before committing.
                        Name = "Preview deserialize (dry run)",
                        Icon = Icon.Eye,
                        NodeAction = RunCommandAction.For(new SerializerDeserializeCommand { Mode = "deploy", IsAdminUiInvocation = true, IsDryRun = true })
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
                        Name = "Preview seed (dry run)",
                        Icon = Icon.Eye,
                        NodeAction = RunCommandAction.For(new SerializerDeserializeCommand { Mode = "seed", IsAdminUiInvocation = true, IsDryRun = true })
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

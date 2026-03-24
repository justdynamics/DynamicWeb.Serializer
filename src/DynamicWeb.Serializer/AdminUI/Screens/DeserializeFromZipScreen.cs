using DynamicWeb.Serializer.AdminUI.Commands;
using DynamicWeb.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI;
using Dynamicweb.CoreUI.Actions;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Displays.Information;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Inputs;
using Dynamicweb.CoreUI.Layout;
using Dynamicweb.CoreUI.Screens;

namespace DynamicWeb.Serializer.AdminUI.Screens;

/// <summary>
/// Dialog screen for deserializing from a zip file.
/// Auto-runs a dry-run on load (via query/model) and shows per-table breakdown.
/// User confirms to execute actual deserialization via DeserializeFromZipCommand.
/// </summary>
public sealed class DeserializeFromZipScreen : PromptScreenBase<DeserializeFromZipModel>
{
    protected override string GetScreenName() => "Import to Database";

    protected override void BuildPromptScreen()
    {
        var model = Model;
        if (model == null)
            return;

        // Show file name being imported
        AddComponent(EditorFor(m => m.FileName), "Import Details");

        if (!model.IsValid)
        {
            // Show validation error
            if (!string.IsNullOrEmpty(model.ValidationError))
            {
                AddComponent(EditorFor(m => m.ValidationError), "Validation");
            }
            return;
        }

        // Show dry-run per-table breakdown (PredicateSummary iteration)
        if (model.DryRunSummary != null)
        {
            var breakdownComponents = new List<UiComponentBase?>();

            foreach (var predicate in model.DryRunSummary.Predicates)
            {
                var line = $"{predicate.Name} ({predicate.Table}): " +
                           $"{predicate.Created} new, {predicate.Updated} updated, {predicate.Skipped} skipped";
                if (predicate.Failed > 0)
                    line += $", {predicate.Failed} failed";

                breakdownComponents.Add(new TextBlock { Value = line });
            }

            var totalLine = $"Total: {model.DryRunSummary.TotalCreated} new, " +
                           $"{model.DryRunSummary.TotalUpdated} updated, " +
                           $"{model.DryRunSummary.TotalSkipped} skipped";
            if (model.DryRunSummary.TotalFailed > 0)
                totalLine += $", {model.DryRunSummary.TotalFailed} failed";

            breakdownComponents.Add(new TextBlock { Value = totalLine });

            AddComponents(breakdownComponents, "Dry-Run Summary");

            // Show warnings if dry-run had errors
            if (model.DryRunSummary.Errors.Count > 0)
            {
                var warningComponents = model.DryRunSummary.Errors
                    .Select(e => (UiComponentBase?)new TextBlock { Value = $"Warning: {e}" })
                    .ToList();
                AddComponents(warningComponents, "Warnings");
            }
        }
    }

    protected override string GetOkActionName() => "Confirm Import";

    protected override CommandBase<DeserializeFromZipModel>? GetOkCommand()
    {
        if (Model?.IsValid != true)
            return null;

        return new DeserializeFromZipCommand { FilePath = Model.FilePath };
    }

    protected override EditorBase? GetEditor(string property)
    {
        return property switch
        {
            nameof(DeserializeFromZipModel.FileName) => new Text { Readonly = true },
            nameof(DeserializeFromZipModel.ValidationError) => new Textarea { Readonly = true },
            _ => null
        };
    }
}

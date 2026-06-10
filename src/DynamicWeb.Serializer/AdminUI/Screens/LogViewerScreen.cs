using DynamicWeb.Serializer.AdminUI.Models;
using DynamicWeb.Serializer.AdminUI.Queries;
using Dynamicweb.CoreUI.Data;
using Dynamicweb.CoreUI.Editors;
using Dynamicweb.CoreUI.Editors.Inputs;
using Dynamicweb.CoreUI.Editors.Lists;
using Dynamicweb.CoreUI.Screens;
using static Dynamicweb.CoreUI.Editors.Inputs.ListBase;

namespace DynamicWeb.Serializer.AdminUI.Screens;

public sealed class LogViewerScreen : EditScreenBase<LogViewerModel>
{
    protected override void BuildEditScreen()
    {
        var model = Model;

        // Reload file data based on the ShadowEdit-overlaid SelectedFileName
        // (ShadowEdit sets SelectedFileName before BuildEditScreen runs,
        // but the query loaded data for the default file)
        model?.ReloadSelectedFile();

        if (model == null || !model.HasLogs)
        {
            AddComponents("Log Viewer",
            [
                new("No Logs Available",
                [
                    EditorFor(m => m.RawLogText)
                ])
            ]);
            return;
        }

        // Build sections dynamically based on available data
        var sections = new List<LayoutWrapper>
        {
            new("Log File Selection",
            [
                EditorFor(m => m.SelectedFileName)
            ])
        };

        if (model.Summary != null)
        {
            sections.Add(new("Summary",
            [
                EditorFor(m => m.SummaryOperation),
                EditorFor(m => m.SummaryTimestamp),
                EditorFor(m => m.SummaryTotalCreated),
                EditorFor(m => m.SummaryTotalUpdated),
                EditorFor(m => m.SummaryTotalSkipped),
                EditorFor(m => m.SummaryTotalFailed)
            ]));

            if (model.PredicateBreakdown.Length > 0)
            {
                sections.Add(new("Predicate Breakdown",
                [
                    EditorFor(m => m.PredicateBreakdown)
                ]));
            }

            if (model.AdviceText.Length > 0)
            {
                sections.Add(new("Advice",
                [
                    EditorFor(m => m.AdviceText)
                ]));
            }
        }

        sections.Add(new("Log Output",
        [
            EditorFor(m => m.RawLogText)
        ]));

        AddComponents("Log Viewer", sections);
    }

    protected override EditorBase? GetEditor(string property)
    {
        return property switch
        {
            nameof(LogViewerModel.SelectedFileName) => CreateLogFileSelect(),
            nameof(LogViewerModel.PredicateBreakdown) => new Textarea
            {
                Label = "Predicate Breakdown",
                Explanation = "Per-predicate summary of the operation"
            },
            nameof(LogViewerModel.AdviceText) => new Textarea
            {
                Label = "Advice",
                Explanation = "Guided recommendations based on operation results"
            },
            nameof(LogViewerModel.RawLogText) => new Textarea
            {
                Label = "Log Output",
                Explanation = "Full log text from the selected file"
            },
            _ => null
        };
    }

    private Select CreateLogFileSelect()
    {
        var options = (Model?.AvailableLogFiles ?? new List<string>())
            .Select(f => new ListOption
            {
                Value = f,
                Label = f
            }).ToList();

        return new Select
        {
            SortOrder = OrderBy.Default,
            Options = options
        }.WithReloadOnChange();
    }

    protected override string GetScreenName() => "Log Viewer";

    protected override CommandBase<LogViewerModel>? GetSaveCommand() => null;
}

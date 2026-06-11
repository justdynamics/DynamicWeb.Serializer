using Dynamicweb.Content;
using Dynamicweb.Content.UI.Models;
using Dynamicweb.Content.UI.Screens;
using Dynamicweb.CoreUI;
using Dynamicweb.CoreUI.Displays.Information;
using Dynamicweb.CoreUI.Layout;
using Dynamicweb.CoreUI.Screens;

namespace Truvio.Commerce.Serializer.AdminUI.Injectors;

/// <summary>
/// Mode alerts for the content EDITING surfaces. Predicate mode is a per-page property —
/// every grid row and paragraph inherits its page's mode — so rather than per-element
/// badges, each editing screen (visual editor, paragraph dialog, grid-row dialog) gets one
/// screen-level alert for the owning page:
///
///   deploy → warning: edits here are overwritten by the next deploy;
///   seed   → info: starter content, local edits are preserved.
///
/// Uses <see cref="ScreenLayout.Alert"/>, DW's native screen alert slot. Unlike the tree's
/// seed icons (config-gated, default off), the editor alerts always show — at the moment of
/// editing, "will my change survive?" is exactly the question being answered.
/// </summary>
internal static class ModeAlert
{
    public static void Apply(UiComponentBase content, int pageId)
    {
        try
        {
            if (pageId <= 0 || !content.TryGet<ScreenLayout>(out var layout) || layout is null || layout.Alert is not null)
                return;

            var evaluators = TreeNodeDecorator.TryCreateEvaluators();
            if (evaluators is null)
                return;

            var page = Services.Pages.GetPage(pageId);
            if (page is null)
                return;

            var checkPath = TreeNodeDecorator.GetPredicateCheckPath(page);

            var deployNames = evaluators.Deploy?.GetManagingPredicateNames(checkPath, page.AreaId);
            if (deployNames is { Count: > 0 })
            {
                layout.Alert = new Alert
                {
                    Type = AlertType.Warning,
                    Icon = Dynamicweb.CoreUI.Icons.Icon.Sync,
                    Value = $"This page is managed at deploy by '{string.Join("', '", deployNames)}'. " +
                            "Content here is overwritten by the next deploy — make lasting changes in the source environment."
                };
                return;
            }

            var seedNames = evaluators.Seed?.GetManagingPredicateNames(checkPath, page.AreaId);
            if (seedNames is { Count: > 0 })
            {
                layout.Alert = new Alert
                {
                    Type = AlertType.Info,
                    Icon = Dynamicweb.CoreUI.Icons.Icon.Flower,
                    Value = $"Starter content seeded by '{string.Join("', '", seedNames)}'. " +
                            "Edits on this environment are preserved; only fields left empty are filled by the next seed."
                };
            }
        }
        catch
        {
            // Alerts are best-effort; never break an editing screen over config issues.
        }
    }
}

/// <summary>Mode alert on the visual editor (page canvas).</summary>
public sealed class SerializerVisualEditAlertInjector : ScreenInjector<PageVisualEditScreen>
{
    public override void OnAfter(PageVisualEditScreen screen, UiComponentBase content)
        => ModeAlert.Apply(content, screen?.Model?.Id ?? 0);
}

/// <summary>Mode alert on the paragraph edit dialog.</summary>
public sealed class SerializerParagraphEditAlertInjector : ScreenInjector<ParagraphEditScreen>
{
    public override void OnAfter(ParagraphEditScreen screen, UiComponentBase content)
        => ModeAlert.Apply(content, screen?.Model?.PageID ?? 0);
}

/// <summary>Mode alert on the grid-row edit dialog.</summary>
public sealed class SerializerGridRowEditAlertInjector : ScreenInjector<GridRowEditScreen>
{
    public override void OnAfter(GridRowEditScreen screen, UiComponentBase content)
        => ModeAlert.Apply(content, screen?.Model?.PageId ?? 0);
}

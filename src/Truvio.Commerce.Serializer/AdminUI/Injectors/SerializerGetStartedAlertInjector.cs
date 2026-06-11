using Dynamicweb.CoreUI;
using Dynamicweb.CoreUI.Displays.Information;
using Dynamicweb.CoreUI.Layout;
using Dynamicweb.CoreUI.Screens;
using Truvio.Commerce.Serializer.AdminUI.Screens;

namespace Truvio.Commerce.Serializer.AdminUI.Injectors;

/// <summary>
/// First-run visibility: with no configuration (or no predicates) the Serialize Settings
/// screen swaps its actions for the Get-started group — but action groups live behind the
/// Actions dropdown, so a fresh install LOOKS like an ordinary settings form and the offer
/// is easy to miss. This screen-level alert states the situation and points at the dropdown.
/// </summary>
public sealed class SerializerGetStartedAlertInjector : ScreenInjector<SerializerSettingsEditScreen>
{
    public override void OnAfter(SerializerSettingsEditScreen screen, UiComponentBase content)
    {
        try
        {
            if (screen?.Model?.NeedsSetup != true)
                return;

            if (!content.TryGet<ScreenLayout>(out var layout) || layout is null || layout.Alert is not null)
                return;

            layout.Alert = new Alert
            {
                Type = AlertType.Info,
                Icon = Dynamicweb.CoreUI.Icons.Icon.Rocket,
                Value = "No serializer configuration yet — nothing is synced. Open the Actions menu (top right) and " +
                        "choose 'Start from the Swift starter…' for a curated Swift setup, or 'Create empty configuration' " +
                        "to define predicates yourself."
            };
        }
        catch
        {
            // The alert is a convenience; never break the settings screen over it.
        }
    }
}

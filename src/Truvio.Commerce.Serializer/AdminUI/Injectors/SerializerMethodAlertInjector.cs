using Dynamicweb.CoreUI;
using Dynamicweb.CoreUI.Actions.Implementations;
using Dynamicweb.CoreUI.Displays.Information;
using Dynamicweb.CoreUI.Layout;
using Dynamicweb.CoreUI.Screens;
using Dynamicweb.Ecommerce.UI.Screens;
using Truvio.Commerce.Serializer.AdminUI.Queries;
using Truvio.Commerce.Serializer.AdminUI.Screens;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Models;
using static Dynamicweb.CoreUI.Displays.Widgets.CardInfo;

namespace Truvio.Commerce.Serializer.AdminUI.Injectors;

/// <summary>
/// Mode alerts for the commerce-settings edit screens whose data ships through SqlTable
/// predicates — the SqlTable counterpart of the content editor alerts. Every entity edit
/// screen backed by a deploy-managed table gets a cue: an editor changing a VAT rate, a
/// currency format or a payment method on a target environment is silently overwritten by
/// the next deploy, and for payment/shipping the inverse trap exists too (excluded
/// credential columns/elements that do NOT sync). List/bulk surfaces (catalog rows, URL
/// redirects) deliberately get no cue — see docs/swift-deploy-seed-analysis.md §7.
///
/// Same shape as the content alerts: a single verdict sentence in the screen alert, the
/// exception list as a clickable header chip navigating to the managing predicate (falls
/// back to inline text when the screen has no info bar). Deploy warnings always show; seed
/// info alerts only when showSeedIndicators is on.
/// </summary>
internal static class SqlTableModeAlert
{
    public static void Apply(UiComponentBase content, string table, string entityNoun)
    {
        try
        {
            if (!content.TryGet<ScreenLayout>(out var layout) || layout is null || layout.Alert is not null)
                return;

            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath is null)
                return;

            var config = ConfigLoader.Load(configPath);
            var index = config.Predicates.FindIndex(p =>
                string.Equals(p.ProviderType, "SqlTable", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Table, table, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return;

            var predicate = config.Predicates[index];

            var localParts = new List<string>();
            if (predicate.ExcludeFields.Count > 0)
                localParts.Add($"columns {string.Join(", ", predicate.ExcludeFields)}");
            if (predicate.ExcludeXmlElements.Count > 0)
                localParts.Add($"provider settings {string.Join(", ", predicate.ExcludeXmlElements)}");

            var isDeploy = predicate.Mode == DeploymentMode.Deploy;
            if (!isDeploy && !config.ShowSeedIndicators)
                return;

            AddExclusionChip(layout, predicate, index, localParts);

            layout.Alert = isDeploy
                ? new Alert
                {
                    Type = AlertType.Warning,
                    Icon = Dynamicweb.CoreUI.Icons.Icon.Sync,
                    Value = $"This {entityNoun} is managed at deploy by '{predicate.Name}'. " +
                            "Changes here are overwritten by the next deploy — make lasting changes in the source environment."
                }
                : new Alert
                {
                    Type = AlertType.Info,
                    Icon = Dynamicweb.CoreUI.Icons.Icon.Flower,
                    Value = $"This {entityNoun} is seeded by '{predicate.Name}'. " +
                            "Edits on this environment are preserved; only fields left empty are filled by the next seed."
                };
        }
        catch
        {
            // Alerts are best-effort; never break a settings screen over config issues.
        }
    }

    /// <summary>
    /// Renders the predicate's exclusions as a clickable header chip ("Stays local — N
    /// exclusions — view") opening the read-only "Stays local" SlideOver, which lists
    /// excludeFields / excludeXmlElements in full and offers "Manage exclusions" to admins.
    /// Screens without an info bar get one created — alert text cannot carry a click.
    /// </summary>
    private static void AddExclusionChip(ScreenLayout layout, ProviderPredicateDefinition predicate, int index, List<string> localParts)
    {
        if (localParts.Count == 0)
            return;

        layout.InfoBar ??= new Dynamicweb.CoreUI.Displays.Information.InfoBar();
        layout.InfoBar.Information ??= new Dictionary<string, InfoValue>();
        var count = predicate.ExcludeFields.Count + predicate.ExcludeXmlElements.Count;
        layout.InfoBar.Information[$"Stays local ({predicate.Name})"] = new InfoValue(
            count == 1 ? "1 exclusion — view" : $"{count} exclusions — view",
            OpenSlideOverAction.To<CarveOutDetailScreen>()
                .With(new CarveOutDetailQuery
                {
                    TypeName = predicate.Name,
                    Kind = Models.CarveOutDetailModel.KindPredicate
                }));
    }
}

/// <summary>Mode alert on the payment method edit screen (EcomPayments predicate).</summary>
public sealed class SerializerPaymentEditAlertInjector : ScreenInjector<PaymentEditScreen>
{
    public override void OnAfter(PaymentEditScreen screen, UiComponentBase content)
        => SqlTableModeAlert.Apply(content, "EcomPayments", "payment method");
}

/// <summary>Mode alert on the shipping method edit screen (EcomShippings predicate).</summary>
public sealed class SerializerShippingEditAlertInjector : ScreenInjector<ShippingEditScreen>
{
    public override void OnAfter(ShippingEditScreen screen, UiComponentBase content)
        => SqlTableModeAlert.Apply(content, "EcomShippings", "shipping method");
}

/// <summary>Mode alert on the country edit screen (EcomCountries predicate).</summary>
public sealed class SerializerCountryEditAlertInjector : ScreenInjector<Dynamicweb.Products.UI.Screens.Settings.CountryEditScreen>
{
    public override void OnAfter(Dynamicweb.Products.UI.Screens.Settings.CountryEditScreen screen, UiComponentBase content)
        => SqlTableModeAlert.Apply(content, "EcomCountries", "country");
}

/// <summary>Mode alert on the currency edit screen (EcomCurrencies predicate).</summary>
public sealed class SerializerCurrencyEditAlertInjector : ScreenInjector<Dynamicweb.Products.UI.Screens.Settings.CurrencyEditScreen>
{
    public override void OnAfter(Dynamicweb.Products.UI.Screens.Settings.CurrencyEditScreen screen, UiComponentBase content)
        => SqlTableModeAlert.Apply(content, "EcomCurrencies", "currency");
}

/// <summary>Mode alert on the ecommerce language edit screen (EcomLanguages predicate).</summary>
public sealed class SerializerEcomLanguageEditAlertInjector : ScreenInjector<Dynamicweb.Products.UI.Screens.LanguageEditScreen>
{
    public override void OnAfter(Dynamicweb.Products.UI.Screens.LanguageEditScreen screen, UiComponentBase content)
        => SqlTableModeAlert.Apply(content, "EcomLanguages", "ecommerce language");
}

// NOTE: no VatGroupEditScreen injector — the type is not public in Dynamicweb.Products.UI
// 10.23.9 (it is in later versions). Add the cue when the package pin moves past it.

/// <summary>Mode alert on the shop edit screen (EcomShops predicate).</summary>
public sealed class SerializerShopEditAlertInjector : ScreenInjector<Dynamicweb.Products.UI.Screens.Settings.ShopEditScreen>
{
    public override void OnAfter(Dynamicweb.Products.UI.Screens.Settings.ShopEditScreen screen, UiComponentBase content)
        => SqlTableModeAlert.Apply(content, "EcomShops", "shop");
}

/// <summary>Mode alert on the order flow edit screen (EcomOrderFlow predicate).</summary>
public sealed class SerializerOrderFlowEditAlertInjector : ScreenInjector<OrderFlowEditScreen>
{
    public override void OnAfter(OrderFlowEditScreen screen, UiComponentBase content)
        => SqlTableModeAlert.Apply(content, "EcomOrderFlow", "order flow");
}

/// <summary>Mode alert on the order state edit screen (EcomOrderStates predicate).</summary>
public sealed class SerializerOrderStateEditAlertInjector : ScreenInjector<OrderStateEditScreen>
{
    public override void OnAfter(OrderStateEditScreen screen, UiComponentBase content)
        => SqlTableModeAlert.Apply(content, "EcomOrderStates", "order state");
}

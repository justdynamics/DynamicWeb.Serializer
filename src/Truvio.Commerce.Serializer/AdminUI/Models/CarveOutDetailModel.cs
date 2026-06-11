using Dynamicweb.CoreUI.Data;
using Truvio.Commerce.Serializer.AdminUI.Infrastructure;
using Truvio.Commerce.Serializer.Configuration;
using Truvio.Commerce.Serializer.Providers.SqlTable;

namespace Truvio.Commerce.Serializer.AdminUI.Models;

/// <summary>
/// Read-only detail behind a carve-out cue: WHICH fields/settings stay local for a type or
/// predicate. Opened as a SlideOver from the tree context menu, the editor header chips and
/// the commerce-screen chips — surfaces a content editor can reach without Settings access.
/// Editing the exclusions stays on the Settings screens; the screen offers a "Manage" action
/// only to administrators.
/// </summary>
public sealed class CarveOutDetailModel : DataViewModelBase
{
    public const string KindXmlElements = "XmlElements";
    public const string KindItemTypeFields = "ItemTypeFields";
    public const string KindPredicate = "Predicate";
    public const string KindPage = "Page";

    [ConfigurableProperty("Type", explanation: "The item type, module/provider XML type, or predicate these exclusions belong to.")]
    public string TypeName { get; set; } = string.Empty;

    [ConfigurableProperty("What this means", explanation: "")]
    public string Summary { get; set; } = string.Empty;

    [ConfigurableProperty("Excluded fields", explanation: "These values are never written by a sync — each environment keeps its own.")]
    public string ExcludedFields { get; set; } = string.Empty;

    [ConfigurableProperty("Sample XML from database", explanation: "Raw XML for this type so the excluded element names can be seen in context.")]
    public string SampleXml { get; set; } = string.Empty;

    /// <summary>One of the Kind* constants — drives which settings editor "Manage" opens.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>1-based predicate index for Kind=Predicate manage navigation.</summary>
    public int PredicateIndex { get; set; }

    /// <summary>True when the current user may open the settings editors (admins only).</summary>
    public bool CanManage { get; set; }

    public static CarveOutDetailModel Load(string typeName, string kind)
    {
        var model = new CarveOutDetailModel
        {
            TypeName = typeName,
            Kind = kind,
            CanManage = CurrentUserIsAdmin()
        };

        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
            {
                model.Summary = "No serializer configuration found.";
                return model;
            }

            var config = ConfigLoader.Load(configPath);

            switch (kind)
            {
                case KindXmlElements:
                    var elements = FieldExclusionInspector.Lookup(config.ExcludeXmlElementsByType, typeName);
                    model.ExcludedFields = string.Join("\n", elements ?? new List<string>());
                    model.Summary =
                        $"Settings of '{typeName}' listed below stay local to this environment. " +
                        "A deploy never overwrites them; a seed never fills them. Everything else on the page syncs normally.";
                    model.SampleXml = TryGetSampleXml(typeName);
                    break;

                case KindItemTypeFields:
                    var fields = FieldExclusionInspector.Lookup(config.ExcludeFieldsByItemType, typeName);
                    model.ExcludedFields = string.Join("\n", fields ?? new List<string>());
                    model.Summary =
                        $"Fields of item type '{typeName}' listed below stay local to this environment. " +
                        "A deploy never overwrites them; a seed never fills them. Everything else syncs normally.";
                    break;

                case KindPredicate:
                    var index = config.Predicates.FindIndex(p =>
                        string.Equals(p.Name, typeName, StringComparison.OrdinalIgnoreCase));
                    if (index < 0)
                    {
                        model.Summary = $"Predicate '{typeName}' was not found in the configuration.";
                        return model;
                    }
                    var predicate = config.Predicates[index];
                    model.PredicateIndex = index + 1;
                    var parts = new List<string>();
                    if (predicate.ExcludeFields.Count > 0)
                        parts.Add("Columns:\n  " + string.Join("\n  ", predicate.ExcludeFields));
                    if (predicate.ExcludeXmlElements.Count > 0)
                        parts.Add("Provider settings (XML elements):\n  " + string.Join("\n  ", predicate.ExcludeXmlElements));
                    model.ExcludedFields = string.Join("\n\n", parts);
                    model.Summary =
                        $"The exclusions of predicate '{predicate.Name}' (table {predicate.Table}) listed below stay local " +
                        "to this environment. Everything else in the table syncs normally.";
                    break;

                default:
                    model.Summary = $"Unknown exclusion kind '{kind}'.";
                    break;
            }
        }
        catch (Exception ex)
        {
            model.Summary = $"Could not load exclusion details: {ex.Message}";
        }

        return model;
    }

    /// <summary>
    /// Page-scoped detail behind the tree's single "View excluded fields" context entry:
    /// every carve-out on the page in one panel. A page carrying exactly one carved-out
    /// type delegates to the per-type load so the sample XML and admin manage link apply.
    /// </summary>
    public static CarveOutDetailModel LoadForPage(int pageId)
    {
        var model = new CarveOutDetailModel
        {
            Kind = KindPage,
            CanManage = CurrentUserIsAdmin()
        };

        try
        {
            var configPath = ConfigPathResolver.FindConfigFile();
            if (configPath == null)
            {
                model.Summary = "No serializer configuration found.";
                return model;
            }
            var config = ConfigLoader.Load(configPath);

            var page = Dynamicweb.Content.Services.Pages.GetPage(pageId);
            if (page is null)
            {
                model.Summary = $"Page {pageId} was not found.";
                return model;
            }
            model.TypeName = page.MenuText;

            var paragraphs = Dynamicweb.Content.Services.Paragraphs.GetParagraphsByPageId(pageId)
                .Select(p => ((string?)p.ItemType, (string?)p.ModuleSystemName));
            var carveOuts = FieldExclusionInspector.Describe(
                page.ItemType, page.UrlDataProviderTypeName, paragraphs,
                config.ExcludeFieldsByItemType, config.ExcludeXmlElementsByType);

            if (carveOuts.Count == 0)
            {
                model.Summary = "Nothing on this page is excluded — every field syncs.";
                return model;
            }

            if (carveOuts.Count == 1)
            {
                var only = carveOuts[0];
                return Load(only.TypeName, only.Kind == CarveOutKind.XmlElements
                    ? KindXmlElements
                    : KindItemTypeFields);
            }

            var sections = new List<string>();
            foreach (var carveOut in carveOuts)
            {
                var dict = carveOut.Kind == CarveOutKind.XmlElements
                    ? config.ExcludeXmlElementsByType
                    : config.ExcludeFieldsByItemType;
                var names = FieldExclusionInspector.Lookup(dict, carveOut.TypeName) ?? new List<string>();
                sections.Add($"{carveOut.Label}:\n  " + string.Join("\n  ", names));
            }
            model.ExcludedFields = string.Join("\n\n", sections);
            model.Summary =
                $"Fields and settings on '{page.MenuText}' listed below stay local to this environment. " +
                "A deploy never overwrites them; a seed never fills them. Everything else on the page syncs normally.";
        }
        catch (Exception ex)
        {
            model.Summary = $"Could not load exclusion details: {ex.Message}";
        }

        return model;
    }

    private static string TryGetSampleXml(string typeName)
    {
        try
        {
            return new XmlTypeDiscovery(new DwSqlExecutor()).GetSampleXml(typeName) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Best-effort admin check — when the user context is unavailable, default to NOT
    /// offering the manage link (the read-only view is the safe baseline).</summary>
    private static bool CurrentUserIsAdmin()
    {
        try
        {
            return Dynamicweb.Security.UserManagement.UserContext.Current?.User?.IsAdmin == true;
        }
        catch
        {
            return false;
        }
    }
}

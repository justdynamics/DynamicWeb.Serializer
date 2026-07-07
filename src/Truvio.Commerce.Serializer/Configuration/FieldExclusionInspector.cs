namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>Which exclusion dict a carve-out came from — determines the admin screen a
/// click-through opens (Item Type Excludes vs Embedded XML Excludes).</summary>
public enum CarveOutKind
{
    ItemTypeFields,
    XmlElements
}

/// <summary>
/// One field-level carve-out on a page: a type present on the page whose exclusion list
/// keeps part of its fields local. <see cref="Label"/> is the display form, e.g.
/// <c>"eCom_CartV2 (21 settings)"</c> — the surrounding sentence supplies the verdict
/// ("stay local", "never filled") so the label stays context-neutral.
/// </summary>
public sealed record FieldCarveOut(CarveOutKind Kind, string TypeName, int Count)
{
    public string Label
    {
        get
        {
            var noun = Kind == CarveOutKind.XmlElements ? "setting" : "field";
            return Count == 1 ? $"{TypeName} (1 {noun})" : $"{TypeName} ({Count} {noun}s)";
        }
    }
}

/// <summary>
/// Detects field-level carve-outs on a single page: content the path algebra reports as
/// managed, but where the global by-type exclusion dicts
/// (<see cref="SerializerConfiguration.ExcludeFieldsByItemType"/> /
/// <see cref="SerializerConfiguration.ExcludeXmlElementsByType"/>) keep part of the page's
/// fields local to the environment. The classic case is the cart page: the page itself is
/// covered by a replace predicate, but the eCom_CartV2 module-settings exclusions (mail
/// recipients, error messages, default payment/shipping ids) never sync — the page is only
/// PARTIALLY managed, and the tree icon / editor alert must say so.
///
/// Per-predicate flat <c>excludeFields</c> are deliberately NOT reported here: they apply
/// to every page a predicate covers (mostly area-level item fields like domains and GTM
/// ids), so flagging them per page would mark the whole tree partial and drown the signal.
/// They stay visible on the predicate edit screen and the settings overview.
///
/// Pure: callers supply the type names found on the page; no DW service access here.
/// </summary>
public static class FieldExclusionInspector
{
    /// <summary>
    /// Carve-outs for one page. Empty when nothing on the page matches a non-empty
    /// exclusion list. Distinct per type, sorted by type name for stable display.
    /// </summary>
    /// <param name="pageItemType">The page's item type (matched against ExcludeFieldsByItemType).</param>
    /// <param name="pageUrlProviderType">The page's URL data provider type name (matched against ExcludeXmlElementsByType).</param>
    /// <param name="paragraphs">Item type + module system name per paragraph on the page.</param>
    public static IReadOnlyList<FieldCarveOut> Describe(
        string? pageItemType,
        string? pageUrlProviderType,
        IEnumerable<(string? ItemType, string? ModuleSystemName)> paragraphs,
        IReadOnlyDictionary<string, List<string>> excludeFieldsByItemType,
        IReadOnlyDictionary<string, List<string>> excludeXmlElementsByType)
    {
        if (excludeFieldsByItemType.Count == 0 && excludeXmlElementsByType.Count == 0)
            return Array.Empty<FieldCarveOut>();

        var carveOuts = new SortedDictionary<string, FieldCarveOut>(StringComparer.OrdinalIgnoreCase);

        void AddFieldNote(string? itemType)
        {
            var excluded = Lookup(excludeFieldsByItemType, itemType);
            if (excluded is not null)
                carveOuts[$"item:{itemType}"] = new FieldCarveOut(CarveOutKind.ItemTypeFields, itemType!, excluded.Count);
        }

        void AddXmlNote(string? xmlType)
        {
            var excluded = Lookup(excludeXmlElementsByType, xmlType);
            if (excluded is not null)
                carveOuts[$"xml:{xmlType}"] = new FieldCarveOut(CarveOutKind.XmlElements, xmlType!, excluded.Count);
        }

        AddFieldNote(pageItemType);
        AddXmlNote(pageUrlProviderType);

        foreach (var (itemType, moduleSystemName) in paragraphs)
        {
            AddFieldNote(itemType);
            AddXmlNote(moduleSystemName);
        }

        return carveOuts.Values.ToList();
    }

    /// <summary>Case-insensitive dict lookup; only non-empty exclusion lists count (an empty
    /// list means the type was discovered but nothing is excluded). Shared with the
    /// carve-out detail SlideOver, which shows the matched list read-only.</summary>
    internal static List<string>? Lookup(IReadOnlyDictionary<string, List<string>> dict, string? key)
    {
        if (string.IsNullOrEmpty(key) || dict.Count == 0)
            return null;

        if (dict.TryGetValue(key, out var direct))
            return direct.Count > 0 ? direct : null;

        foreach (var kv in dict)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value.Count > 0 ? kv.Value : null;
        }
        return null;
    }
}

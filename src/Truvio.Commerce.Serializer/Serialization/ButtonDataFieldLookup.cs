using System.Collections.Concurrent;

namespace Truvio.Commerce.Serializer.Serialization;

/// <summary>
/// Engine issue #6: answers "which fields of this item type carry a ButtonData value?" so
/// every write site can reshape them via <see cref="ButtonDataNormalizer"/> before handing
/// the dictionary to <c>ItemEntry.DeserializeFrom</c>.
///
/// <para>
/// Split out of <see cref="ButtonDataNormalizer"/> because this half needs DW metadata: the
/// normalizer stays dependency-free and unit-tested, this one is the thin platform lookup.
/// </para>
/// </summary>
internal static class ButtonDataFieldLookup
{
    /// <summary>
    /// Per-item-type result cache. Only positive resolutions are cached — an item type whose
    /// metadata could not be read yet (replace mode deploys ItemType XML in the same run that
    /// writes the items) must be re-probed rather than pinned to an empty set for the process.
    /// </summary>
    private static readonly ConcurrentDictionary<string, IReadOnlySet<string>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// System names of the fields on <paramref name="itemType"/> whose editor is the DW button
    /// editor. Empty when the type has none, or when metadata cannot be read — a failed lookup
    /// must never break the write path; the field is then written unreshaped, exactly as it was
    /// before this fix.
    /// </summary>
    public static IReadOnlySet<string> For(string? itemType, Action<string>? log = null)
    {
        if (string.IsNullOrEmpty(itemType))
            return Empty;

        if (_cache.TryGetValue(itemType, out var cached))
            return cached;

        try
        {
            var typeMetadata = Dynamicweb.Content.Items.ItemManager.Metadata.GetItemType(itemType);
            if (typeMetadata == null)
                return Empty;

            var buttonFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // GetItemFields includes inherited fields (matches ItemTypeBySystemNameQuery).
            foreach (var field in Dynamicweb.Content.Items.ItemManager.Metadata.GetItemFields(typeMetadata))
            {
                if (!string.IsNullOrWhiteSpace(field.SystemName) &&
                    ButtonDataNormalizer.IsButtonEditor(field.Editor?.TypeName))
                {
                    buttonFields.Add(field.SystemName);
                }
            }

            _cache[itemType] = buttonFields;
            return buttonFields;
        }
        catch (Exception ex)
        {
            log?.Invoke($"  Could not read field metadata for item type '{itemType}' " +
                        $"({ex.Message}) — ButtonData fields are written unreshaped.");
            return Empty;
        }
    }

    /// <summary>
    /// Rewrites every ButtonData-typed entry of <paramref name="values"/> into the object shape
    /// the editor binds (blank-membered object for a clear). Values whose shape the normalizer
    /// does not recognise are left untouched rather than replaced by an invented object.
    /// Restricted to <paramref name="onlyFields"/> when supplied (merge fills only some entries).
    /// </summary>
    public static void Apply(
        string? itemType,
        Dictionary<string, object?> values,
        Action<string>? log = null,
        IReadOnlySet<string>? onlyFields = null)
    {
        var buttonDataFields = For(itemType, log);
        if (buttonDataFields.Count == 0) return;

        foreach (var fieldName in buttonDataFields)
        {
            if (onlyFields != null && !onlyFields.Contains(fieldName)) continue;
            if (!values.TryGetValue(fieldName, out var current)) continue;

            if (ButtonDataNormalizer.TryNormalize(current, out var normalized))
                values[fieldName] = normalized;
            else
                log?.Invoke($"  ButtonData field '{fieldName}' holds an unrecognised value shape — " +
                            "written as-is (it may not bind on the target).");
        }
    }

    private static readonly IReadOnlySet<string> Empty =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Test seam: drops cached resolutions so a test can re-probe metadata.</summary>
    internal static void ClearCache() => _cache.Clear();
}

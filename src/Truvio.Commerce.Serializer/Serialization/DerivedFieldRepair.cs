using System.Globalization;

namespace Truvio.Commerce.Serializer.Serialization;

/// <summary>
/// Pure decision logic for the derive-on-save repair pass (LRN-hosted-publish-05). Some
/// item-field values are recomputed by the platform when the item is saved — e.g. Swift's
/// <c>LogoWidth</c> is derived from the image's intrinsic dimensions in the same save that
/// writes <c>Image</c> + <c>LogoWidth</c>, so the authored width is overwritten with the
/// asset's natural width and the authored value is lost. This is not a failed field — the
/// field wrote, then was recomputed.
///
/// <para>
/// <see cref="Compute"/> answers: given the values authored in the payload and the values
/// read back AFTER the platform's save, which fields must be re-written to their authored
/// value? Kept free of any DW dependency so it is fully unit-testable; the DB read/second-save
/// wiring lives in <c>ContentDeserializer</c>.
/// </para>
/// </summary>
internal static class DerivedFieldRepair
{
    /// <summary>
    /// A field needs repair when its authored value is non-empty and the persisted (post-save)
    /// value differs from it. Returns the authored value to re-write, keyed by field name.
    /// Fields whose authored value is empty/null are never repaired (the platform default or a
    /// legitimately-derived value is left in place).
    /// </summary>
    public static Dictionary<string, object?> Compute(
        IReadOnlyDictionary<string, object?> authored,
        IReadOnlyDictionary<string, object?> persisted)
    {
        var repairs = new Dictionary<string, object?>();

        foreach (var kvp in authored)
        {
            if (IsEmpty(kvp.Value))
                continue;

            persisted.TryGetValue(kvp.Key, out var persistedValue);
            if (!ValuesEqual(kvp.Value, persistedValue))
                repairs[kvp.Key] = kvp.Value;
        }

        return repairs;
    }

    /// <summary>Empty = null, or a string that is empty/whitespace after trimming.</summary>
    internal static bool IsEmpty(object? value)
    {
        if (value is null) return true;
        if (value is string s) return string.IsNullOrWhiteSpace(s);
        return false;
    }

    /// <summary>
    /// Compare an authored value against a persisted one using a trimmed, culture-invariant
    /// string projection (case-insensitive). "200" == 200 (correctly persisted number) is
    /// equal; "200" vs 1405 (a derived overwrite) differs.
    /// </summary>
    internal static bool ValuesEqual(object? authored, object? persisted)
    {
        return string.Equals(Normalize(authored), Normalize(persisted), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(object? value)
    {
        if (value is null) return string.Empty;
        return value switch
        {
            string s => s.Trim(),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture).Trim(),
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }
}

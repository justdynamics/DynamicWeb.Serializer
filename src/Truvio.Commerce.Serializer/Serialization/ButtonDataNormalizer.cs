using System.Globalization;
using System.Text.Json;

namespace Truvio.Commerce.Serializer.Serialization;

/// <summary>
/// Engine issue #6 (Foundry LRN, Swift 2.4 / DW 10.28.3): the ButtonData editor
/// (<c>Dynamicweb.Content.Items.Editors.ButtonEditor</c>, value type
/// <c>Dynamicweb.Content.Items.Editors.ButtonData</c>) round-trips its value out as a JSON
/// <b>string</b>. Writing that string back — or an empty string to clear it — fails model
/// binding and the save <b>silently no-ops</b>: no error, prior value kept. Binding only
/// succeeds when the value is an <b>object</b>, and clearing needs an object whose members
/// are blank.
///
/// <para>
/// A serialize→deserialize round-trip therefore has to reshape every ButtonData-typed field
/// before the write: the read-back JSON string becomes the object it describes, and a
/// null/empty value (the shape source-wins produces for a field the payload does not assert)
/// becomes <see cref="Blank"/> rather than a null that would leave the stale target button
/// standing.
/// </para>
///
/// <para>
/// Kept free of any DW dependency so it is fully unit-testable; the item-type lookup that
/// decides WHICH fields are ButtonData-typed lives in <c>ContentDeserializer</c>, matching
/// the <see cref="DerivedFieldRepair"/> split.
/// </para>
/// </summary>
internal static class ButtonDataNormalizer
{
    /// <summary>Editor type whose fields carry a ButtonData value.</summary>
    public const string EditorTypeName = "Dynamicweb.Content.Items.Editors.ButtonEditor";

    /// <summary>
    /// The ButtonData members and the value a blank button carries for each. <c>LinkType</c>
    /// and <c>Style</c> are not free-form — they must stay at the editor's defaults or the
    /// bound object is rejected, so a "blank" button is blank in the two text members only.
    /// </summary>
    private static readonly (string Name, string Blank)[] Members =
    {
        ("SelectedValue", ""),
        ("Label", ""),
        ("Link", ""),
        ("LinkType", "page"),
        ("Style", "primary")
    };

    /// <summary>
    /// True when <paramref name="editorTypeName"/> names the button editor. Tolerates an
    /// assembly-qualified name (DW metadata carries both shapes depending on source).
    /// </summary>
    public static bool IsButtonEditor(string? editorTypeName)
    {
        if (string.IsNullOrWhiteSpace(editorTypeName)) return false;

        var typeName = editorTypeName.Split(',')[0].Trim();
        return string.Equals(typeName, EditorTypeName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A blank-membered ButtonData object — the shape that clears a button. Per the issue's
    /// acceptance criterion, a deserialize of a blanked ButtonData must re-read with
    /// <c>Label == ""</c> AND <c>Link == ""</c>.
    /// </summary>
    public static Dictionary<string, object?> Blank()
    {
        var blank = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, blankValue) in Members)
            blank[name] = blankValue;
        return blank;
    }

    /// <summary>
    /// Reshapes <paramref name="value"/> into the object the ButtonData editor binds.
    /// <list type="bullet">
    /// <item>null / empty / whitespace → <see cref="Blank"/> (the clear).</item>
    /// <item>JSON object string (the read-back shape) → that object, every known member
    /// present and stringified, unknown members preserved.</item>
    /// <item>an existing dictionary → the same normalization.</item>
    /// </list>
    /// Returns <c>false</c> for anything else (a non-JSON string, a JSON array, malformed
    /// JSON) — the caller then leaves the value untouched rather than inventing an object
    /// out of a shape it does not understand.
    /// </summary>
    public static bool TryNormalize(object? value, out Dictionary<string, object?> normalized)
    {
        if (value is null)
        {
            normalized = Blank();
            return true;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            normalized = FromMembers(dictionary.Select(kvp => (kvp.Key, kvp.Value)));
            return true;
        }

        var text = value as string ?? value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            normalized = Blank();
            return true;
        }

        text = text.Trim();
        if (!text.StartsWith('{'))
        {
            normalized = Blank();
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                normalized = Blank();
                return false;
            }

            normalized = FromMembers(document.RootElement
                .EnumerateObject()
                .Select(p => (p.Name, (object?)ReadScalar(p.Value))));
            return true;
        }
        catch (JsonException)
        {
            normalized = Blank();
            return false;
        }
    }

    /// <summary>
    /// Builds the bound object: every known member present (source value, else its blank
    /// default), plus any member the source carried that the engine does not know about —
    /// dropping those would silently discard editor state added by a newer platform.
    /// </summary>
    private static Dictionary<string, object?> FromMembers(IEnumerable<(string Name, object? Value)> source)
    {
        var result = Blank();

        foreach (var (name, value) in source)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            result[name] = Stringify(value) ?? (result.TryGetValue(name, out var blank) ? blank : "");
        }

        return result;
    }

    /// <summary>Unwraps a JSON scalar to the string / null the editor binds.</summary>
    private static string? ReadScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => element.GetString(),
        _ => element.GetRawText()
    };

    /// <summary>
    /// ButtonData members are strings on the wire — a bare number in hand-authored YAML
    /// (e.g. <c>SelectedValue: 77</c>) is stringified rather than rejected. Null stays null
    /// so the caller falls back to the member's blank default.
    /// </summary>
    private static string? Stringify(object? value) => value switch
    {
        null => null,
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}

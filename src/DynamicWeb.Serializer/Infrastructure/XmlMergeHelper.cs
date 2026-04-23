using System.Xml;
using System.Xml.Linq;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Phase 39 D-22..D-27 (see <c>.planning/phases/39-seed-mode-field-level-merge-.../39-02-PLAN.md</c>):
/// per-element XML merge for Seed-mode deserialization. Fills target XML elements that are
/// absent OR have null/empty/whitespace text, using source XML as the fill value pool.
/// Preserves any element present on target but absent from source (D-24 — Seed never strips).
/// </summary>
/// <remarks>
/// <para>
/// Element identity is by local-name, except when a child has a <c>name</c> attribute
/// (DW <c>&lt;Parameter name="X"&gt;</c> idiom used by
/// <c>EcomPayments.PaymentGatewayParameters</c> and <c>EcomShippings.ShippingServiceParameters</c>),
/// in which case the <c>name</c> attribute is the identity key. This matches the scheme already
/// used by <see cref="XmlFormatter.CompactWithMerge"/>.
/// </para>
/// <para>
/// Security (T-39-02-05): uses an <see cref="XmlReader"/> with <see cref="DtdProcessing.Prohibit"/>
/// to reject entity-expansion (billion-laughs) payloads. Malformed XML on either side returns
/// the target unchanged.
/// </para>
/// <para>
/// Security (T-39-02-03): merged XML is re-serialized via <see cref="XDocument.ToString()"/>,
/// so any injected SQL-like text inside XML content is escaped as XML text, not executed as SQL.
/// The merged XML value is bound into the enclosing SQL via <c>CommandBuilder {0}</c> placeholders
/// (see <see cref="Providers.SqlTable.SqlTableWriter.UpdateColumnSubset"/>) — no SQL escape path.
/// </para>
/// <para>
/// Dry-run (T-39-02-06): the <see cref="MergeWithDiagnostics"/> variant returns per-element
/// fill descriptions that include source values — do not emit those logs to untrusted sinks.
/// </para>
/// </remarks>
public static class XmlMergeHelper
{
    private static readonly XmlReaderSettings SafeSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = false,
        IgnoreWhitespace = false
    };

    /// <summary>Merge source XML into target XML per D-22/D-23/D-24. See type remarks.</summary>
    public static string? Merge(string? targetXml, string? sourceXml)
        => MergeWithDiagnostics(targetXml, sourceXml).mergedXml;

    /// <summary>
    /// Merge with per-element fill diagnostics for D-19 / D-26 dry-run logging.
    /// Returned <c>fills</c> strings describe each element-level change:
    /// <c>"element=Mail1SenderEmail: &lt;missing&gt; -> 'no-reply@x.com'"</c>.
    /// </summary>
    public static (string? mergedXml, IReadOnlyList<string> fills) MergeWithDiagnostics(
        string? targetXml, string? sourceXml)
    {
        var fills = new List<string>();

        // Pure-null boundary cases — mirror XmlFormatter.CompactWithMerge semantics.
        if (targetXml is null && sourceXml is null) return (null, fills);
        if (string.IsNullOrWhiteSpace(targetXml)) return (sourceXml, fills);
        if (string.IsNullOrWhiteSpace(sourceXml)) return (targetXml, fills);

        var targetDoc = TryParse(targetXml);
        var sourceDoc = TryParse(sourceXml);

        // Defensive parse: if either side fails to parse (malformed or prohibited DTD),
        // return target unchanged — no partial merges (T-39-02-05).
        if (targetDoc?.Root is null || sourceDoc?.Root is null)
            return (targetXml, fills);

        MergeElement(targetDoc.Root, sourceDoc.Root, fills);

        var hadDeclaration = targetXml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);
        string result;
        if (hadDeclaration && targetDoc.Declaration is not null)
            result = targetDoc.Declaration + targetDoc.ToString(SaveOptions.DisableFormatting);
        else
            result = targetDoc.ToString(SaveOptions.DisableFormatting);
        return (result, fills);
    }

    private static XDocument? TryParse(string xml)
    {
        try
        {
            using var sr = new StringReader(xml);
            using var xr = XmlReader.Create(sr, SafeSettings);
            return XDocument.Load(xr);
        }
        catch (XmlException) { return null; }
    }

    private static string GetKey(XElement el)
        => el.Attribute("name")?.Value ?? el.Name.LocalName;

    private static bool IsUnsetText(string? text)
        => string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// "Unset" leaf per D-22: no child elements AND text is null/empty/whitespace.
    /// Elements with children are never considered unset (the merge walks into them).
    /// </summary>
    private static bool IsUnsetLeafElement(XElement el)
        => !el.HasElements && IsUnsetText(el.Value);

    private static void MergeElement(XElement target, XElement source, List<string> fills)
    {
        // Attribute-level merge at this level.
        MergeAttributes(target, source, fills);

        // Leaf-leaf case: both target and source are leaves — fill target if unset.
        if (!target.HasElements && !source.HasElements)
        {
            if (IsUnsetText(target.Value) && !IsUnsetText(source.Value))
            {
                fills.Add($"element={GetKey(target)}: <empty> -> '{source.Value}'");
                target.Value = source.Value;
            }
            return;
        }

        // Index target children by identity key (case-insensitive). Duplicate keys keep the first —
        // DW <Parameter name="X"> documents never duplicate a name within a single parent.
        var targetByKey = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var tgtChild in target.Elements())
        {
            var k = GetKey(tgtChild);
            if (!targetByKey.ContainsKey(k))
                targetByKey[k] = tgtChild;
        }

        foreach (var srcChild in source.Elements())
        {
            var key = GetKey(srcChild);
            if (targetByKey.TryGetValue(key, out var tgtChild))
            {
                if (IsUnsetLeafElement(tgtChild))
                {
                    // Target leaf is unset (absent/empty/whitespace) — replace with source content.
                    fills.Add($"element={key}: <missing-or-empty> -> '{srcChild.Value}'");
                    tgtChild.ReplaceWith(new XElement(srcChild));
                }
                else
                {
                    // Recurse for non-leaf or set-leaf children.
                    MergeElement(tgtChild, srcChild, fills);
                }
            }
            else
            {
                // Element absent on target — add it wholesale (D-22 missing fill).
                fills.Add($"element={key}: <missing> -> '{srcChild.Value}'");
                target.Add(new XElement(srcChild));
            }
        }
        // D-24: target-only elements preserved by doing nothing for them.
    }

    private static void MergeAttributes(XElement target, XElement source, List<string> fills)
    {
        foreach (var srcAttr in source.Attributes())
        {
            // Ignore namespace-declaration attributes — XDocument handles those automatically,
            // and we don't want xmlns:* entries showing up in diagnostics.
            if (srcAttr.IsNamespaceDeclaration) continue;

            // Skip the "name" attribute — it's an identity key, not a payload to merge.
            if (srcAttr.Name.LocalName == "name" && srcAttr.Name.NamespaceName == "") continue;

            var tgtAttr = target.Attribute(srcAttr.Name);
            if (tgtAttr is null)
            {
                fills.Add($"attribute={srcAttr.Name.LocalName}: <missing> -> '{srcAttr.Value}'");
                target.SetAttributeValue(srcAttr.Name, srcAttr.Value);
            }
            else if (IsUnsetText(tgtAttr.Value) && !IsUnsetText(srcAttr.Value))
            {
                fills.Add($"attribute={srcAttr.Name.LocalName}: <empty> -> '{srcAttr.Value}'");
                tgtAttr.Value = srcAttr.Value;
            }
        }
    }
}

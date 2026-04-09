using System.Xml;
using System.Xml.Linq;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Formats XML strings for readable YAML output (pretty-print) and compact DB storage (compact).
/// Handles null, empty, whitespace, non-XML, and malformed XML gracefully — all pass through unchanged.
/// CRLF line endings are normalized to LF in PrettyPrint output to ensure ForceStringScalarEmitter
/// selects ScalarStyle.Literal (YAML literal block scalar) instead of DoubleQuoted.
/// </summary>
public static class XmlFormatter
{
    /// <summary>
    /// Pretty-prints compact XML into indented multi-line form with LF line endings.
    /// Preserves XML declaration when present in the original.
    /// Returns the original string unchanged for null, empty, whitespace, non-XML, or malformed XML.
    /// </summary>
    public static string? PrettyPrint(string? rawXml)
    {
        if (string.IsNullOrWhiteSpace(rawXml))
            return rawXml;

        try
        {
            var xdoc = XDocument.Parse(rawXml);
            var hadDeclaration = rawXml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

            string result;
            if (hadDeclaration && xdoc.Declaration != null)
                result = xdoc.Declaration.ToString() + "\n" + xdoc.ToString(SaveOptions.None);
            else
                result = xdoc.ToString(SaveOptions.None);

            // Normalize CRLF to LF — required for ForceStringScalarEmitter to select Literal block style.
            // Safe per XML spec section 2.11: all conformant parsers normalize line endings to LF.
            result = result.Replace("\r\n", "\n").Replace("\r", "\n");

            return result;
        }
        catch (XmlException)
        {
            return rawXml;
        }
    }

    /// <summary>
    /// Removes XML elements matching the specified names (case-insensitive) from the XML string.
    /// Returns the original string unchanged for null, empty, whitespace, non-XML, or malformed XML.
    /// The result is pretty-printed with LF line endings (same as PrettyPrint output).
    /// </summary>
    public static string? RemoveElements(string? xml, IEnumerable<string>? elementNames)
    {
        if (string.IsNullOrWhiteSpace(xml) || elementNames == null)
            return xml;

        var nameSet = new HashSet<string>(elementNames, StringComparer.OrdinalIgnoreCase);
        if (nameSet.Count == 0)
            return xml;

        try
        {
            var xdoc = XDocument.Parse(xml);
            var hadDeclaration = xml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

            // Remove all matching elements (collect first to avoid modifying during enumeration)
            var toRemove = xdoc.Descendants()
                .Where(e => nameSet.Contains(e.Name.LocalName))
                .ToList();
            foreach (var el in toRemove)
                el.Remove();

            string result;
            if (hadDeclaration && xdoc.Declaration != null)
                result = xdoc.Declaration.ToString() + "\n" + xdoc.ToString(SaveOptions.None);
            else
                result = xdoc.ToString(SaveOptions.None);

            return result.Replace("\r\n", "\n").Replace("\r", "\n");
        }
        catch (XmlException)
        {
            return xml;
        }
    }

    /// <summary>
    /// Compacts incoming XML and merges in any root-level child elements from the existing XML
    /// that are not present in the incoming XML. This is a "blind merge" — the target keeps any
    /// elements the source didn't send, regardless of why they were absent (excludeXmlElements,
    /// different source config, etc.). Incoming elements always win for elements present in both.
    /// Returns compact single-line XML ready for DB storage.
    /// </summary>
    public static string? CompactWithMerge(string? incomingXml, string? existingXml)
    {
        if (string.IsNullOrWhiteSpace(incomingXml))
            return Compact(incomingXml);

        if (string.IsNullOrWhiteSpace(existingXml))
            return Compact(incomingXml);

        try
        {
            var incomingDoc = XDocument.Parse(incomingXml);
            var existingDoc = XDocument.Parse(existingXml);

            if (incomingDoc.Root == null || existingDoc.Root == null)
                return Compact(incomingXml);

            // Collect element names present in incoming (case-insensitive)
            var incomingNames = new HashSet<string>(
                incomingDoc.Root.Elements().Select(e => e.Name.LocalName),
                StringComparer.OrdinalIgnoreCase);

            // Preserve root-level children from existing that are absent in incoming
            foreach (var el in existingDoc.Root.Elements())
            {
                if (!incomingNames.Contains(el.Name.LocalName))
                    incomingDoc.Root.Add(new XElement(el));
            }

            var hadDeclaration = incomingXml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

            string result;
            if (hadDeclaration && incomingDoc.Declaration != null)
                result = incomingDoc.Declaration.ToString() + incomingDoc.ToString(SaveOptions.DisableFormatting);
            else
                result = incomingDoc.ToString(SaveOptions.DisableFormatting);

            return result;
        }
        catch (XmlException)
        {
            return Compact(incomingXml);
        }
    }

    /// <summary>
    /// Compacts pretty-printed XML into a single-line form suitable for database storage.
    /// Preserves XML declaration when present in the original.
    /// Returns the original string unchanged for null, empty, whitespace, non-XML, or malformed XML.
    /// </summary>
    public static string? Compact(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return xml;

        try
        {
            var xdoc = XDocument.Parse(xml);
            var hadDeclaration = xml.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);

            string result;
            if (hadDeclaration && xdoc.Declaration != null)
                result = xdoc.Declaration.ToString() + xdoc.ToString(SaveOptions.DisableFormatting);
            else
                result = xdoc.ToString(SaveOptions.DisableFormatting);

            return result;
        }
        catch (XmlException)
        {
            return xml;
        }
    }
}

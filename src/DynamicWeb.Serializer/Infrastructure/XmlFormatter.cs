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

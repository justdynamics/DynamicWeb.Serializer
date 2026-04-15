using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DynamicWeb.Serializer.Infrastructure;
using DynamicWeb.Serializer.Providers.SqlTable;
using Dynamicweb.Data;

namespace DynamicWeb.Serializer.AdminUI.Infrastructure;

/// <summary>
/// Discovers XML types and their element names from Page and Paragraph tables.
/// Used by the Embedded XML admin screens to populate type lists and element exclusion selectors.
/// </summary>
public class XmlTypeDiscovery
{
    private readonly ISqlExecutor _sqlExecutor;

    public XmlTypeDiscovery(ISqlExecutor sqlExecutor)
    {
        _sqlExecutor = sqlExecutor;
    }

    /// <summary>
    /// Discover distinct XML type names from Page (UrlDataProvider) and Paragraph (Module) tables.
    /// </summary>
    public HashSet<string> DiscoverXmlTypes()
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Page URL data provider types
        var cb1 = new CommandBuilder();
        cb1.Add("SELECT DISTINCT PageUrlDataProvider FROM Page WHERE PageUrlDataProvider != '' AND PageUrlDataProvider IS NOT NULL");
        using (var reader = _sqlExecutor.ExecuteReader(cb1))
        {
            while (reader.Read())
            {
                var typeName = reader["PageUrlDataProvider"]?.ToString();
                if (!string.IsNullOrEmpty(typeName))
                    types.Add(typeName);
            }
        }

        // Paragraph module system names
        var cb2 = new CommandBuilder();
        cb2.Add("SELECT DISTINCT ParagraphModuleSystemName FROM Paragraph WHERE ParagraphModuleSystemName != '' AND ParagraphModuleSystemName IS NOT NULL");
        using (var reader = _sqlExecutor.ExecuteReader(cb2))
        {
            while (reader.Read())
            {
                var typeName = reader["ParagraphModuleSystemName"]?.ToString();
                if (!string.IsNullOrEmpty(typeName))
                    types.Add(typeName);
            }
        }

        return types;
    }

    /// <summary>
    /// Discover distinct root-level XML element names for a given type by parsing live XML blobs.
    /// Malformed XML blobs are skipped without throwing.
    /// </summary>
    public HashSet<string> DiscoverElementsForType(string typeName)
    {
        var elements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Defense-in-depth: validate typeName to prevent SQL injection
        // CommandBuilder does not support parameterized queries, so we use regex validation
        if (!Regex.IsMatch(typeName, @"^[A-Za-z0-9_., ]+$"))
            return elements;

        // Page URL data provider parameters
        var cb1 = new CommandBuilder();
        cb1.Add($"SELECT TOP 50 PageUrlDataProviderParameters FROM Page WHERE PageUrlDataProvider = '{typeName}' AND PageUrlDataProviderParameters IS NOT NULL AND PageUrlDataProviderParameters != ''");
        using (var reader = _sqlExecutor.ExecuteReader(cb1))
        {
            while (reader.Read())
            {
                var xml = reader["PageUrlDataProviderParameters"]?.ToString();
                ParseXmlElements(xml, elements);
            }
        }

        // Paragraph module settings
        var cb2 = new CommandBuilder();
        cb2.Add($"SELECT TOP 50 ParagraphModuleSettings FROM Paragraph WHERE ParagraphModuleSystemName = '{typeName}' AND ParagraphModuleSettings IS NOT NULL AND ParagraphModuleSettings != ''");
        using (var reader = _sqlExecutor.ExecuteReader(cb2))
        {
            while (reader.Read())
            {
                var xml = reader["ParagraphModuleSettings"]?.ToString();
                ParseXmlElements(xml, elements);
            }
        }

        return elements;
    }

    /// <summary>
    /// Returns a pretty-printed sample XML blob for a given type, or null if none found.
    /// Queries Page first, then Paragraph.
    /// </summary>
    public string? GetSampleXml(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (!Regex.IsMatch(typeName, @"^[A-Za-z0-9_., ]+$"))
            return null;

        // Try Page first
        var cb1 = new CommandBuilder();
        cb1.Add($"SELECT TOP 1 PageUrlDataProviderParameters FROM Page WHERE PageUrlDataProvider = '{typeName}' AND PageUrlDataProviderParameters IS NOT NULL AND PageUrlDataProviderParameters != ''");
        using (var reader = _sqlExecutor.ExecuteReader(cb1))
        {
            if (reader.Read())
            {
                var xml = reader["PageUrlDataProviderParameters"]?.ToString();
                if (!string.IsNullOrWhiteSpace(xml))
                    return XmlFormatter.PrettyPrint(xml);
            }
        }

        // Try Paragraph
        var cb2 = new CommandBuilder();
        cb2.Add($"SELECT TOP 1 ParagraphModuleSettings FROM Paragraph WHERE ParagraphModuleSystemName = '{typeName}' AND ParagraphModuleSettings IS NOT NULL AND ParagraphModuleSettings != ''");
        using (var reader = _sqlExecutor.ExecuteReader(cb2))
        {
            if (reader.Read())
            {
                var xml = reader["ParagraphModuleSettings"]?.ToString();
                if (!string.IsNullOrWhiteSpace(xml))
                    return XmlFormatter.PrettyPrint(xml);
            }
        }

        return null;
    }

    private static void ParseXmlElements(string? xml, HashSet<string> elements)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return;

        try
        {
            var doc = XDocument.Parse(xml);
            if (doc.Root == null)
                return;

            var children = doc.Root.Elements().ToList();
            var distinctNames = children.Select(e => e.Name.LocalName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (distinctNames.Count <= 1 && children.Count > 0)
            {
                // All children share one element name (e.g., <Parameter name="X">)
                // Extract the "name" attribute values instead — these are the meaningful identifiers
                foreach (var el in children)
                {
                    var nameAttr = el.Attribute("name")?.Value;
                    if (!string.IsNullOrEmpty(nameAttr))
                        elements.Add(nameAttr);
                }
            }
            else
            {
                // Children have distinct element names (e.g., <IndexQuery>, <TrackQueries>)
                foreach (var el in children)
                    elements.Add(el.Name.LocalName);
            }
        }
        catch (XmlException)
        {
            // Skip malformed XML blobs silently
        }
    }
}

using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
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
        cb1.Add("SELECT DISTINCT PageUrlDataProviderType FROM Page WHERE PageUrlDataProviderType != '' AND PageUrlDataProviderType IS NOT NULL");
        using (var reader = _sqlExecutor.ExecuteReader(cb1))
        {
            while (reader.Read())
            {
                var typeName = reader["PageUrlDataProviderType"]?.ToString();
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
        if (!Regex.IsMatch(typeName, @"^[A-Za-z0-9_.]+$"))
            return elements;

        // Page URL data provider parameters
        var cb1 = new CommandBuilder();
        cb1.Add($"SELECT TOP 50 PageUrlDataProviderParameters FROM Page WHERE PageUrlDataProviderType = '{typeName}' AND PageUrlDataProviderParameters IS NOT NULL AND PageUrlDataProviderParameters != ''");
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

    private static void ParseXmlElements(string? xml, HashSet<string> elements)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return;

        try
        {
            var doc = XDocument.Parse(xml);
            if (doc.Root != null)
            {
                foreach (var el in doc.Root.Elements())
                    elements.Add(el.Name.LocalName);
            }
        }
        catch (XmlException)
        {
            // Skip malformed XML blobs silently
        }
    }
}

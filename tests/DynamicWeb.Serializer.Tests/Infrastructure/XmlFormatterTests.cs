using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

public class XmlFormatterTests
{
    // -----------------------------------------------------------------------
    // PrettyPrint — null / empty / whitespace
    // -----------------------------------------------------------------------

    [Fact]
    public void PrettyPrint_Null_ReturnsNull()
    {
        Assert.Null(XmlFormatter.PrettyPrint(null));
    }

    [Fact]
    public void PrettyPrint_Empty_ReturnsEmpty()
    {
        Assert.Equal("", XmlFormatter.PrettyPrint(""));
    }

    [Fact]
    public void PrettyPrint_Whitespace_ReturnsUnchanged()
    {
        Assert.Equal("  ", XmlFormatter.PrettyPrint("  "));
    }

    // -----------------------------------------------------------------------
    // PrettyPrint — non-XML / malformed
    // -----------------------------------------------------------------------

    [Fact]
    public void PrettyPrint_NonXml_ReturnsUnchanged()
    {
        Assert.Equal("not xml", XmlFormatter.PrettyPrint("not xml"));
    }

    [Fact]
    public void PrettyPrint_MalformedXml_ReturnsUnchanged()
    {
        const string malformed = "<broken>";
        Assert.Equal(malformed, XmlFormatter.PrettyPrint(malformed));
    }

    // -----------------------------------------------------------------------
    // PrettyPrint — valid XML with declaration (DW moduleSettings sample)
    // -----------------------------------------------------------------------

    [Fact]
    public void PrettyPrint_ModuleSettings_ProducesIndentedXmlWithDeclaration()
    {
        const string input =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Settings>" +
            "<module systemName=\"Dynamicweb.Frontend.Navigation\">" +
            "<param name=\"StartLevel\">0</param>" +
            "<param name=\"EndLevel\">5</param>" +
            "</module>" +
            "</Settings>";

        var result = XmlFormatter.PrettyPrint(input)!;

        // Starts with declaration
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result);
        // Multi-line
        Assert.Contains("\n", result);
        // Indented children
        Assert.Contains("  <module", result);
        Assert.Contains("    <param", result);
    }

    [Fact]
    public void PrettyPrint_UrlDataProviderParameters_ProducesIndentedXmlWithDeclaration()
    {
        const string input =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Parameters addin=\"Dynamicweb.Frontend.QueryPublisher\">" +
            "<Param name=\"Query\">dwcontent</Param>" +
            "</Parameters>";

        var result = XmlFormatter.PrettyPrint(input)!;

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result);
        Assert.Contains("  <Param", result);
    }

    // -----------------------------------------------------------------------
    // PrettyPrint — valid XML without declaration
    // -----------------------------------------------------------------------

    [Fact]
    public void PrettyPrint_NoDeclaration_ProducesIndentedXmlWithoutDeclaration()
    {
        const string input = "<Settings><param>1</param></Settings>";

        var result = XmlFormatter.PrettyPrint(input)!;

        Assert.DoesNotContain("<?xml", result);
        Assert.StartsWith("<Settings>", result);
        Assert.Contains("  <param>", result);
    }

    // -----------------------------------------------------------------------
    // PrettyPrint — CRLF normalization
    // -----------------------------------------------------------------------

    [Fact]
    public void PrettyPrint_NeverContainsCR()
    {
        const string input =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Settings><param>value</param></Settings>";

        var result = XmlFormatter.PrettyPrint(input)!;

        Assert.DoesNotContain("\r", result);
    }

    // -----------------------------------------------------------------------
    // Compact — null / empty / non-XML
    // -----------------------------------------------------------------------

    [Fact]
    public void Compact_Null_ReturnsNull()
    {
        Assert.Null(XmlFormatter.Compact(null));
    }

    [Fact]
    public void Compact_Empty_ReturnsEmpty()
    {
        Assert.Equal("", XmlFormatter.Compact(""));
    }

    [Fact]
    public void Compact_NonXml_ReturnsUnchanged()
    {
        Assert.Equal("not xml", XmlFormatter.Compact("not xml"));
    }

    // -----------------------------------------------------------------------
    // Compact — produces single-line
    // -----------------------------------------------------------------------

    [Fact]
    public void Compact_PrettyPrintedXml_ReturnsSingleLine()
    {
        const string prettyXml =
            "<Settings>\n  <param>1</param>\n</Settings>";

        var result = XmlFormatter.Compact(prettyXml)!;

        Assert.DoesNotContain("\n", result);
        Assert.DoesNotContain("\r", result);
    }

    [Fact]
    public void Compact_PreservesDeclarationWhenPresent()
    {
        const string prettyXml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Settings>\n  <param>1</param>\n</Settings>";

        var result = XmlFormatter.Compact(prettyXml)!;

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result);
        Assert.DoesNotContain("\n", result);
    }

    // -----------------------------------------------------------------------
    // Round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public void RoundTrip_CompactPrettyPrint_SemanticallyIdentical()
    {
        const string original =
            "<Settings><module systemName=\"Nav\"><param name=\"X\">1</param></module></Settings>";

        var prettyPrinted = XmlFormatter.PrettyPrint(original);
        var compacted = XmlFormatter.Compact(prettyPrinted);

        // Parse both and compare — semantically identical
        var originalDoc = System.Xml.Linq.XDocument.Parse(original);
        var roundTripDoc = System.Xml.Linq.XDocument.Parse(compacted!);

        Assert.Equal(
            originalDoc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
            roundTripDoc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
    }

    [Fact]
    public void RoundTrip_WithDeclaration_DeclarationSurvives()
    {
        const string original =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<Settings><param>1</param></Settings>";

        var prettyPrinted = XmlFormatter.PrettyPrint(original);
        var compacted = XmlFormatter.Compact(prettyPrinted);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", compacted);
    }

    // -----------------------------------------------------------------------
    // RemoveElements — null / empty / non-XML
    // -----------------------------------------------------------------------

    [Fact]
    public void RemoveElements_Null_ReturnsNull()
    {
        Assert.Null(XmlFormatter.RemoveElements(null, new[] { "sort" }));
    }

    [Fact]
    public void RemoveElements_Empty_ReturnsEmpty()
    {
        Assert.Equal("", XmlFormatter.RemoveElements("", new[] { "sort" }));
    }

    [Fact]
    public void RemoveElements_NonXml_ReturnsUnchanged()
    {
        Assert.Equal("not xml", XmlFormatter.RemoveElements("not xml", new[] { "sort" }));
    }

    // -----------------------------------------------------------------------
    // RemoveElements — element removal
    // -----------------------------------------------------------------------

    [Fact]
    public void RemoveElements_MatchingElements_RemovesThem()
    {
        const string input = "<Settings><sort>5</sort><pagesize>10</pagesize><title>Test</title></Settings>";

        var result = XmlFormatter.RemoveElements(input, new[] { "sort", "pagesize" })!;

        Assert.Contains("<title>", result);
        Assert.DoesNotContain("<sort>", result);
        Assert.DoesNotContain("<pagesize>", result);
    }

    [Fact]
    public void RemoveElements_CaseInsensitive()
    {
        const string input = "<Settings><sort>5</sort><title>Test</title></Settings>";

        var result = XmlFormatter.RemoveElements(input, new[] { "Sort" })!;

        Assert.DoesNotContain("<sort>", result);
        Assert.Contains("<title>", result);
    }

    [Fact]
    public void RemoveElements_EmptyNamesList_ReturnsUnchanged()
    {
        const string input = "<Settings><sort>5</sort></Settings>";

        var result = XmlFormatter.RemoveElements(input, Array.Empty<string>())!;

        Assert.Contains("<sort>", result);
    }

    [Fact]
    public void RemoveElements_NestedElements_RemovesAll()
    {
        const string input =
            "<Settings><group><sort>5</sort></group><sort>10</sort></Settings>";

        var result = XmlFormatter.RemoveElements(input, new[] { "sort" })!;

        Assert.DoesNotContain("<sort>", result);
        Assert.Contains("<group", result);
    }
}

using DynamicWeb.Serializer.Infrastructure;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Infrastructure;

/// <summary>
/// Phase 39 D-22..D-27 (39-02-PLAN Task 2): per-element XML merge rules.
/// Target element is "unset" when absent OR text is null/empty/whitespace — source
/// fills. Target-only elements preserved (D-24). Defensive parse with DTD prohibited
/// for billion-laughs hardening (T-39-02-05).
/// </summary>
[Trait("Category", "Phase39")]
public class XmlMergeHelperTests
{
    // -----------------------------------------------------------------------
    // D-22 / D-23: element-level fill rules
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_ElementMissingOnTarget_FillsFromSource()
    {
        const string target = "<Root><A>1</A></Root>";
        const string source = "<Root><A>1</A><B>2</B></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        Assert.Contains("<B>2</B>", merged);
        Assert.Contains("<A>1</A>", merged);
    }

    [Fact]
    public void Merge_ElementEmptyOnTarget_FillsFromSource()
    {
        const string target = "<Root><A></A></Root>";
        const string source = "<Root><A>1</A></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        Assert.Contains(">1<", merged); // <A>1</A> after merge
    }

    [Fact]
    public void Merge_ElementWhitespaceOnTarget_FillsFromSource()
    {
        const string target = "<Root><A>   </A></Root>";
        const string source = "<Root><A>x</A></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        Assert.Contains(">x<", merged);
        Assert.DoesNotContain("   ", merged!);
    }

    [Fact]
    public void Merge_ElementSetOnTarget_PreservesTarget()
    {
        const string target = "<Root><A>customer</A></Root>";
        const string source = "<Root><A>seed</A></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        Assert.Contains(">customer<", merged);
        Assert.DoesNotContain(">seed<", merged!);
    }

    // -----------------------------------------------------------------------
    // D-24: target-only element preservation
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_TargetOnlyElement_PreservedUntouched()
    {
        const string target = "<Root><A>1</A><X>target-only</X></Root>";
        const string source = "<Root><A>1</A></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        Assert.Contains(">target-only<", merged);
    }

    // -----------------------------------------------------------------------
    // Attribute merge rules (same D-22 rule applied to attribute values)
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_AttributeMissingOnTarget_FillsFromSource()
    {
        const string target = "<Root><A>1</A></Root>";
        const string source = "<Root lang=\"en\"><A>1</A></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        Assert.Contains("lang=\"en\"", merged);
    }

    [Fact]
    public void Merge_AttributeSetOnTarget_PreservesTarget()
    {
        const string target = "<Root lang=\"fr\"><A>1</A></Root>";
        const string source = "<Root lang=\"en\"><A>1</A></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        Assert.Contains("lang=\"fr\"", merged);
        Assert.DoesNotContain("lang=\"en\"", merged!);
    }

    // -----------------------------------------------------------------------
    // Nested element recursion — per-leaf D-22
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_NestedElements_MergedPerLeaf()
    {
        const string target = "<Root><Outer><A>keep</A><B></B></Outer></Root>";
        const string source = "<Root><Outer><A>should-not-overwrite</A><B>fill</B><C>new</C></Outer></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        Assert.Contains(">keep<", merged);            // A preserved (target set)
        Assert.Contains(">fill<", merged);            // B filled (target empty)
        Assert.Contains(">new<", merged);             // C added (target missing)
        Assert.DoesNotContain("should-not-overwrite", merged!);
    }

    // -----------------------------------------------------------------------
    // DW idiom: <Parameter name="X"> identity by name attribute
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_NameAttributeIdentity_EcomPaymentsShape()
    {
        const string target =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderName\">Existing Name</Parameter>" +
            "</Settings>";
        const string source =
            "<Settings>" +
            "<Parameter name=\"Mail1SenderName\">Seed Name</Parameter>" +
            "<Parameter name=\"Mail1SenderEmail\">no-reply@x</Parameter>" +
            "</Settings>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        // Target's Mail1SenderName preserved (already set).
        Assert.Contains(">Existing Name<", merged);
        Assert.DoesNotContain(">Seed Name<", merged!);
        // Mail1SenderEmail filled from source.
        Assert.Contains("name=\"Mail1SenderEmail\"", merged);
        Assert.Contains(">no-reply@x<", merged);
    }

    // -----------------------------------------------------------------------
    // Null / whitespace inputs
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_BothNull_ReturnsNull()
    {
        var merged = XmlMergeHelper.Merge(null, null);
        Assert.Null(merged);
    }

    [Fact]
    public void Merge_TargetNullSourceNonNull_ReturnsSource()
    {
        const string source = "<Root><A>1</A></Root>";
        var merged = XmlMergeHelper.Merge(null, source);

        Assert.Equal(source, merged);
    }

    [Fact]
    public void Merge_TargetNonNullSourceNull_ReturnsTarget()
    {
        const string target = "<Root><A>1</A></Root>";
        var merged = XmlMergeHelper.Merge(target, null);

        Assert.Equal(target, merged);
    }

    // -----------------------------------------------------------------------
    // Defensive parse — malformed XML returns target unchanged
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_MalformedTargetXml_ReturnsTargetUnchanged()
    {
        const string target = "<broken";
        const string source = "<Root><A>1</A></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.Equal(target, merged);
    }

    [Fact]
    public void Merge_MalformedSourceXml_ReturnsTargetUnchanged()
    {
        const string target = "<Root><A>1</A></Root>";
        const string source = "<broken";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.Equal(target, merged);
    }

    // -----------------------------------------------------------------------
    // Security hardening — T-39-02-05 (billion-laughs DTD)
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_DtdPayload_IsProhibited()
    {
        // Billion-laughs style DTD payload — XmlReader with DtdProcessing.Prohibit
        // rejects this; XmlMergeHelper should catch the XmlException and treat the
        // source as malformed -> return target unchanged (or null when target is null).
        const string target = "<Root><A>1</A></Root>";
        const string sourceWithDtd =
            "<!DOCTYPE Root [<!ENTITY lol \"LOL\">]><Root><A>&lol;</A></Root>";

        var merged = XmlMergeHelper.Merge(target, sourceWithDtd);

        // Source was rejected as malformed → target returned unchanged.
        Assert.Equal(target, merged);
    }

    // -----------------------------------------------------------------------
    // T-39-02-03 — SQL-like XML text is safely escaped as XML text
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_SourceWithSqlLikeText_EscapesAsXmlText()
    {
        const string target = "<Root><A></A></Root>";
        const string source = "<Root><A>'; DROP TABLE Users; --</A></Root>";

        var merged = XmlMergeHelper.Merge(target, source);

        Assert.NotNull(merged);
        // The dangerous text is embedded as XML text content, not as a sibling SQL
        // fragment. The re-serialized XML must still parse as XML (round-trip safety).
        Assert.Contains("DROP TABLE Users", merged);
        // And the merged document remains well-formed XML — System.Xml.Linq would
        // throw if it weren't.
        var parsed = System.Xml.Linq.XDocument.Parse(merged!);
        Assert.NotNull(parsed.Root);
    }

    // -----------------------------------------------------------------------
    // MergeWithDiagnostics — fills list reporting
    // -----------------------------------------------------------------------

    [Fact]
    public void MergeWithDiagnostics_ReportsFilledElements()
    {
        const string target = "<Root><A></A></Root>";
        const string source = "<Root><A>x</A><B>y</B></Root>";

        var (merged, fills) = XmlMergeHelper.MergeWithDiagnostics(target, source);

        Assert.NotNull(merged);
        Assert.NotEmpty(fills);
        // Either filled-A + missing-B, or whichever the planner chose — we assert
        // structural presence, not exact wording.
        Assert.Contains(fills, f => f.Contains("A"));
        Assert.Contains(fills, f => f.Contains("B"));
    }

    [Fact]
    public void MergeWithDiagnostics_AllSet_ReturnsEmptyFillsList()
    {
        const string target = "<Root><A>1</A><B>2</B></Root>";
        const string source = "<Root><A>1</A><B>2</B></Root>";

        var (_, fills) = XmlMergeHelper.MergeWithDiagnostics(target, source);

        Assert.Empty(fills);
    }
}

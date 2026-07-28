using Truvio.Commerce.Serializer.Serialization;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Serialization;

/// <summary>
/// Engine issue #6: the ButtonData editor round-trips its value to the client as a JSON
/// string; writing that string back — or an empty string to clear it — fails model binding
/// and the save silently no-ops. Binding only succeeds when the value is an OBJECT, and a
/// clear needs a blank-membered object. These tests pin the write shape the deserializer
/// produces for a ButtonData-typed field.
/// </summary>
public class ButtonDataNormalizerTests
{
    // -------------------------------------------------------------------------
    // Clearing case (issue #6 acceptance criterion)
    // -------------------------------------------------------------------------

    [Fact]
    public void Blank_YieldsBlankLabelAndLink()
    {
        var blank = ButtonDataNormalizer.Blank();

        Assert.Equal("", blank["Label"]);
        Assert.Equal("", blank["Link"]);
        Assert.Equal("", blank["SelectedValue"]);
        Assert.Equal("page", blank["LinkType"]);
        Assert.Equal("primary", blank["Style"]);
    }

    [Fact]
    public void TryNormalize_Null_ProducesBlankObjectNotNull()
    {
        // Source-wins nulls out fields absent from the YAML; a null write silently no-ops
        // on a ButtonData field, so the clear must go out as a blank-membered object.
        Assert.True(ButtonDataNormalizer.TryNormalize(null, out var normalized));

        Assert.Equal("", normalized["Label"]);
        Assert.Equal("", normalized["Link"]);
    }

    [Fact]
    public void TryNormalize_EmptyString_ProducesBlankObject()
    {
        Assert.True(ButtonDataNormalizer.TryNormalize("", out var normalized));

        Assert.Equal("", normalized["Label"]);
        Assert.Equal("", normalized["Link"]);
    }

    [Fact]
    public void TryNormalize_WhitespaceString_ProducesBlankObject()
    {
        Assert.True(ButtonDataNormalizer.TryNormalize("   ", out var normalized));

        Assert.Equal("", normalized["Label"]);
        Assert.Equal("", normalized["Link"]);
    }

    // -------------------------------------------------------------------------
    // Round-trip case (issue #6 acceptance criterion)
    // -------------------------------------------------------------------------

    [Fact]
    public void TryNormalize_RoundTrippedJsonString_ProducesObjectWithMembers()
    {
        const string readBack =
            """{"SelectedValue":"77","Label":"Shop now","Link":"","LinkType":"page","Style":"primary"}""";

        Assert.True(ButtonDataNormalizer.TryNormalize(readBack, out var normalized));

        Assert.Equal("77", normalized["SelectedValue"]);
        Assert.Equal("Shop now", normalized["Label"]);
        Assert.Equal("", normalized["Link"]);
        Assert.Equal("page", normalized["LinkType"]);
        Assert.Equal("primary", normalized["Style"]);
    }

    [Fact]
    public void TryNormalize_JsonStringMissingMembers_FillsBlankDefaults()
    {
        Assert.True(ButtonDataNormalizer.TryNormalize(
            """{"Label":"Read more"}""", out var normalized));

        Assert.Equal("Read more", normalized["Label"]);
        Assert.Equal("", normalized["Link"]);
        Assert.Equal("", normalized["SelectedValue"]);
        Assert.Equal("page", normalized["LinkType"]);
        Assert.Equal("primary", normalized["Style"]);
    }

    [Fact]
    public void TryNormalize_JsonStringWithNullMember_ProducesBlankForThatMember()
    {
        Assert.True(ButtonDataNormalizer.TryNormalize(
            """{"Label":"X","Link":null}""", out var normalized));

        Assert.Equal("", normalized["Link"]);
    }

    [Fact]
    public void TryNormalize_JsonStringWithNumericSelectedValue_StringifiesIt()
    {
        // The link resolver rewrites "SelectedValue": "N" as a string, but hand-authored
        // YAML can carry a bare number — the editor binds strings.
        Assert.True(ButtonDataNormalizer.TryNormalize(
            """{"SelectedValue":77}""", out var normalized));

        Assert.Equal("77", normalized["SelectedValue"]);
    }

    [Fact]
    public void TryNormalize_PreservesUnknownMembers()
    {
        Assert.True(ButtonDataNormalizer.TryNormalize(
            """{"Label":"X","Icon":"arrow-right"}""", out var normalized));

        Assert.Equal("arrow-right", normalized["Icon"]);
        Assert.Equal("X", normalized["Label"]);
    }

    [Fact]
    public void TryNormalize_AlreadyAnObject_NormalizesInPlaceShape()
    {
        var source = new Dictionary<string, object?>
        {
            ["Label"] = "Shop now",
            ["SelectedValue"] = 77
        };

        Assert.True(ButtonDataNormalizer.TryNormalize(source, out var normalized));

        Assert.Equal("Shop now", normalized["Label"]);
        Assert.Equal("77", normalized["SelectedValue"]);
        Assert.Equal("", normalized["Link"]);
        Assert.Equal("page", normalized["LinkType"]);
    }

    // -------------------------------------------------------------------------
    // Shapes the normalizer must NOT invent an object for
    // -------------------------------------------------------------------------

    [Fact]
    public void TryNormalize_NonJsonString_ReturnsFalse()
    {
        Assert.False(ButtonDataNormalizer.TryNormalize("Shop now", out _));
    }

    [Fact]
    public void TryNormalize_JsonArray_ReturnsFalse()
    {
        Assert.False(ButtonDataNormalizer.TryNormalize("""["a","b"]""", out _));
    }

    [Fact]
    public void TryNormalize_MalformedJsonObject_ReturnsFalse()
    {
        Assert.False(ButtonDataNormalizer.TryNormalize("""{"Label":""", out _));
    }

    // -------------------------------------------------------------------------
    // Editor identification
    // -------------------------------------------------------------------------

    [Fact]
    public void IsButtonEditor_MatchesTheDwEditorTypeName()
    {
        Assert.True(ButtonDataNormalizer.IsButtonEditor("Dynamicweb.Content.Items.Editors.ButtonEditor"));
    }

    [Fact]
    public void IsButtonEditor_MatchesAssemblyQualifiedName()
    {
        Assert.True(ButtonDataNormalizer.IsButtonEditor(
            "Dynamicweb.Content.Items.Editors.ButtonEditor, Dynamicweb, Version=10.28.1.0"));
    }

    [Fact]
    public void IsButtonEditor_RejectsOtherEditors()
    {
        Assert.False(ButtonDataNormalizer.IsButtonEditor("Dynamicweb.Content.Items.Editors.LinkEditor"));
        Assert.False(ButtonDataNormalizer.IsButtonEditor(null));
        Assert.False(ButtonDataNormalizer.IsButtonEditor(""));
    }
}

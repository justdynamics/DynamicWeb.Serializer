using Truvio.Commerce.Serializer.Serialization;
using Xunit;

namespace Truvio.Commerce.Serializer.Tests.Serialization;

/// <summary>
/// Regression tests for the item-field link-resolution guard (ContentDeserializer.ResolveLinkFields).
///
/// Defect: deserializing a base layer that adds one language-paired page (EN + NL) deterministically
/// corrupted item content — a hard PRIMARY KEY violation in one area AND a SILENT item-Title "smear"
/// in areas that reported "0 failed". Root cause: the post-write link-resolution pass rewrote EVERY
/// serialized item field, INCLUDING the item's own numeric "Id" system field. The resolver remaps bare
/// numeric page ids source→target (LinkEditor stores "121" instead of "Default.aspx?ID=121"), so an
/// item whose "Id" happened to match a remapped page id had its primary key rewritten. The following
/// item.Save() then persisted the row under a DIFFERENT id, overwriting a neighbouring item's fields
/// (the smear) and orphaning the original row; the churned id space also lagged Dynamicweb's item-id
/// allocator, which surfaced later as the duplicate-key insert.
///
/// Fix: ResolveLinkFields excludes ItemSystemFields (Id, ItemInstanceType, Sort, ...) from resolution —
/// item identity is never treated as a page link. These tests pin that behaviour.
/// </summary>
public class ContentDeserializerLinkResolutionTests
{
    // Same shape as InternalLinkResolverTests: source page id -> target page id.
    // Note 1 -> 901: a bare "1" would be remapped if it were ever resolved.
    private static readonly Dictionary<int, int> Map = new()
    {
        { 123, 456 },
        { 1, 901 },
        { 12, 902 },
        { 23, 999 },
    };

    private static InternalLinkResolver CreateResolver() => new(Map, null, null);

    [Fact]
    public void ResolveLinkFields_NeverRewritesItemIdSystemField()
    {
        // The item's own primary key "Id" = "23" matches a mapped source id (23 -> 999).
        // If it were resolved, Save() would write the row under id 999 and smear a neighbour.
        var fields = new Dictionary<string, object?>
        {
            ["Id"] = "23",
            ["Title"] = "Overview",
            ["ButtonLink"] = "Default.aspx?ID=123",
        };

        var changed = ContentDeserializer.ResolveLinkFields(fields, CreateResolver(), k => $"test|{k}");

        // Identity is untouched...
        Assert.False(changed.ContainsKey("Id"), "The item's Id system field must never be link-resolved.");
        // ...content link IS resolved...
        Assert.Equal("Default.aspx?ID=456", changed["ButtonLink"]);
        // ...and unchanged content is not written back.
        Assert.False(changed.ContainsKey("Title"), "Only fields whose value changed should be returned.");
    }

    [Fact]
    public void ResolveLinkFields_ExcludesAllSystemFields_EvenWhenValueMatchesAMappedId()
    {
        var fields = new Dictionary<string, object?>
        {
            ["Id"] = "1",                 // 1 -> 901 if (wrongly) resolved
            ["Sort"] = "12",              // 12 -> 902 if (wrongly) resolved
            ["ItemInstanceType"] = "23",  // 23 -> 999 if (wrongly) resolved
            ["GlobalRecordPageGuid"] = "1",
            ["MasterParagraphGuid"] = "1",
        };

        var changed = ContentDeserializer.ResolveLinkFields(fields, CreateResolver(), k => $"test|{k}");

        Assert.Empty(changed);
    }

    [Fact]
    public void ResolveLinkFields_ResolvesContentLinkFields()
    {
        var fields = new Dictionary<string, object?>
        {
            ["RichText"] = "<a href=\"Default.aspx?ID=12\">go</a>",
            ["RawPageId"] = "23",   // bare numeric LinkEditor value 23 -> 999
            ["Plain"] = "no links here",
        };

        var changed = ContentDeserializer.ResolveLinkFields(fields, CreateResolver(), k => $"test|{k}");

        Assert.Equal("<a href=\"Default.aspx?ID=902\">go</a>", changed["RichText"]);
        Assert.Equal("999", changed["RawPageId"]);
        Assert.False(changed.ContainsKey("Plain"));
    }
}

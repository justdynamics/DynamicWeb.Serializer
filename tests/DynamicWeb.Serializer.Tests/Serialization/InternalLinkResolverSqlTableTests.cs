using DynamicWeb.Serializer.Serialization;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Serialization;

/// <summary>
/// Phase 37-05 / LINK-02 pass 2 — <see cref="InternalLinkResolver.ResolveInStringColumn"/>
/// is the SqlTable-column-oriented alias for the existing <c>ResolveLinks</c> API.
/// The underlying regex / map logic is unchanged from the content path; these tests
/// document the intended call pattern (string column value → rewritten value) and
/// guard against accidental behavior drift in the alias.
/// </summary>
public class InternalLinkResolverSqlTableTests
{
    private readonly Dictionary<int, int> _map = new()
    {
        { 5862, 9000 },
        { 100, 200 }
    };

    [Fact]
    public void ResolveInStringColumn_RewritesDefaultAspxId()
    {
        var resolver = new InternalLinkResolver(_map);
        var rewritten = resolver.ResolveInStringColumn("Default.aspx?ID=5862");
        Assert.Equal("Default.aspx?ID=9000", rewritten);
    }

    [Fact]
    public void ResolveInStringColumn_MultipleRefsInOneValue_AllRewritten()
    {
        var resolver = new InternalLinkResolver(_map);
        var rewritten = resolver.ResolveInStringColumn(
            "Default.aspx?ID=5862 and Default.aspx?ID=100");
        Assert.Equal("Default.aspx?ID=9000 and Default.aspx?ID=200", rewritten);
    }

    [Fact]
    public void ResolveInStringColumn_UnresolvedId_LogsWarning_ValuePreserved()
    {
        var warnings = new List<string>();
        var resolver = new InternalLinkResolver(_map, warnings.Add);

        var rewritten = resolver.ResolveInStringColumn("Default.aspx?ID=999999");

        Assert.Equal("Default.aspx?ID=999999", rewritten);
        Assert.Contains(warnings, w => w.Contains("999999") && w.Contains("Unresolvable"));
    }

    [Fact]
    public void ResolveInStringColumn_NullValue_ReturnsNull()
    {
        var resolver = new InternalLinkResolver(_map);
        Assert.Null(resolver.ResolveInStringColumn(null));
    }

    [Fact]
    public void ResolveInStringColumn_EmptyValue_ReturnsEmpty()
    {
        var resolver = new InternalLinkResolver(_map);
        Assert.Equal("", resolver.ResolveInStringColumn(""));
    }

    [Fact]
    public void ResolveInStringColumn_NoMatch_ReturnsUnchanged()
    {
        var resolver = new InternalLinkResolver(_map);
        Assert.Equal("plain text", resolver.ResolveInStringColumn("plain text"));
    }

    [Fact]
    public void ResolveInStringColumn_RawNumericInMap_Rewritten()
    {
        // Existing behavior: pure-numeric input that matches a key in the map is rewritten.
        var resolver = new InternalLinkResolver(_map);
        Assert.Equal("200", resolver.ResolveInStringColumn("100"));
    }

    [Fact]
    public void ResolveInStringColumn_OutputContainingQuotes_IsAPureString()
    {
        // T-37-05-03 note: the rewritten string is placed back into the SqlTable row
        // dictionary and flows through the existing parameterized MERGE — the test doesn't
        // need to verify SQL safety (CommandBuilder handles that). This just pins down
        // the output type: resolver returns a string, never something that leaks into
        // the SQL composition layer.
        var map = new Dictionary<int, int> { { 1, 2 } };
        var resolver = new InternalLinkResolver(map);
        var raw = "Default.aspx?ID=1 -- with '; trailing chars";
        var rewritten = resolver.ResolveInStringColumn(raw);
        Assert.Equal("Default.aspx?ID=2 -- with '; trailing chars", rewritten);
        Assert.IsType<string>(rewritten);
    }
}

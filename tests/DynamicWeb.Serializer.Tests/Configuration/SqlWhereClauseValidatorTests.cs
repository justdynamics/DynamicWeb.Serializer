using DynamicWeb.Serializer.Configuration;
using Xunit;

namespace DynamicWeb.Serializer.Tests.Configuration;

public class SqlWhereClauseValidatorTests
{
    private static IReadOnlySet<string> Cols(params string[] names) =>
        new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

    private readonly SqlWhereClauseValidator _v = new();

    // ---------------------------------------------------------------------
    // Positive cases
    // ---------------------------------------------------------------------

    [Fact]
    public void Validate_SimpleEquality_Succeeds()
    {
        _v.Validate("AccessUserType = 2", Cols("AccessUserType"));
    }

    [Fact]
    public void Validate_StringLiteral_Succeeds()
    {
        _v.Validate("AccessUserUserName = 'Admin'", Cols("AccessUserUserName"));
    }

    [Fact]
    public void Validate_InClause_Succeeds()
    {
        _v.Validate(
            "AccessUserUserName IN ('Admin','Editors')",
            Cols("AccessUserUserName"));
    }

    [Fact]
    public void Validate_CompositeWithAnd_Succeeds()
    {
        _v.Validate(
            "AccessUserType = 2 AND AccessUserUserName = 'Admin'",
            Cols("AccessUserType", "AccessUserUserName"));
    }

    [Fact]
    public void Validate_CompositeWithOr_Succeeds()
    {
        _v.Validate(
            "AccessUserType = 2 OR AccessUserType = 3",
            Cols("AccessUserType"));
    }

    [Fact]
    public void Validate_IsNullAndNotNull_Succeeds()
    {
        _v.Validate("AreaName IS NOT NULL", Cols("AreaName"));
        _v.Validate("AreaName IS NULL", Cols("AreaName"));
    }

    [Fact]
    public void Validate_LikeOperator_Succeeds()
    {
        _v.Validate(
            "AccessUserUserName LIKE 'admin%'",
            Cols("AccessUserUserName"));
    }

    [Fact]
    public void Validate_BetweenOperator_Succeeds()
    {
        _v.Validate(
            "AccessUserType BETWEEN 1 AND 5",
            Cols("AccessUserType"));
    }

    // ---------------------------------------------------------------------
    // Injection rejection cases (SEED-002 corpus)
    // ---------------------------------------------------------------------

    [Fact]
    public void Validate_Semicolon_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _v.Validate("AccessUserType = 2; DROP TABLE X", Cols("AccessUserType")));
        Assert.Contains(";", ex.Message);
    }

    [Fact]
    public void Validate_DashDashComment_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _v.Validate("AccessUserType = 2 -- comment", Cols("AccessUserType")));
        Assert.Contains("--", ex.Message);
    }

    [Fact]
    public void Validate_SlashStarComment_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _v.Validate("AccessUserType = 2 /* comment */", Cols("AccessUserType")));
        Assert.Contains("/*", ex.Message);
    }

    [Fact]
    public void Validate_SubSelect_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _v.Validate("(SELECT 1) = 1", Cols("X")));
        Assert.Contains("SELECT", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_ExecKeyword_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _v.Validate("EXEC sp_who", Cols("X")));
        Assert.True(
            ex.Message.Contains("EXEC", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("sp_executesql", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("banned"));
    }

    [Fact]
    public void Validate_XpPrefix_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _v.Validate("xp_cmdshell 'whoami'", Cols("X")));
        Assert.Contains("xp_", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_UpdateKeyword_Rejected()
    {
        Assert.Throws<InvalidOperationException>(
            () => _v.Validate("X = 1 UPDATE Y SET Z = 1", Cols("X")));
    }

    [Fact]
    public void Validate_DeleteKeyword_Rejected()
    {
        Assert.Throws<InvalidOperationException>(
            () => _v.Validate("X = 1 DELETE FROM Y", Cols("X")));
    }

    [Fact]
    public void Validate_DropKeyword_Rejected()
    {
        Assert.Throws<InvalidOperationException>(
            () => _v.Validate("X = 1 DROP TABLE Y", Cols("X")));
    }

    [Fact]
    public void Validate_UnknownIdentifier_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => _v.Validate("UnknownColumn = 1", Cols("AccessUserType")));
        Assert.Contains("UnknownColumn", ex.Message);
    }

    [Fact]
    public void Validate_UnterminatedString_Rejected()
    {
        Assert.Throws<InvalidOperationException>(
            () => _v.Validate("AreaName = 'unterminated", Cols("AreaName")));
    }

    [Fact]
    public void Validate_EmptyString_Rejected()
    {
        Assert.Throws<InvalidOperationException>(
            () => _v.Validate("", Cols()));
        Assert.Throws<InvalidOperationException>(
            () => _v.Validate("   ", Cols()));
    }

    [Fact]
    public void Validate_StringLiteralWithBannedKeyword_DoesNotLeakOut()
    {
        // Banned keyword inside a string literal should be stripped before keyword check.
        // A literal like 'SELECT me' does not make the clause dangerous.
        // We validate identifier only — this must pass since only the string literal contains SELECT.
        // NOTE: our guard is conservative: BannedTokens are substring-matched case-insensitively,
        // but literal content is OK to contain allowed operators/values. This covers the realistic
        // DW config case `Name = 'Admin Select Group'`.
        _v.Validate("AreaName = 'Admin Select Group'", Cols("AreaName"));
    }

    [Fact]
    public void Validate_SemicolonInsideStringLiteral_Rejected()
    {
        // Even though the ; is inside a literal, we reject it conservatively — the validator
        // scans literal tokens via substring match. If a real-world config needs a literal with
        // a semicolon, the user must add it to excludeFields instead. This is the safer default.
        Assert.Throws<InvalidOperationException>(
            () => _v.Validate("AreaName = 'not;ok'", Cols("AreaName")));
    }

    [Fact]
    public void Validate_PathologicalInput_CompletesInReasonableTime()
    {
        // 10KB of benign clause → validator must stay O(n).
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 2500; i++)
            sb.Append("X = 1 AND ");
        sb.Append("X = 1");
        var clause = sb.ToString();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        _v.Validate(clause, Cols("X"));
        sw.Stop();

        // Generous budget — must complete well under a second on any machine.
        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Validate took {sw.ElapsedMilliseconds}ms — pathological-input check failed");
    }
}

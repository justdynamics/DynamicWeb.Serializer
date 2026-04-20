namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// Tokenizes a WHERE-clause string and validates it against a set of allowed column
/// names. Rejects SQL injection patterns (<c>;</c>, <c>--</c>, <c>/*</c>, <c>xp_</c>,
/// <c>sp_executesql</c>, subselects) and DDL/DML keywords. Scope: single-table WHERE
/// clauses with literal values. No joins, subqueries, or function calls.
/// Per SEED-002 + FILTER-01.
/// </summary>
public class SqlWhereClauseValidator
{
    // Literal substrings rejected unconditionally (case-insensitive substring match).
    private static readonly string[] BannedTokens =
    {
        ";", "--", "/*", "*/", "xp_", "sp_executesql"
    };

    // Whole-word keyword bans (case-insensitive).
    private static readonly HashSet<string> BannedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "UPDATE", "DELETE", "INSERT", "MERGE", "EXEC", "EXECUTE", "DROP",
        "TRUNCATE", "ALTER", "CREATE", "GRANT", "REVOKE", "UNION", "INTO",
        "WAITFOR", "SHUTDOWN"
    };

    // Whole-word keywords that are SAFE inside a where clause and therefore not
    // required to appear in the allowedColumns set.
    private static readonly HashSet<string> SafeOperatorKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND", "OR", "NOT", "IN", "IS", "NULL", "LIKE", "BETWEEN", "TRUE", "FALSE"
    };

    /// <summary>
    /// Validate the given WHERE clause against the predicate table's allowed column set.
    /// Throws <see cref="InvalidOperationException"/> with a message naming the bad token
    /// on any violation. Returns silently on success.
    /// </summary>
    public void Validate(string whereClause, IReadOnlySet<string> allowedColumns)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
            throw new InvalidOperationException("WHERE clause is empty.");

        // 1. Literal banned-token scan over the raw clause (catches ;, --, /*, xp_, sp_executesql
        //    even when hidden inside string literals — conservative, safer than silent allow).
        foreach (var banned in BannedTokens)
        {
            if (whereClause.Contains(banned, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"WHERE clause contains banned token '{banned}': {whereClause}");
        }

        // 2. Tokenize with string literals elided — prevents false positives on literal values
        //    like `'Admin Select Group'` (whose content would otherwise collide with identifier/keyword bans).
        var stripped = StripStringLiterals(whereClause);
        var tokens = stripped.Split(
            new[] { ' ', '\t', '\n', '\r', '(', ')', ',', '=', '<', '>', '!' },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawToken in tokens)
        {
            var token = rawToken.Trim().Trim('\'');
            if (token.Length == 0) continue;

            // Whole-word keyword ban.
            if (BannedKeywords.Contains(token))
                throw new InvalidOperationException(
                    $"WHERE clause contains banned keyword '{token}': {whereClause}");

            // Identifier-like tokens (start with letter or underscore, not a parseable integer,
            // not an operator keyword) must be in allowedColumns.
            if (IsIdentifierLike(token) && !SafeOperatorKeywords.Contains(token))
            {
                if (!allowedColumns.Contains(token))
                    throw new InvalidOperationException(
                        $"WHERE clause references unknown identifier '{token}'. " +
                        $"Not a column on the target table. Check INFORMATION_SCHEMA. Clause: {whereClause}");
            }
        }
    }

    private static bool IsIdentifierLike(string token)
    {
        if (token.Length == 0) return false;
        var c0 = token[0];
        if (!char.IsLetter(c0) && c0 != '_') return false;
        // Numerics are not identifiers.
        if (int.TryParse(token, out _)) return false;
        return true;
    }

    /// <summary>
    /// Replace every single-quoted string literal (quotes and contents) with a single
    /// space so downstream tokenization cannot mistake literal content for identifiers
    /// or keywords. Throws if the clause has an unterminated literal.
    /// </summary>
    private static string StripStringLiterals(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        var inString = false;
        foreach (var c in input)
        {
            if (c == '\'')
            {
                inString = !inString;
                if (!inString) sb.Append(' '); // end of literal — emit separator
                continue;
            }
            if (!inString) sb.Append(c);
        }

        if (inString)
            throw new InvalidOperationException("Unterminated string literal in WHERE clause.");

        return sb.ToString();
    }
}

using System.Text;
using System.Text.RegularExpressions;

namespace SignalBeam.ServiceDefaults.Observability;

/// <summary>
/// Derives a low-cardinality summary ("{operation} {collection}") from SQL text, following the
/// OpenTelemetry database conventions for <c>db.query.summary</c>, <c>db.operation.name</c> and
/// <c>db.collection.name</c>. Npgsql leaves these empty for plain SQL commands (everything EF Core
/// and raw ADO.NET commands send), which makes every span read "postgresql".
/// </summary>
/// <remarks>
/// A heuristic, not a SQL parser. The text is first reduced in one linear pass to the outer part of
/// its first statement: comments removed, string literals and everything inside parentheses blanked
/// out, input capped at <see cref="MaxScanLength"/>. The operation is the first keyword (or, for a
/// <c>WITH</c> query, the first top-level DML keyword) and the collection is the first top-level
/// table after FROM / INTO / UPDATE / TRUNCATE.
/// Known limits: a <c>WITH</c> query that selects from its CTE reports the CTE name.
/// </remarks>
public static partial class SqlQuerySummary
{
    /// <summary>Operation and target are always near the start; never scan more than this.</summary>
    public const int MaxScanLength = 8192;

    private const int MatchTimeoutMs = 100;

    private static readonly HashSet<string> Operations = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "COPY", "CALL", "WITH",
        "CREATE", "ALTER", "DROP", "TRUNCATE", "BEGIN", "COMMIT", "ROLLBACK",
        "SAVEPOINT", "RELEASE", "SET", "SHOW", "LOCK", "VACUUM", "ANALYZE",
    };

    public readonly record struct SqlSummary(string Operation, string? Collection, string Summary);

    /// <summary>Returns <c>null</c> when the text has no recognisable leading SQL operation.</summary>
    /// <exception cref="RegexMatchTimeoutException">Only under extreme CPU starvation; callers on hot paths should catch it.</exception>
    public static SqlSummary? Parse(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return null;
        }

        var text = OuterStatement(sql);
        var first = FirstKeyword().Match(text);
        var keyword = first.Groups["kw"].Value;
        if (!first.Success || !Operations.Contains(keyword))
        {
            return null;
        }

        var operation = keyword.ToUpperInvariant();
        var statement = text;
        if (operation == "WITH")
        {
            // CTE bodies are blanked out, so the first top-level DML keyword is the main statement.
            var dml = DmlKeyword().Match(text, first.Index + first.Length);
            if (dml.Success)
            {
                operation = dml.Value.ToUpperInvariant();
                statement = text[dml.Index..];
            }
        }

        var collection = FindCollection(operation, statement);
        return new SqlSummary(operation, collection, collection is null ? operation : $"{operation} {collection}");
    }

    private static string? FindCollection(string operation, string statement)
    {
        var match = operation switch
        {
            "INSERT" => InsertTarget().Match(statement),
            "UPDATE" => UpdateTarget().Match(statement),
            "DELETE" or "SELECT" or "WITH" => FromTarget().Match(statement),
            "TRUNCATE" or "LOCK" => TruncateTarget().Match(statement),
            _ => Match.Empty,
        };

        return match.Success ? match.Groups["name"].Value.Replace("\"", string.Empty, StringComparison.Ordinal) : null;
    }

    /// <summary>
    /// Linear pass producing the outer part of the first statement: comments dropped, string
    /// literals and parenthesised content replaced by spaces (parentheses kept), quoted identifiers
    /// kept, cut at the first top-level ';'.
    /// </summary>
    internal static string OuterStatement(string sql)
    {
        var length = Math.Min(sql.Length, MaxScanLength);
        var sb = new StringBuilder(length);
        var depth = 0;
        var i = 0;

        while (i < length)
        {
            var c = sql[i];
            var next = i + 1 < length ? sql[i + 1] : '\0';

            if (c == '-' && next == '-')
            {
                i = SkipUntil(sql, i + 2, length, "\n");
                sb.Append(' ');
                continue;
            }

            if (c == '/' && next == '*')
            {
                i = SkipUntil(sql, i + 2, length, "*/");
                sb.Append(' ');
                continue;
            }

            if (c == '\'')
            {
                // String literal ('' is an escaped quote): never kept, so values can't leak into names.
                i = SkipQuoted(sql, i + 1, length, '\'');
                sb.Append(' ');
                continue;
            }

            if (c == '"')
            {
                var end = SkipQuoted(sql, i + 1, length, '"');
                if (depth == 0)
                {
                    sb.Append(sql, i, end - i);
                }

                i = end;
                continue;
            }

            switch (c)
            {
                case '(':
                    if (depth++ == 0)
                    {
                        sb.Append('(');
                    }

                    break;
                case ')':
                    if (depth > 0 && --depth == 0)
                    {
                        sb.Append(')');
                    }

                    break;
                case ';' when depth == 0:
                    return sb.ToString();
                default:
                    if (depth == 0)
                    {
                        sb.Append(c);
                    }

                    break;
            }

            i++;
        }

        return sb.ToString();
    }

    private static int SkipUntil(string s, int start, int end, string terminator)
    {
        var idx = s.IndexOf(terminator, start, end - start, StringComparison.Ordinal);
        return idx < 0 ? end : idx + terminator.Length;
    }

    private static int SkipQuoted(string s, int start, int end, char quote)
    {
        var i = start;
        while (i < end)
        {
            if (s[i] == quote)
            {
                if (i + 1 < end && s[i + 1] == quote)
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return end;
    }

    private const string Identifier = @"(?<name>(?:""[^""]+""|[A-Za-z_][\w$]*)(?:\.(?:""[^""]+""|[A-Za-z_][\w$]*))?)";

    [GeneratedRegex(@"^\s*(?<kw>[A-Za-z]+)", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex FirstKeyword();

    [GeneratedRegex(@"\b(?:SELECT|INSERT|UPDATE|DELETE|MERGE)\b", RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex DmlKeyword();

    [GeneratedRegex(@"\bINSERT\s+INTO\s+" + Identifier, RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex InsertTarget();

    [GeneratedRegex(@"\bUPDATE\s+(?:ONLY\s+)?" + Identifier, RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex UpdateTarget();

    [GeneratedRegex(@"\bFROM\s+(?:ONLY\s+)?" + Identifier, RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex FromTarget();

    [GeneratedRegex(@"\b(?:TRUNCATE|LOCK)\s+(?:TABLE\s+)?(?:ONLY\s+)?" + Identifier, RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex TruncateTarget();
}

using System.Text.RegularExpressions;

namespace SignalBeam.ServiceDefaults.Observability;

/// <summary>
/// Derives a low-cardinality summary ("{operation} {collection}") from SQL text, following the
/// OpenTelemetry database conventions for <c>db.query.summary</c>, <c>db.operation.name</c> and
/// <c>db.collection.name</c>. Npgsql leaves these empty for plain SQL commands (everything EF Core
/// and Dapper send), which makes every span read "postgresql".
/// </summary>
/// <remarks>
/// A heuristic, not a SQL parser: it looks at the first statement only and picks the first table
/// after FROM / INTO / UPDATE. That is enough for readable span names and stable call signatures.
/// </remarks>
public static partial class SqlQuerySummary
{
    private const int MatchTimeoutMs = 100;

    private static readonly HashSet<string> Operations = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "MERGE", "COPY", "CALL",
        "CREATE", "ALTER", "DROP", "TRUNCATE", "BEGIN", "COMMIT", "ROLLBACK",
        "SAVEPOINT", "RELEASE", "SET", "SHOW", "LOCK", "VACUUM", "ANALYZE",
    };

    public readonly record struct Result(string Operation, string? Collection)
    {
        public string Summary => Collection is null ? Operation : $"{Operation} {Collection}";
    }

    public static Result? Parse(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return null;
        }

        var text = StripComments().Replace(sql, " ").TrimStart();
        var first = FirstKeyword().Match(text);
        if (!first.Success)
        {
            return null;
        }

        var operation = first.Value.ToUpperInvariant();
        var statement = text;
        if (operation == "WITH")
        {
            // The statement's operation is the first DML keyword after the CTE definitions,
            // and its target is looked up in that main statement, not inside the CTEs.
            var dml = DmlKeyword().Match(text, first.Length);
            if (dml.Success)
            {
                operation = dml.Groups["op"].Value.ToUpperInvariant();
                statement = text[dml.Groups["op"].Index..];
            }
        }

        if (!Operations.Contains(operation) && operation != "WITH")
        {
            return null;
        }

        return new Result(operation, FindCollection(operation, statement));
    }

    private static string? FindCollection(string operation, string text)
    {
        var match = operation switch
        {
            "INSERT" => InsertTarget().Match(text),
            "UPDATE" => UpdateTarget().Match(text),
            "DELETE" or "SELECT" or "WITH" => FromTarget().Match(text),
            "TRUNCATE" or "LOCK" => TruncateTarget().Match(text),
            _ => Match.Empty,
        };

        return match.Success ? NormalizeIdentifier(match.Groups["name"].Value) : null;
    }

    private static string NormalizeIdentifier(string name) => name.Replace("\"", string.Empty, StringComparison.Ordinal);

    [GeneratedRegex(@"--[^\n]*|/\*.*?\*/", RegexOptions.Singleline, MatchTimeoutMs)]
    private static partial Regex StripComments();

    [GeneratedRegex(@"^[A-Za-z]+", RegexOptions.None, MatchTimeoutMs)]
    private static partial Regex FirstKeyword();

    [GeneratedRegex(@"\)\s*(?<op>SELECT|INSERT|UPDATE|DELETE|MERGE)\b", RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex DmlKeyword();

    private const string Identifier = @"(?<name>(?:""[^""]+""|[A-Za-z_][\w$]*)(?:\.(?:""[^""]+""|[A-Za-z_][\w$]*))?)";

    [GeneratedRegex(@"\bINSERT\s+INTO\s+" + Identifier, RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex InsertTarget();

    [GeneratedRegex(@"\bUPDATE\s+(?:ONLY\s+)?" + Identifier, RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex UpdateTarget();

    [GeneratedRegex(@"\bFROM\s+(?:ONLY\s+)?" + Identifier, RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex FromTarget();

    [GeneratedRegex(@"\b(?:TRUNCATE|LOCK)\s+(?:TABLE\s+)?(?:ONLY\s+)?" + Identifier, RegexOptions.IgnoreCase, MatchTimeoutMs)]
    private static partial Regex TruncateTarget();
}

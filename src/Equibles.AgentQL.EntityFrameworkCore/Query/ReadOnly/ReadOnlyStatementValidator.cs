namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

// Whitelists the statement shapes the executor is willing to run under
// ReadOnly mode. Only SELECT / VALUES / TABLE / WITH (with read-shaped CTE
// bodies and outer body) are accepted; everything else — transaction
// control, DML, DDL, permissions, session settings, anonymous blocks,
// stored procedure calls — is rejected before the connection is opened.
// This is provider-agnostic and closes the SQL Server / Oracle gap from
// issues #108 / #110 where the DBMS itself cannot be configured to refuse
// writes in-band.
//
// The validator runs on the output of QueryExecutor.SanitizeQuery, so it
// sees the SQL with comments removed and whitespace normalised but with
// every quoted run preserved verbatim. A forbidden keyword that appears
// inside a string literal ('please UPDATE...') or a quoted identifier
// ("UPDATE_TIME") is therefore not treated as a statement verb.
//
// CTE bodies are validated recursively — `WITH x AS (INSERT INTO ...)
// SELECT * FROM x` is rejected because PG's writable CTEs would otherwise
// be a trivial bypass.
internal static class ReadOnlyStatementValidator
{
    private static readonly HashSet<string> AllowedReadVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT",
        "VALUES",
        "TABLE",
    };

    // Returns null when the SQL is acceptable; otherwise a human-readable
    // explanation of which statement was rejected. The whole batch is
    // rejected on the first violation — partial execution under ReadOnly is
    // worse than a clean refusal.
    public static string Validate(string sanitizedSql)
    {
        if (string.IsNullOrWhiteSpace(sanitizedSql))
            return null;

        foreach (var statement in SplitStatements(sanitizedSql))
        {
            var violation = ValidateStatement(statement);
            if (violation != null)
                return violation;
        }

        return null;
    }

    private static string ValidateStatement(string statement)
    {
        var verb = ReadIdentifier(statement, 0, out var afterVerb);
        if (verb == null)
            return null;

        if (AllowedReadVerbs.Contains(verb))
            return null;

        if (!verb.Equals("WITH", StringComparison.OrdinalIgnoreCase))
            return $"Statement '{verb.ToUpperInvariant()}' is not permitted in ReadOnly mode";

        return ValidateWithStatement(statement, afterVerb);
    }

    // Walks the CTE definitions of a WITH-led statement, recursively
    // validating each CTE body, then validates the outer body verb.
    private static string ValidateWithStatement(string sql, int pos)
    {
        var maybeRecursive = ReadIdentifier(sql, pos, out var afterMaybe);
        if (
            maybeRecursive != null
            && maybeRecursive.Equals("RECURSIVE", StringComparison.OrdinalIgnoreCase)
        )
            pos = afterMaybe;

        while (true)
        {
            var name = ReadIdentifier(sql, pos, out var afterName);
            if (name == null)
                return "WITH statement is malformed";
            pos = afterName;

            pos = SkipWhitespace(sql, pos);
            if (pos < sql.Length && sql[pos] == '(')
                pos = SkipBalancedParens(sql, pos);

            var asKeyword = ReadIdentifier(sql, pos, out var afterAs);
            if (asKeyword == null || !asKeyword.Equals("AS", StringComparison.OrdinalIgnoreCase))
                return "WITH statement is malformed";
            pos = afterAs;

            // PG 12+ accepts [NOT] MATERIALIZED between AS and the CTE body.
            var modifier = ReadIdentifier(sql, pos, out var afterModifier);
            if (modifier != null && modifier.Equals("NOT", StringComparison.OrdinalIgnoreCase))
            {
                pos = afterModifier;
                var materialized = ReadIdentifier(sql, pos, out var afterMaterialized);
                if (
                    materialized != null
                    && materialized.Equals("MATERIALIZED", StringComparison.OrdinalIgnoreCase)
                )
                    pos = afterMaterialized;
            }
            else if (
                modifier != null
                && modifier.Equals("MATERIALIZED", StringComparison.OrdinalIgnoreCase)
            )
            {
                pos = afterModifier;
            }

            pos = SkipWhitespace(sql, pos);
            if (pos >= sql.Length || sql[pos] != '(')
                return "WITH statement is malformed";

            // Recursively validate the CTE body as its own statement, then
            // skip past the parens to continue the walk.
            var bodyEnd = SkipBalancedParens(sql, pos);
            var bodyInner = sql.Substring(pos + 1, bodyEnd - pos - 2);
            var nestedViolation = ValidateStatement(bodyInner);
            if (nestedViolation != null)
                return nestedViolation;
            pos = bodyEnd;

            pos = SkipWhitespace(sql, pos);
            if (pos < sql.Length && sql[pos] == ',')
            {
                pos++;
                continue;
            }
            break;
        }

        var outerVerb = ReadIdentifier(sql, pos, out _);
        if (outerVerb == null)
            return "WITH statement has no body";
        if (
            outerVerb.Equals("WITH", StringComparison.OrdinalIgnoreCase)
            || AllowedReadVerbs.Contains(outerVerb)
        )
            return ValidateStatement(sql.Substring(pos));

        return $"WITH ... {outerVerb.ToUpperInvariant()} is not permitted in ReadOnly mode";
    }

    // Splits the SQL into statements on top-level ';'. Quoted runs are
    // skipped so a ';' inside a string literal or a PG dollar-quoted body
    // does not act as a statement boundary.
    private static IEnumerable<string> SplitStatements(string sql)
    {
        var start = 0;
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (c == '\'' || c == '"')
                i = SkipQuoted(sql, i, c);
            else if (c == '$' && TrySkipDollarQuoted(sql, i, out var next))
                i = next;
            else if (c == ';')
            {
                var statement = sql.Substring(start, i - start);
                if (!string.IsNullOrWhiteSpace(statement))
                    yield return statement;
                start = i + 1;
                i++;
            }
            else
                i++;
        }
        var tail = sql.Substring(start);
        if (!string.IsNullOrWhiteSpace(tail))
            yield return tail;
    }

    private static int SkipQuoted(string sql, int start, char quote)
    {
        var i = start + 1;
        while (i < sql.Length)
        {
            if (sql[i] == quote)
            {
                if (i + 1 < sql.Length && sql[i + 1] == quote)
                {
                    i += 2;
                    continue;
                }
                return i + 1;
            }
            i++;
        }
        return i;
    }

    private static bool TrySkipDollarQuoted(string sql, int start, out int next)
    {
        var tagEnd = start + 1;
        if (tagEnd < sql.Length && sql[tagEnd] != '$')
        {
            if (!char.IsLetter(sql[tagEnd]) && sql[tagEnd] != '_')
            {
                next = start;
                return false;
            }
            tagEnd++;
            while (tagEnd < sql.Length && (char.IsLetterOrDigit(sql[tagEnd]) || sql[tagEnd] == '_'))
                tagEnd++;
        }
        if (tagEnd >= sql.Length || sql[tagEnd] != '$')
        {
            next = start;
            return false;
        }
        var delimiter = sql.Substring(start, tagEnd - start + 1);
        var close = sql.IndexOf(delimiter, tagEnd + 1, StringComparison.Ordinal);
        next = close < 0 ? sql.Length : close + delimiter.Length;
        return true;
    }

    private static int SkipBalancedParens(string sql, int pos)
    {
        if (pos >= sql.Length || sql[pos] != '(')
            return pos;
        var depth = 0;
        while (pos < sql.Length)
        {
            var c = sql[pos];
            if (c == '\'' || c == '"')
            {
                pos = SkipQuoted(sql, pos, c);
                continue;
            }
            if (c == '$' && TrySkipDollarQuoted(sql, pos, out var next))
            {
                pos = next;
                continue;
            }
            if (c == '(')
                depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                    return pos + 1;
            }
            pos++;
        }
        return pos;
    }

    private static string ReadIdentifier(string sql, int pos, out int after)
    {
        while (pos < sql.Length && char.IsWhiteSpace(sql[pos]))
            pos++;
        var start = pos;
        while (pos < sql.Length && (char.IsLetter(sql[pos]) || sql[pos] == '_'))
            pos++;
        after = pos;
        return pos > start ? sql.Substring(start, pos - start) : null;
    }

    private static int SkipWhitespace(string sql, int pos)
    {
        while (pos < sql.Length && char.IsWhiteSpace(sql[pos]))
            pos++;
        return pos;
    }
}

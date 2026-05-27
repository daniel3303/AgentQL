using System.Data;
using System.Text;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using Equibles.AgentQL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Equibles.AgentQL.EntityFrameworkCore.Query;

public class QueryExecutor<TContext> : IQueryExecutor
    where TContext : DbContext
{
    private readonly TContext _context;
    private readonly AgentQLOptions _options;
    private readonly ILogger<QueryExecutor<TContext>> _logger;
    private bool _limitationLogged;

    public int MaxRows => _options.MaxRows;

    public QueryExecutor(
        TContext context,
        AgentQLOptions options,
        ILogger<QueryExecutor<TContext>> logger
    )
    {
        _context = context;
        _options = options;
        _logger = logger;
    }

    public async Task<QueryResult> Execute(string sql)
    {
        try
        {
            var sanitizedSql = SanitizeQuery(sql);

            if (_options.ReadOnly)
            {
                var violation = ReadOnlyStatementValidator.Validate(sanitizedSql);
                if (violation != null)
                {
                    // Same shape as the DBMS-level read-only rejection from
                    // PR #106 — silent neutralization with empty results, so
                    // the LLM-facing contract is uniform across all defenses.
                    _logger.LogInformation(
                        "ReadOnly statement whitelist rejected: {Violation}. Sql: {Sql}",
                        violation,
                        sanitizedSql
                    );
                    return QueryResult.FromSuccess(
                        new List<Dictionary<string, object>>(),
                        sanitizedSql
                    );
                }
            }

            var isolationLevel = _options.ReadOnly
                ? IsolationLevel.ReadUncommitted
                : IsolationLevel.ReadCommitted;

            var enforcer = _options.ReadOnly
                ? ReadOnlySessionEnforcerFactory.Resolve(_context.Database.ProviderName)
                : NullReadOnlySessionEnforcer.Instance;

            if (_options.ReadOnly && !_limitationLogged && enforcer.Limitation != null)
            {
                _logger.LogWarning(
                    "AgentQL ReadOnly mode has a limitation on this provider: {Limitation}",
                    enforcer.Limitation
                );
                _limitationLogged = true;
            }

            var connection = _context.Database.GetDbConnection();
            var connectionWasOpen = connection.State == ConnectionState.Open;
            if (!connectionWasOpen)
                await _context.Database.OpenConnectionAsync();

            try
            {
                await enforcer.Apply(connection);

                try
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(
                        isolationLevel
                    );

                    try
                    {
                        var results = new List<Dictionary<string, object>>();

                        await using var command = connection.CreateCommand();
                        command.CommandText = sanitizedSql;
                        command.CommandTimeout = _options.CommandTimeout;
                        command.Transaction = transaction.GetDbTransaction();

                        await using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.FieldCount > _options.MaxColumns)
                            {
                                // Reject queries that would return more
                                // columns than the configured cap — this is
                                // an extraction defense on top of MaxRows,
                                // and a clear signal to the LLM to narrow
                                // its column list. Use Error so the LLM sees
                                // a message it can learn from rather than an
                                // empty result that looks like a hit. The
                                // reader's await-using scope and the outer
                                // transaction's await-using scope take care
                                // of disposal + auto-rollback as the stack
                                // unwinds — no explicit rollback is needed
                                // (issuing one here while the reader is open
                                // throws "command in progress").
                                var fieldCount = reader.FieldCount;
                                return QueryResult.FromError(
                                    $"Result has {fieldCount} columns, exceeds MaxColumns limit of {_options.MaxColumns}. Project a specific column list instead of SELECT *."
                                );
                            }

                            var rowCount = 0;
                            while (await reader.ReadAsync() && rowCount < _options.MaxRows)
                            {
                                var row = new Dictionary<string, object>();
                                for (var i = 0; i < reader.FieldCount; i++)
                                {
                                    var value = reader.GetValue(i);
                                    row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                                }

                                results.Add(row);
                                rowCount++;
                            }
                        }

                        if (_options.ReadOnly)
                            await transaction.RollbackAsync();
                        else
                            await transaction.CommitAsync();

                        return QueryResult.FromSuccess(results, sanitizedSql);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            await transaction.RollbackAsync();
                        }
                        catch (Exception rollbackEx)
                        {
                            _logger.LogWarning(
                                rollbackEx,
                                "Failed to rollback transaction, but this is expected if connection was already closed"
                            );
                        }

                        // A read-only violation is the public success path under
                        // ReadOnly mode — the DBMS enforced what the executor
                        // would otherwise have rolled back. Return empty data so
                        // the LLM-facing contract is unchanged.
                        if (_options.ReadOnly && enforcer.IsReadOnlyViolation(ex))
                        {
                            _logger.LogInformation(
                                "Read-only mode rejected a write attempt at the DBMS: {Sql}",
                                sanitizedSql
                            );
                            return QueryResult.FromSuccess(
                                new List<Dictionary<string, object>>(),
                                sanitizedSql
                            );
                        }

                        _logger.LogError(ex, "Error executing SQL query: {Sql}", sanitizedSql);
                        throw;
                    }
                }
                finally
                {
                    // Session settings persist across pooled connections, so the
                    // reset must run even when execution failed.
                    try
                    {
                        await enforcer.Reset(connection);
                    }
                    catch (Exception resetEx)
                    {
                        _logger.LogWarning(
                            resetEx,
                            "Failed to reset read-only session settings; the connection will not be returned to the pool in its original state"
                        );
                    }
                }
            }
            finally
            {
                if (!connectionWasOpen)
                    await _context.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex)
        {
            return QueryResult.FromError($"Query execution failed: {ex.Message}");
        }
    }

    // Strips SQL line/block comments and normalizes whitespace, while leaving
    // single-quoted string literals, double-quoted identifiers, and PostgreSQL
    // dollar-quoted strings verbatim — a "--", "/* */", or run of spaces inside
    // a quoted run is data, not syntax. This is a normalization step, not a
    // SQL-injection defense (the read-only session enforcement and end-of-query
    // rollback are). Quote escaping follows the SQL standard ('' / ""), matching
    // PostgreSQL, SQLite, SQL Server and Oracle defaults; MySQL's non-standard
    // backslash escapes inside string literals are not interpreted.
    private static string SanitizeQuery(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        var result = new StringBuilder(sql.Length);
        var i = 0;

        while (i < sql.Length)
        {
            var c = sql[i];

            if (c == '\'' || c == '"')
            {
                i = CopyQuoted(sql, i, c, result);
            }
            else if (c == '$' && TryCopyDollarQuoted(sql, i, result, out var afterDollar))
            {
                i = afterDollar;
            }
            else if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                i += 2;
                while (i < sql.Length && sql[i] != '\n' && sql[i] != '\r')
                    i++;
                AppendSeparator(result);
            }
            else if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                    i++;
                i = i + 1 < sql.Length ? i + 2 : sql.Length;
                AppendSeparator(result);
            }
            else if (char.IsWhiteSpace(c))
            {
                AppendSeparator(result);
                i++;
            }
            else
            {
                result.Append(c);
                i++;
            }
        }

        return result.ToString().Trim();
    }

    // Copies a quoted run starting at its opening quote, treating a doubled
    // quote ('' or "") as an escaped quote rather than a terminator. Returns
    // the index just past the closing quote (or end, if unterminated).
    private static int CopyQuoted(string sql, int start, char quote, StringBuilder result)
    {
        result.Append(quote);
        var i = start + 1;

        while (i < sql.Length)
        {
            var c = sql[i];
            result.Append(c);

            if (c == quote)
            {
                if (i + 1 < sql.Length && sql[i + 1] == quote)
                {
                    result.Append(quote);
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return i;
    }

    // Recognises a PostgreSQL dollar-quoted string ($$...$$ or $tag$...$tag$,
    // tag = identifier) and copies the whole run verbatim. Returns false for a
    // bare '$' or a positional parameter like $1 so the caller treats it as an
    // ordinary character. An unterminated run is copied to the end.
    private static bool TryCopyDollarQuoted(
        string sql,
        int start,
        StringBuilder result,
        out int next
    )
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
        var end = close < 0 ? sql.Length : close + delimiter.Length;

        result.Append(sql, start, end - start);
        next = end;
        return true;
    }

    // Appends a single space unless the output is empty or already ends with
    // one — collapses whitespace and removed-comment gaps to one separator.
    private static void AppendSeparator(StringBuilder result)
    {
        if (result.Length > 0 && result[^1] != ' ')
            result.Append(' ');
    }
}

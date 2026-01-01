using System.Data;
using System.Text.RegularExpressions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Equibles.AgentQL.EntityFrameworkCore.Query;

public class QueryExecutor<TContext> : IQueryExecutor where TContext : DbContext {
    private readonly TContext _context;
    private readonly AgentQLOptions _options;
    private readonly ILogger<QueryExecutor<TContext>> _logger;

    public int MaxRows => _options.MaxRows;

    public QueryExecutor(TContext context, AgentQLOptions options, ILogger<QueryExecutor<TContext>> logger) {
        _context = context;
        _options = options;
        _logger = logger;
    }

    public async Task<QueryResult> Execute(string sql) {
        try {
            var sanitizedSql = SanitizeQuery(sql);

            var isolationLevel = _options.ReadOnly ? IsolationLevel.ReadUncommitted : IsolationLevel.ReadCommitted;
            await using var transaction = await _context.Database.BeginTransactionAsync(isolationLevel);

            try {
                var results = new List<Dictionary<string, object>>();

                await using var command = _context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sanitizedSql;
                command.CommandTimeout = _options.CommandTimeout;
                command.Transaction = transaction.GetDbTransaction();

                await using (var reader = await command.ExecuteReaderAsync()) {
                    var rowCount = 0;
                    while (await reader.ReadAsync() && rowCount < _options.MaxRows) {
                        var row = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++) {
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
            catch (Exception ex) {
                _logger.LogError(ex, "Error executing SQL query: {Sql}", sanitizedSql);

                try {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx) {
                    _logger.LogWarning(rollbackEx,
                        "Failed to rollback transaction, but this is expected if connection was already closed");
                }

                throw;
            }
        }
        catch (Exception ex) {
            return QueryResult.FromError($"Query execution failed: {ex.Message}");
        }
    }

    private static string SanitizeQuery(string sql) {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        sql = Regex.Replace(sql, @"--.*$", "", RegexOptions.Multiline);
        sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"\s+", " ");

        return sql.Trim();
    }
}

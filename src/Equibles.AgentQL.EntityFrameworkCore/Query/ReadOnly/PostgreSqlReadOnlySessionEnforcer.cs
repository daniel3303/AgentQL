using System.Data.Common;

namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

// SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY makes every
// subsequent transaction on the connection read-only — including the
// implicit autocommit transaction that runs the trailing INSERT after an
// embedded COMMIT. PostgreSQL then refuses the write at the server, so the
// executor no longer relies on rolling back its own transaction. Reset
// flips the session back to READ WRITE because Npgsql pools connections;
// without the reset a later borrower would inherit our read-only mode.
internal sealed class PostgreSqlReadOnlySessionEnforcer : IReadOnlySessionEnforcer
{
    public static readonly PostgreSqlReadOnlySessionEnforcer Instance = new();

    private PostgreSqlReadOnlySessionEnforcer() { }

    // SQLSTATE 25006 — read_only_sql_transaction — is the SQL-standard code
    // PostgreSQL returns when a write is attempted in a read-only transaction.
    private const string ReadOnlyTransactionSqlState = "25006";

    public Task Apply(DbConnection connection) =>
        Execute(connection, "SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY");

    public Task Reset(DbConnection connection) =>
        Execute(connection, "SET SESSION CHARACTERISTICS AS TRANSACTION READ WRITE");

    public bool IsReadOnlyViolation(Exception ex) =>
        ex is DbException db && db.SqlState == ReadOnlyTransactionSqlState;

    private static async Task Execute(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

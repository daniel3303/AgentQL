using System.Data.Common;

namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

// SET SESSION TRANSACTION READ ONLY flips MySQL's session default so every
// subsequent transaction on the connection — including the implicit
// autocommit transaction that runs the trailing write after an embedded
// COMMIT — is read-only. Reset is required because MySqlConnector and
// MySql.Data both pool connections; a later borrower would otherwise
// inherit our read-only mode.
//
// MySqlException is detected by reflection on its Number property (the
// MySQL server error code), so this assembly does not need a hard package
// reference to either MySQL provider — users targeting other providers do
// not pay for it. Error 1792 is ER_CANT_EXECUTE_IN_READ_ONLY_TRANSACTION,
// raised by MySQL 5.7+ when a write is attempted in a read-only session
// or transaction.
internal sealed class MySqlReadOnlySessionEnforcer : IReadOnlySessionEnforcer
{
    public static readonly MySqlReadOnlySessionEnforcer Instance = new();

    private const int MySqlCantExecuteInReadOnlyTransaction = 1792;

    private MySqlReadOnlySessionEnforcer() { }

    public Task Apply(DbConnection connection) =>
        Execute(connection, "SET SESSION TRANSACTION READ ONLY");

    public Task Reset(DbConnection connection) =>
        Execute(connection, "SET SESSION TRANSACTION READ WRITE");

    public bool IsReadOnlyViolation(Exception ex)
    {
        if (ex is not DbException)
            return false;

        var property = ex.GetType().GetProperty("Number");
        return property?.GetValue(ex) is int code && code == MySqlCantExecuteInReadOnlyTransaction;
    }

    private static async Task Execute(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

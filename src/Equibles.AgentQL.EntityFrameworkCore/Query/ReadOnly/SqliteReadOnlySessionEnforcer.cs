using System.Data.Common;

namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

// PRAGMA query_only is connection-scoped: while set, SQLite refuses every
// write — DDL or DML — including writes attempted under the implicit
// autocommit transaction that follows an embedded COMMIT in a multi-
// statement batch. Reset is required because Microsoft.Data.Sqlite pools
// connections and a later borrower would otherwise inherit query_only.
//
// SqliteException is detected by reflection on its SqlitePrimaryErrorCode
// property so this assembly does not need a hard package reference to
// Microsoft.Data.Sqlite — users targeting other providers do not pay for it.
internal sealed class SqliteReadOnlySessionEnforcer : IReadOnlySessionEnforcer
{
    public static readonly SqliteReadOnlySessionEnforcer Instance = new();

    // SQLITE_READONLY — the primary error code SQLite returns when a write
    // is attempted against a connection set to query_only or opened in
    // read-only mode. Extended codes such as SQLITE_READONLY_RECOVERY
    // (264) all share this primary value.
    private const int SqliteReadOnlyPrimaryErrorCode = 8;

    private SqliteReadOnlySessionEnforcer() { }

    public Task Apply(DbConnection connection) => Execute(connection, "PRAGMA query_only = 1");

    public Task Reset(DbConnection connection) => Execute(connection, "PRAGMA query_only = 0");

    public bool IsReadOnlyViolation(Exception ex)
    {
        if (ex is not DbException)
            return false;

        var property = ex.GetType().GetProperty("SqlitePrimaryErrorCode");
        return property?.GetValue(ex) is int code && code == SqliteReadOnlyPrimaryErrorCode;
    }

    private static async Task Execute(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}

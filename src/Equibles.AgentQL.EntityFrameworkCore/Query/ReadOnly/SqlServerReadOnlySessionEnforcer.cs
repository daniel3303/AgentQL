using System.Data.Common;

namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

// SQL Server has no in-band session-level "this connection cannot write"
// mode comparable to PostgreSQL's SET SESSION CHARACTERISTICS AS
// TRANSACTION READ ONLY or SQLite's PRAGMA query_only. The remaining
// defenses are out-of-band: grant the application's DB principal only
// db_datareader / SELECT, or route reads through an Always-On secondary
// via ApplicationIntent=ReadOnly. Without either, an embedded COMMIT can
// still close the executor's transaction and let a trailing INSERT
// autocommit — issue #76's bug class is not server-enforced on SQL Server.
// The enforcer therefore applies/resets nothing and surfaces a one-time
// warning through Limitation so operators see the gap.
internal sealed class SqlServerReadOnlySessionEnforcer : IReadOnlySessionEnforcer
{
    public static readonly SqlServerReadOnlySessionEnforcer Instance = new();

    private SqlServerReadOnlySessionEnforcer() { }

    public string Limitation =>
        "SQL Server does not support in-band session-level read-only enforcement. "
        + "Embedded transaction-control statements (e.g., COMMIT mid-batch) may "
        + "let writes escape the executor's rollback. Use a SELECT-only database "
        + "principal (db_datareader) or ApplicationIntent=ReadOnly against an "
        + "Always-On read-only secondary.";

    public Task Apply(DbConnection connection) => Task.CompletedTask;

    public Task Reset(DbConnection connection) => Task.CompletedTask;

    public bool IsReadOnlyViolation(Exception ex) => false;
}

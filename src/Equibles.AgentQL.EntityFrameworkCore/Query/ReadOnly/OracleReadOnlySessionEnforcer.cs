using System.Data.Common;

namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

// Oracle's SET TRANSACTION READ ONLY only applies to the current
// transaction — the moment an embedded COMMIT closes it, the trailing
// write runs in a new transaction with no read-only flag. Oracle has no
// in-band session-level equivalent of PostgreSQL's SET SESSION
// CHARACTERISTICS or SQLite's PRAGMA query_only that would also constrain
// the implicit autocommit transaction. The remaining defenses are
// out-of-band: grant the application's DB user only SELECT on the schema,
// or open the database in READ ONLY mode (ALTER DATABASE ... OPEN READ
// ONLY). Issue #76's bug class is therefore not server-enforced on Oracle.
// The enforcer applies/resets nothing and surfaces a one-time warning
// through Limitation so operators see the gap.
internal sealed class OracleReadOnlySessionEnforcer : IReadOnlySessionEnforcer
{
    public static readonly OracleReadOnlySessionEnforcer Instance = new();

    private OracleReadOnlySessionEnforcer() { }

    public string Limitation =>
        "Oracle does not support in-band session-level read-only enforcement; "
        + "SET TRANSACTION READ ONLY scopes to a single transaction and an "
        + "embedded COMMIT escapes it. Embedded transaction-control statements "
        + "may let writes escape the executor's rollback. Grant the application's "
        + "DB user only SELECT on the schema, or open the database in READ ONLY "
        + "mode (ALTER DATABASE ... OPEN READ ONLY).";

    public Task Apply(DbConnection connection) => Task.CompletedTask;

    public Task Reset(DbConnection connection) => Task.CompletedTask;

    public bool IsReadOnlyViolation(Exception ex) => false;
}

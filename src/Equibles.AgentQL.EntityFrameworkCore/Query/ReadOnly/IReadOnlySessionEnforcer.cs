using System.Data.Common;

namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

// Issues provider-specific session commands that force the DBMS itself to
// reject writes for the lifetime of a query — so an embedded COMMIT cannot
// drop the executor's transaction and let a trailing INSERT autocommit.
internal interface IReadOnlySessionEnforcer
{
    Task Apply(DbConnection connection);
    Task Reset(DbConnection connection);

    // A write rejected by the DBMS because the session is read-only is the
    // executor's success path under ReadOnly mode: the public contract is
    // "no write persists", and the rejection is how it is enforced. The
    // caller translates this into a Success=true result with empty data so
    // the LLM sees the same shape as the prior rollback-only behavior.
    bool IsReadOnlyViolation(Exception ex);
}

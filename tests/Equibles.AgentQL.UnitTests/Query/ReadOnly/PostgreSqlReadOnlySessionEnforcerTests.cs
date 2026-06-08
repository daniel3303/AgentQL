using System.Data.Common;
using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

public class PostgreSqlReadOnlySessionEnforcerTests
{
    private readonly PostgreSqlReadOnlySessionEnforcer _enforcer =
        PostgreSqlReadOnlySessionEnforcer.Instance;

    [Fact]
    public void IsReadOnlyViolation_DbExceptionWithNonReadOnlySqlState_ReturnsFalse()
    {
        // Contract: only SQLSTATE 25006 (read_only_sql_transaction) is a
        // read-only violation. A DbException carrying a different SqlState is a
        // genuine error (here 42P01, undefined_table) and must return false —
        // classifying it as a read-only violation would silently swallow the
        // real failure as the empty-success the read-only contract promises.
        var ex = new FakePostgresException("relation does not exist", "42P01");

        _enforcer.IsReadOnlyViolation(ex).Should().BeFalse();
    }

    private sealed class FakePostgresException : DbException
    {
        public FakePostgresException(string message, string sqlState)
            : base(message)
        {
            SqlState = sqlState;
        }

        public override string SqlState { get; }
    }
}

using System.Data.Common;
using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

/// <summary>
/// SQLite enforces read-only via <c>PRAGMA query_only</c>; a rejected write
/// surfaces as a SqliteException whose primary error code is SQLITE_READONLY
/// (8). <c>IsReadOnlyViolation</c> reflects over the <c>SqlitePrimaryErrorCode</c>
/// property to recognise that signal without a hard package reference. These
/// tests pin that detection contract — the path that turns a DBMS-level write
/// rejection into the silent empty-success the read-only mode promises.
/// </summary>
public class SqliteReadOnlySessionEnforcerTests
{
    private readonly SqliteReadOnlySessionEnforcer _enforcer =
        SqliteReadOnlySessionEnforcer.Instance;

    [Fact]
    public void IsReadOnlyViolation_DbExceptionWithReadOnlyPrimaryCode_ReturnsTrue()
    {
        // Contract: a DbException reporting SqlitePrimaryErrorCode == 8
        // (SQLITE_READONLY) is the read-only write rejection that must be
        // neutralized, so detection must return true.
        var ex = new FakeSqliteException("attempt to write a readonly database", 8);

        _enforcer.IsReadOnlyViolation(ex).Should().BeTrue();
    }

    private sealed class FakeSqliteException : DbException
    {
        public FakeSqliteException(string message, int sqlitePrimaryErrorCode)
            : base(message)
        {
            SqlitePrimaryErrorCode = sqlitePrimaryErrorCode;
        }

        public int SqlitePrimaryErrorCode { get; }
    }
}

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

    [Fact]
    public void IsReadOnlyViolation_NonDbExceptionWithReadOnlyCode_ReturnsFalse()
    {
        // Contract: detection requires a DbException first; the reflection
        // lookup must not leak past that guard. A non-DbException exposing the
        // same SqlitePrimaryErrorCode == 8 must NOT be neutralized as a
        // read-only violation, or a genuine error would be silently swallowed.
        var ex = new FakeNonDbExceptionWithCode("not a db exception", 8);

        _enforcer.IsReadOnlyViolation(ex).Should().BeFalse();
    }

    [Fact]
    public void IsReadOnlyViolation_DbExceptionWithoutSqlitePrimaryErrorCode_ReturnsFalse()
    {
        // Contract: a DbException from a non-SQLite provider has no
        // SqlitePrimaryErrorCode property, so the reflection lookup yields null
        // and detection must return false — not throw on the missing property.
        var ex = new FakePlainDbException("a non-sqlite db error");

        _enforcer.IsReadOnlyViolation(ex).Should().BeFalse();
    }

    private sealed class FakePlainDbException : DbException
    {
        public FakePlainDbException(string message)
            : base(message) { }
    }

    private sealed class FakeNonDbExceptionWithCode : Exception
    {
        public FakeNonDbExceptionWithCode(string message, int sqlitePrimaryErrorCode)
            : base(message)
        {
            SqlitePrimaryErrorCode = sqlitePrimaryErrorCode;
        }

        public int SqlitePrimaryErrorCode { get; }
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

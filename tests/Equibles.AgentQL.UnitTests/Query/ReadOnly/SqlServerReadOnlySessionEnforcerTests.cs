using System.Data.Common;
using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using NSubstitute;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

/// <summary>
/// SQL Server has no in-band session-level read-only enforcement, so the
/// enforcer is intentionally a no-op that only exposes a Limitation
/// message. These tests pin that contract: no commands sent on Apply /
/// Reset, IsReadOnlyViolation always returns false, Limitation surfaces
/// the recommended remediation (SELECT-only principal / ApplicationIntent).
/// </summary>
public class SqlServerReadOnlySessionEnforcerTests
{
    private readonly SqlServerReadOnlySessionEnforcer _enforcer =
        SqlServerReadOnlySessionEnforcer.Instance;

    [Fact]
    public void Limitation_IsNonNullAndDescribesTheGap()
    {
        _enforcer.Limitation.Should().NotBeNullOrWhiteSpace();
        _enforcer.Limitation.Should().Contain("SQL Server");
    }

    [Fact]
    public void Limitation_RecommendsLeastPrivilegedRemediation()
    {
        // The message must surface a concrete remediation so operators
        // do not assume parity with the PostgreSQL / SQLite enforcement.
        _enforcer.Limitation.Should().ContainAny("db_datareader", "ApplicationIntent", "SELECT");
    }

    [Fact]
    public async Task Apply_SendsNoCommandsOnTheConnection()
    {
        var connection = Substitute.For<DbConnection>();

        await _enforcer.Apply(connection);

        connection.DidNotReceiveWithAnyArgs().CreateCommand();
    }

    [Fact]
    public async Task Reset_SendsNoCommandsOnTheConnection()
    {
        var connection = Substitute.For<DbConnection>();

        await _enforcer.Reset(connection);

        connection.DidNotReceiveWithAnyArgs().CreateCommand();
    }

    [Theory]
    [MemberData(nameof(VariousExceptionShapes))]
    public void IsReadOnlyViolation_AlwaysReturnsFalse(Exception ex)
    {
        // SQL Server returns no in-band signal for read-only violation
        // because it has no in-band read-only enforcement — every
        // exception must be treated as a real error, not a silent
        // neutralization.
        _enforcer.IsReadOnlyViolation(ex).Should().BeFalse();
    }

    public static TheoryData<Exception> VariousExceptionShapes() =>
        new()
        {
            new Exception("plain"),
            new InvalidOperationException("ado"),
            new FakeDbException("dbex", "25006"),
            new FakeDbException("dbex", null),
        };

    [Fact]
    public void Instance_IsSingleton()
    {
        SqlServerReadOnlySessionEnforcer
            .Instance.Should()
            .BeSameAs(SqlServerReadOnlySessionEnforcer.Instance);
    }

    private sealed class FakeDbException : DbException
    {
        public FakeDbException(string message, string sqlState)
            : base(message)
        {
            SqlState = sqlState;
        }

        public override string SqlState { get; }
    }
}

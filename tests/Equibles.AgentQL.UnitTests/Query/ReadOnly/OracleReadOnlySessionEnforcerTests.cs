using System.Data.Common;
using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using NSubstitute;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

/// <summary>
/// Oracle's SET TRANSACTION READ ONLY is per-transaction and an embedded
/// COMMIT escapes it, so the enforcer is intentionally a no-op that only
/// exposes a Limitation message — same shape as the SQL Server enforcer.
/// These tests pin that contract: no commands sent on Apply / Reset,
/// IsReadOnlyViolation always returns false, Limitation surfaces the
/// recommended remediation (SELECT-only principal or
/// ALTER DATABASE ... OPEN READ ONLY).
/// </summary>
public class OracleReadOnlySessionEnforcerTests
{
    private readonly OracleReadOnlySessionEnforcer _enforcer =
        OracleReadOnlySessionEnforcer.Instance;

    [Fact]
    public void Limitation_IsNonNullAndMentionsOracle()
    {
        _enforcer.Limitation.Should().NotBeNullOrWhiteSpace();
        _enforcer.Limitation.Should().Contain("Oracle");
    }

    [Fact]
    public void Limitation_ExplainsThePerTransactionGap()
    {
        // The message must surface why Oracle's SET TRANSACTION READ ONLY
        // is insufficient — operators reading the warning need to know
        // it scopes to the current transaction only.
        _enforcer.Limitation.Should().ContainAny("transaction", "COMMIT", "session-level");
    }

    [Fact]
    public void Limitation_RecommendsRemediation()
    {
        _enforcer.Limitation.Should().ContainAny("SELECT", "READ ONLY", "ALTER DATABASE");
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
        // Oracle returns no in-band signal of a read-only violation that
        // the executor can rely on, so every exception must be treated
        // as a real error.
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
        OracleReadOnlySessionEnforcer
            .Instance.Should()
            .BeSameAs(OracleReadOnlySessionEnforcer.Instance);
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

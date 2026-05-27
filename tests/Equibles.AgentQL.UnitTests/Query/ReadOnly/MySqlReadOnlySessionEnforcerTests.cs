using System.Data;
using System.Data.Common;
using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

/// <summary>
/// MySQL exposes a real session-level read-only command — these tests pin
/// both halves of the contract:
///   (a) Apply / Reset send the expected SET SESSION TRANSACTION commands
///       on the connection (verified by a recording DbConnection stub);
///   (b) IsReadOnlyViolation recognises MySQL's
///       ER_CANT_EXECUTE_IN_READ_ONLY_TRANSACTION via reflection on the
///       provider exception's Number property (1792), and treats everything
///       else as a real error.
/// </summary>
public class MySqlReadOnlySessionEnforcerTests
{
    private readonly MySqlReadOnlySessionEnforcer _enforcer = MySqlReadOnlySessionEnforcer.Instance;

    [Fact]
    public async Task Apply_SendsSetSessionTransactionReadOnly()
    {
        var connection = new RecordingDbConnection();

        await _enforcer.Apply(connection);

        connection
            .ExecutedSql.Should()
            .ContainSingle()
            .Which.Should()
            .Be("SET SESSION TRANSACTION READ ONLY");
    }

    [Fact]
    public async Task Reset_SendsSetSessionTransactionReadWrite()
    {
        var connection = new RecordingDbConnection();

        await _enforcer.Reset(connection);

        connection
            .ExecutedSql.Should()
            .ContainSingle()
            .Which.Should()
            .Be("SET SESSION TRANSACTION READ WRITE");
    }

    [Fact]
    public void IsReadOnlyViolation_MySqlException1792_ReturnsTrue()
    {
        var ex = new FakeMySqlException(
            "Cannot execute statement in a READ ONLY transaction",
            1792
        );

        _enforcer.IsReadOnlyViolation(ex).Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1062)]
    [InlineData(2006)]
    public void IsReadOnlyViolation_MySqlExceptionOtherNumber_ReturnsFalse(int number)
    {
        var ex = new FakeMySqlException("other", number);

        _enforcer.IsReadOnlyViolation(ex).Should().BeFalse();
    }

    [Fact]
    public void IsReadOnlyViolation_DbExceptionWithoutNumberProperty_ReturnsFalse()
    {
        // A DbException that does not expose a Number property — the
        // reflection lookup must return null and the predicate must
        // fall through to false rather than throwing.
        var ex = new FakeDbExceptionNoNumber("plain DbException");

        _enforcer.IsReadOnlyViolation(ex).Should().BeFalse();
    }

    [Fact]
    public void IsReadOnlyViolation_NonDbException_ReturnsFalse()
    {
        _enforcer
            .IsReadOnlyViolation(new InvalidOperationException("not a db error"))
            .Should()
            .BeFalse();
    }

    [Fact]
    public void Instance_IsSingleton()
    {
        MySqlReadOnlySessionEnforcer
            .Instance.Should()
            .BeSameAs(MySqlReadOnlySessionEnforcer.Instance);
    }

    // Mimics MySqlException / MySqlConnector.MySqlException — the
    // reflection path in IsReadOnlyViolation looks for a Number property
    // by name on the runtime type, so this fake faithfully exercises it.
    private sealed class FakeMySqlException : DbException
    {
        public FakeMySqlException(string message, int number)
            : base(message)
        {
            Number = number;
        }

        public int Number { get; }
    }

    private sealed class FakeDbExceptionNoNumber : DbException
    {
        public FakeDbExceptionNoNumber(string message)
            : base(message) { }
    }

    // Minimal DbConnection stub that captures the command text of every
    // CreateCommand-then-ExecuteNonQuery cycle. Enough surface to back the
    // enforcer's Execute helper without dragging in a real provider.
    private sealed class RecordingDbConnection : DbConnection
    {
        public List<string> ExecutedSql { get; } = new();

        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }

        public override void Close() { }

        public override void Open() { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this);

        private sealed class RecordingDbCommand : DbCommand
        {
            private readonly RecordingDbConnection _owner;

            public RecordingDbCommand(RecordingDbConnection owner)
            {
                _owner = owner;
            }

            public override string CommandText { get; set; } = string.Empty;
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection => null;
            protected override DbTransaction DbTransaction { get; set; }

            public override void Cancel() { }

            public override int ExecuteNonQuery()
            {
                _owner.ExecutedSql.Add(CommandText);
                return 0;
            }

            public override object ExecuteScalar() => null;

            public override void Prepare() { }

            protected override DbParameter CreateDbParameter() => null;

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
                throw new NotSupportedException();
        }
    }
}

using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against transaction-control injection: an LLM could
/// smuggle <c>SELECT 1; COMMIT; INSERT ...</c> to close the executor's
/// managed transaction mid-batch, leaving the trailing write to autocommit
/// while the executor's later rollback rolls back nothing. The contract is
/// stronger than the per-statement-shape variants — the rollback must
/// neutralise every mutation, no matter what transaction-control keywords
/// the SQL contains. Durability is verified on an INDEPENDENT connection.
///
/// Defense: when ReadOnly is true the executor issues
/// <c>SET SESSION CHARACTERISTICS AS TRANSACTION READ ONLY</c> on the
/// connection before opening its own transaction, so the implicit
/// autocommit transaction underneath the trailing INSERT is also read-only
/// and PostgreSQL refuses the write at the server.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCommitInjectionProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCommitInjectionProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCommitInjectedThenInsert_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        await executor.Execute(
            "SELECT 1; COMMIT; INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('Mallory', 99)"
        );

        // Independent connection: the true durability oracle for "did it persist".
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var poisoned = await verify.Execute(
            "SELECT COUNT(*) AS c FROM \"Customers\" WHERE \"Name\" = 'Mallory'"
        );

        Convert.ToInt32(poisoned.Data![0]["c"]).Should().Be(0);
    }
}

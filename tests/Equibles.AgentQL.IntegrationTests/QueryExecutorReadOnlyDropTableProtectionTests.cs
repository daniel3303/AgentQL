using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee for DDL — PostgreSQL has fully transactional DDL, so a
/// <c>DROP TABLE</c> issued inside the executor's transaction must be undone
/// by the <c>ReadOnly = true</c> rollback. This is the most destructive
/// statement an LLM could plausibly emit; the contract is the same as for
/// DML — no mutation persists across connections — but the rollback path
/// (catalog state, not just row state) is exercised differently from
/// DELETE/UPDATE. Verification runs on an INDEPENDENT connection so the
/// catalog is queried fresh.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyDropTableProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyDropTableProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyDropTable_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var drop = await executor.Execute("DROP TABLE \"Bookings\"");
        drop.Success.Should().BeTrue(drop.ErrorMessage);

        // Independent connection: the true durability oracle. If the DROP had
        // leaked, this query would surface either "relation does not exist" or
        // a row count below the seeded total.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var count = await verify.Execute("SELECT COUNT(*) AS c FROM \"Bookings\"");

        count.Success.Should().BeTrue(count.ErrorMessage);
        Convert.ToInt32(count.Data![0]["c"]).Should().Be(3);
    }
}

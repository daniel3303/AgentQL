using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the <c>MERGE ... WHEN MATCHED THEN UPDATE</c>
/// shape — the existing MERGE pin only covers the INSERT clause, but UPDATE
/// runs a distinct path through PostgreSQL's MERGE planner and is the most
/// realistic upsert vector an LLM would emit. A single statement, leading
/// keyword <c>MERGE</c> (not <c>UPDATE</c>), would defeat any future
/// allowlist that filters only the classic DML trio. The contract is the
/// same — with <c>ReadOnly = true</c> the rollback must neutralise the
/// mutation — but the path is independent of the prior MERGE INSERT pin.
/// Durability verified on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyMergeUpdateProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyMergeUpdateProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyMergeStatementUpdate_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "MERGE INTO \"Customers\" AS c "
                + "USING (SELECT 1 AS x) AS s ON TRUE "
                + "WHEN MATCHED THEN UPDATE SET \"Tier\" = 99"
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

        // Independent connection: the true durability oracle. If the MERGE
        // UPDATE had leaked, every seeded customer would now sit at tier 99.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM \"Customers\" WHERE \"Tier\" = 99"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

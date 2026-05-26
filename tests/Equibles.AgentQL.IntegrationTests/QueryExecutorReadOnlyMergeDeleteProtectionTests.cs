using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the <c>MERGE ... WHEN MATCHED THEN DELETE</c>
/// shape — completes the MERGE trifecta alongside the existing INSERT and
/// UPDATE clause pins. A single statement whose leading keyword is
/// <c>MERGE</c> (not <c>DELETE</c>) is the most destructive MERGE variant: it
/// can wipe every matching row in one go and would defeat any future
/// allowlist that filters only the classic DML trio. The contract is the
/// same — with <c>ReadOnly = true</c> the rollback must neutralise the
/// deletion — but the executor path runs through PostgreSQL's MERGE planner,
/// independent of the prior MERGE INSERT and UPDATE pins. Durability verified
/// on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyMergeDeleteProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyMergeDeleteProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyMergeStatementDelete_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "MERGE INTO \"Bookings\" AS b "
                + "USING (SELECT 1 AS x) AS s ON TRUE "
                + "WHEN MATCHED THEN DELETE"
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

        // Independent connection: the true durability oracle. If the MERGE
        // DELETE had leaked, the three seeded bookings would be gone.
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

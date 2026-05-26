using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against statement-stacking — TRUNCATE variant. An LLM
/// could piggy-back the most destructive single-table wipe onto a benign read
/// via <c>SELECT 1; TRUNCATE "Bookings"</c>, escaping the executor's reader
/// loop yet still executing inside the wrapping transaction. The contract is
/// the same as for plain TRUNCATE — with <c>ReadOnly = true</c> the rollback
/// must neutralise the wipe — but the leading SELECT changes the parser path
/// the way it does for the existing stacked DELETE / UPDATE variants.
/// Durability verified on an INDEPENDENT connection by reading the seeded row
/// count back.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyStackedTruncateProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyStackedTruncateProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyStackedSelectThenTruncate_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var stacked = await executor.Execute("SELECT 1; TRUNCATE \"Bookings\"");
        stacked.Success.Should().BeTrue(stacked.ErrorMessage);

        // Independent connection: the true durability oracle. If the stacked
        // TRUNCATE had leaked, the three seeded bookings would be gone.
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

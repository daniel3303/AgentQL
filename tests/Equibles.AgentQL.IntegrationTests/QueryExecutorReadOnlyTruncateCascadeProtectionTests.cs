using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the cascading-TRUNCATE shape — an LLM-realistic
/// way to wipe an entire object graph in a single statement without naming
/// every dependent table: <c>TRUNCATE "Customers" CASCADE</c> deletes every
/// row in Customers AND every Booking row that references it through the FK.
/// Distinct executor path from the plain single-table and stacked TRUNCATE
/// pins because PostgreSQL has to traverse the FK graph in <c>pg_constraint</c>
/// to enumerate dependents. With <c>ReadOnly = true</c> the rollback must
/// neutralise every row deletion; verification runs on an INDEPENDENT
/// connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyTruncateCascadeProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyTruncateCascadeProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyTruncateCascade_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var wipe = await executor.Execute("TRUNCATE \"Customers\" CASCADE");
        wipe.Success.Should().BeTrue(wipe.ErrorMessage);

        // Independent connection: the true durability oracle. If the
        // cascading TRUNCATE had leaked, both Customers (2 seeded) and the
        // FK-dependent Bookings (3 seeded) would be gone.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT "
                + "(SELECT COUNT(*) FROM \"Customers\") AS customers, "
                + "(SELECT COUNT(*) FROM \"Bookings\") AS bookings"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["customers"]).Should().Be(2);
        Convert.ToInt32(probe.Data![0]["bookings"]).Should().Be(3);
    }
}

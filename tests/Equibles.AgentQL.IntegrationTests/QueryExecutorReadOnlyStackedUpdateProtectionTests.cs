using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against statement-stacking, UPDATE variant. The DELETE
/// stacked-statement protection is pinned; this covers <c>SELECT 1; UPDATE
/// "Customers" SET "Tier" = 99</c> — the same piggyback pattern with a
/// different DML verb. The trailing write could escape the reader loop yet
/// still execute inside the transaction. With <c>ReadOnly = true</c> the
/// rollback must neutralise it. Durability is verified on an INDEPENDENT
/// connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyStackedUpdateProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyStackedUpdateProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyStackedSelectThenUpdate_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var stacked = await executor.Execute("SELECT 1; UPDATE \"Customers\" SET \"Tier\" = 99");
        stacked.Success.Should().BeTrue(stacked.ErrorMessage);

        // Independent connection: the true durability oracle for "did it persist".
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var tampered = await verify.Execute(
            "SELECT COUNT(*) AS c FROM \"Customers\" WHERE \"Tier\" = 99"
        );

        Convert.ToInt32(tampered.Data![0]["c"]).Should().Be(0);
    }
}

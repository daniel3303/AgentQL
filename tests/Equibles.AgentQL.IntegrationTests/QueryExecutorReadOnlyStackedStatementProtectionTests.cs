using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against statement-stacking: an LLM-issued
/// <c>SELECT 1; DELETE FROM "Customers"</c> piggy-backs a destructive write
/// onto a benign read. The single-statement DELETE protection is already
/// pinned; this covers the stacked-batch shape, where the write could escape
/// the reader loop yet still execute. With <c>ReadOnly = true</c> the rollback
/// must neutralise it. Durability is verified on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyStackedStatementProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyStackedStatementProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyStackedSelectThenDelete_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var stacked = await executor.Execute("SELECT 1; DELETE FROM \"Customers\"");
        stacked.Success.Should().BeTrue(stacked.ErrorMessage);

        // Independent connection: the true durability oracle for "did it persist".
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var count = await verify.Execute("SELECT COUNT(*) AS c FROM \"Customers\"");

        Convert.ToInt32(count.Data![0]["c"]).Should().Be(2);
    }
}

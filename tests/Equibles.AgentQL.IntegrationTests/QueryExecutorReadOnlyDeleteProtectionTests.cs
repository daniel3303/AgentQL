using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee: with <c>ReadOnly = true</c> the executor's only defence
/// against a destructive LLM-issued statement is the transaction rollback. The
/// contract therefore promises that a <c>DELETE</c> leaves all data intact. We
/// verify durability through an INDEPENDENT context (a fresh connection) — not
/// the executor's own — so a rollback that only appears effective on the
/// originating connection cannot mask real data loss.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyDeleteProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyDeleteProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyDeleteFromTable_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var delete = await executor.Execute("DELETE FROM \"Customers\"");
        delete.Success.Should().BeTrue();

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

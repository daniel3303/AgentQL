using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee, sibling to the DELETE-protection test: with
/// <c>ReadOnly = true</c> the executor's only defence against a tampering
/// LLM-issued statement is the transaction rollback. The contract therefore
/// promises an <c>UPDATE</c> mutates nothing. Durability is verified through
/// an INDEPENDENT context (a fresh connection) so a rollback that only looks
/// effective on the originating connection cannot mask real tampering.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyUpdateProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyUpdateProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyUpdateTampersColumn_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var update = await executor.Execute("UPDATE \"Customers\" SET \"Tier\" = 99");
        update.Success.Should().BeTrue();

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

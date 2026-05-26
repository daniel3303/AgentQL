using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee for INSERT, the "poison row" attack — a write an LLM
/// could use to seed fake data that later queries would pick up. INSERT is
/// covered by an existing smoke test, but only verified on the executor's
/// own connection; the DELETE/UPDATE protection tests verify durability on
/// an INDEPENDENT connection. This test closes that gap so the matrix of
/// rollback-protected writes is consistently verified.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyInsertProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyInsertProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyInsertRow_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var insert = await executor.Execute(
            "INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('Mallory', 99)"
        );
        insert.Success.Should().BeTrue(insert.ErrorMessage);

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

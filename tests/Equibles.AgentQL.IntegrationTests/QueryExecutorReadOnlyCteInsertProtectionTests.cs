using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the CTE-with-DML bypass, INSERT variant —
/// completes the triangle alongside the DELETE and UPDATE shapes already
/// pinned. <c>WITH i AS (INSERT INTO "Customers" ... RETURNING *) SELECT *
/// FROM i</c> seeds a poison row inside a single statement that looks
/// SELECT-shaped to a naive guard. The contract is the same as for plain
/// INSERT — with <c>ReadOnly = true</c> the rollback must neutralise the
/// write. Durability is verified on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCteInsertProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCteInsertProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCteWrappedInsert_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "WITH i AS (INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('Mallory', 99) RETURNING *) SELECT * FROM i"
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

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

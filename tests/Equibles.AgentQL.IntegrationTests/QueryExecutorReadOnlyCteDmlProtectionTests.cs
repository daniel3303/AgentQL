using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the CTE-with-DML bypass: PostgreSQL allows
/// <c>WITH d AS (DELETE FROM "Customers" RETURNING *) SELECT * FROM d</c> —
/// a single statement that opens with <c>WITH</c>, returns rows, and is
/// syntactically SELECT-shaped, yet mutates data. It is exactly the trick an
/// LLM steered by "you may only SELECT" could land on. The contract is the
/// same as for plain DELETE/UPDATE: with <c>ReadOnly = true</c> the rollback
/// must neutralise the write. Durability is verified on an INDEPENDENT
/// connection so a rollback that only looks effective on the originating
/// connection cannot mask real data loss.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCteDmlProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCteDmlProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCteWrappedDelete_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "WITH d AS (DELETE FROM \"Customers\" RETURNING *) SELECT * FROM d"
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

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

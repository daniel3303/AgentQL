using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the triple-wrapped procedural + dynamic-SQL +
/// DDL bypass: PL/pgSQL refuses raw DDL, so an LLM can smuggle
/// <c>DROP TABLE</c> through a DO block via <c>EXECUTE</c>:
/// <c>DO $$ BEGIN EXECUTE 'DROP TABLE "Bookings"'; END $$;</c>. The leading
/// token is <c>DO</c>, the inner construct is <c>EXECUTE</c> (string-built
/// SQL), and the actual destruction sits inside a string literal — defeating
/// any allowlist that filters on leading token, intermediate keyword, or DDL
/// detection through static parsing. With <c>ReadOnly = true</c> the rollback
/// must still neutralise the DROP because PostgreSQL DDL is transactional
/// even when issued through dynamic SQL inside PL/pgSQL. Verification runs
/// on an INDEPENDENT connection so the catalog is queried fresh.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyDoBlockDynamicDropTableProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyDoBlockDynamicDropTableProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyDoBlockWrappedDynamicDropTable_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "DO $$ BEGIN EXECUTE 'DROP TABLE \"Bookings\"'; END $$;"
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

        // Independent connection: the true durability oracle. If the dynamic
        // DROP had leaked, this row count would surface "relation does not
        // exist" or a count of 0.
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

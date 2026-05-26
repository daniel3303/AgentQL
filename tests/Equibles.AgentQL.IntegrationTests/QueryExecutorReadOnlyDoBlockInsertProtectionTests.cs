using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against PL/pgSQL DO-block bypass — an LLM can smuggle
/// mutations inside <c>DO $$ BEGIN INSERT ... END $$;</c> where the top-level
/// SQL contains no DML keyword at all, defeating any future statement-shape
/// allowlist that inspects the leading token. The contract is the same as for
/// plain INSERT — with <c>ReadOnly = true</c> the rollback must neutralise the
/// write — but the rollback path is exercised through PL/pgSQL procedural code
/// rather than a top-level DML statement. Durability is verified on an
/// INDEPENDENT connection so the row state is queried fresh.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyDoBlockInsertProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyDoBlockInsertProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyDoBlockWrappedInsert_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "DO $$ BEGIN INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('Mallory', 99); END $$;"
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

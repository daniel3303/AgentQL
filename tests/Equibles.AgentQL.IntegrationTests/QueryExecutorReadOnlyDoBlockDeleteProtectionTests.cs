using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against PL/pgSQL DO-block DELETE bypass — completes the
/// DO-block trifecta started by the INSERT variant. An LLM can wipe whole
/// tables through <c>DO $$ BEGIN DELETE FROM "Bookings"; END $$;</c> where the
/// top-level SQL contains no DML keyword, defeating any future allowlist that
/// inspects the leading token. The contract is the same as for plain DELETE —
/// with <c>ReadOnly = true</c> the rollback must neutralise the mass-deletion
/// — but the rollback path runs through PL/pgSQL procedural code. Durability
/// is verified on an INDEPENDENT connection by reading the seeded row count.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyDoBlockDeleteProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyDoBlockDeleteProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyDoBlockWrappedDelete_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var wipe = await executor.Execute("DO $$ BEGIN DELETE FROM \"Bookings\"; END $$;");
        wipe.Success.Should().BeTrue(wipe.ErrorMessage);

        // Independent connection: the true durability oracle. If the procedural
        // DELETE had leaked, the seeded three bookings would be gone.
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

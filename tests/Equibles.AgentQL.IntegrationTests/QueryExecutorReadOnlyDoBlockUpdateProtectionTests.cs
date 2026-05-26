using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against PL/pgSQL DO-block UPDATE bypass — completes the
/// DO-block trifecta alongside the INSERT and DELETE variants. An LLM could
/// rewrite every booking price to zero through
/// <c>DO $$ BEGIN UPDATE "Bookings" SET "Price" = 0; END $$;</c> where the
/// top-level SQL contains no DML keyword, defeating any future allowlist that
/// inspects the leading token. The contract is the same as for plain UPDATE —
/// with <c>ReadOnly = true</c> the rollback must neutralise the mass mutation —
/// but the path runs through PL/pgSQL procedural code rather than a top-level
/// UPDATE. Durability verified on an INDEPENDENT connection by reading the
/// seeded prices back.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyDoBlockUpdateProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyDoBlockUpdateProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyDoBlockWrappedUpdate_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "DO $$ BEGIN UPDATE \"Bookings\" SET \"Price\" = 0; END $$;"
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

        // Independent connection: the true durability oracle. If the procedural
        // UPDATE had leaked, every seeded booking price would now be 0.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM \"Bookings\" WHERE \"Price\" = 0"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

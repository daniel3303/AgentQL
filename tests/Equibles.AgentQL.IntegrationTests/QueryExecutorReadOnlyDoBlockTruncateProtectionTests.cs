using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the PL/pgSQL DO-block TRUNCATE bypass —
/// extends the DO-block coverage to the most destructive single-table wipe.
/// PL/pgSQL allows <c>TRUNCATE</c> directly (no <c>EXECUTE</c> wrapper
/// required), so an LLM can emit
/// <c>DO $$ BEGIN TRUNCATE "Bookings"; END $$;</c> whose leading token is
/// <c>DO</c>, defeating any future allowlist filtering on top-level DML or
/// DDL keywords. The contract is the same as for plain TRUNCATE — with
/// <c>ReadOnly = true</c> the rollback must neutralise the wipe — but the
/// path runs through PL/pgSQL procedural code. Durability verified on an
/// INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyDoBlockTruncateProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyDoBlockTruncateProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyDoBlockWrappedTruncate_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var wipe = await executor.Execute("DO $$ BEGIN TRUNCATE \"Bookings\"; END $$;");
        wipe.Success.Should().BeTrue(wipe.ErrorMessage);

        // Independent connection: the true durability oracle. If the
        // procedural TRUNCATE had leaked, the three seeded bookings would be
        // gone.
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

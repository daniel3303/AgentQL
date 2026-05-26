using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the most destructive single-statement attack
/// possible: <c>DROP SCHEMA public CASCADE</c> wipes every table, view,
/// function, trigger, and sequence the namespace contains in one shot.
/// Distinct executor path from the single-object DROP TABLE pin because the
/// rollback must un-do a cascade-traversal across every catalog at once
/// (<c>pg_class</c>, <c>pg_attribute</c>, <c>pg_proc</c>, <c>pg_trigger</c>,
/// FK chains, etc). With <c>ReadOnly = true</c> the rollback must restore the
/// entire namespace; verification runs on an INDEPENDENT connection and also
/// reads from <c>Customers</c> to prove the underlying objects survived too.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyDropSchemaCascadeProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyDropSchemaCascadeProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyDropSchemaCascade_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var wipe = await executor.Execute("DROP SCHEMA public CASCADE");
        wipe.Success.Should().BeTrue(wipe.ErrorMessage);

        // Independent connection: the true durability oracle. If the
        // DROP SCHEMA had leaked, both the namespace AND every seeded row
        // would be gone.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT "
                + "(SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'public') AS schema_present, "
                + "(SELECT COUNT(*) FROM \"Customers\") AS customers"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["schema_present"]).Should().Be(1);
        Convert.ToInt32(probe.Data![0]["customers"]).Should().Be(2);
    }
}

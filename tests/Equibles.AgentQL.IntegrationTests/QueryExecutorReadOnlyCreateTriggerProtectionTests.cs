using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the most persistent kind of side-effect
/// backdoor — a CREATE TRIGGER plants a sleeper that fires on every future
/// write to the target table, hiding the actual payload in a referenced
/// function while leaving a quiet entry in <c>pg_trigger</c>. The catalog
/// path is distinct from the already-pinned CREATE FUNCTION, CREATE USER,
/// CREATE VIEW, and CREATE INDEX variants. The trigger here uses a built-in
/// trigger function so no function setup is needed; the assertion focuses on
/// the trigger row, which is what would survive across sessions. With
/// <c>ReadOnly = true</c> the rollback must undo the trigger; verification
/// runs on an INDEPENDENT connection against <c>pg_trigger</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateTriggerProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateTriggerProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateTrigger_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var create = await executor.Execute(
            "CREATE TRIGGER \"backdoor_trigger\" "
                + "BEFORE UPDATE ON \"Customers\" FOR EACH ROW "
                + "EXECUTE FUNCTION suppress_redundant_updates_trigger()"
        );
        create.Success.Should().BeTrue(create.ErrorMessage);

        // Independent connection: the true durability oracle. If the CREATE
        // TRIGGER had leaked, the trigger row would fire on every future
        // UPDATE to the Customers table.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM pg_trigger WHERE tgname = 'backdoor_trigger'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

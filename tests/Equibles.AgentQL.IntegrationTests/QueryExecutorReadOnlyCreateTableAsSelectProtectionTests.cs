using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the CREATE-TABLE-AS-SELECT exfiltration shape —
/// a single statement that combines DDL (new table) and DML (row copy) in
/// one, which an LLM could use to stash sensitive rows in a sibling table
/// for later reading: <c>CREATE TABLE "stolen" AS SELECT * FROM "Customers"</c>.
/// The contract is the same as for plain DDL — with <c>ReadOnly = true</c>
/// the rollback must undo BOTH the catalog change and the inserted rows. The
/// code path is distinct from the plain DROP/TRUNCATE/ALTER variants already
/// pinned because both halves of the statement mutate. Verification runs on
/// an INDEPENDENT connection so the catalog is queried fresh.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateTableAsSelectProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateTableAsSelectProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateTableAsSelect_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "CREATE TABLE \"stolen_customers\" AS SELECT * FROM \"Customers\""
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

        // Independent connection: the true durability oracle. If the
        // CREATE-AS had leaked, this catalog probe would return one row for
        // the new table.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM information_schema.tables "
                + "WHERE table_name = 'stolen_customers'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

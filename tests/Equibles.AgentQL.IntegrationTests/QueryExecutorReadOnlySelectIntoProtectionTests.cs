using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the PostgreSQL <c>SELECT ... INTO new_table</c>
/// exfiltration shape — the older spelling of <c>CREATE TABLE AS SELECT</c>,
/// equivalent in effect but with <c>SELECT</c> as the leading keyword. A
/// future top-level allowlist filtering only <c>CREATE</c>/<c>INSERT</c>/
/// <c>UPDATE</c>/<c>DELETE</c> would let this through and silently materialise
/// a sibling table containing every row of the source. The contract is the
/// same as for CREATE TABLE AS — with <c>ReadOnly = true</c> the rollback must
/// undo both the new table and the inserted rows. Durability is verified on
/// an INDEPENDENT connection against <c>information_schema.tables</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlySelectIntoProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlySelectIntoProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlySelectInto_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "SELECT * INTO \"stolen_customers\" FROM \"Customers\""
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

        // Independent connection: the true durability oracle. If the SELECT
        // INTO had leaked, this catalog probe would return one row for the
        // new sibling table.
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

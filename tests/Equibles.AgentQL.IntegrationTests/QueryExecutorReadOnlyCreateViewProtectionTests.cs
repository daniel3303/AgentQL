using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the CREATE-VIEW exfiltration shape — a view
/// stores a query, not rows, so an LLM could emit
/// <c>CREATE VIEW "leaked" AS SELECT * FROM "Customers"</c> to expose every
/// column (including <c>[AgentQLIgnore]</c>-protected ones) under a new
/// catalog object the schema description never advertised. Different code
/// path from the already-pinned CTAS / SELECT INTO variants because no
/// underlying storage is allocated — pure catalog mutation. With
/// <c>ReadOnly = true</c> the rollback must undo the view; verification runs
/// on an INDEPENDENT connection against <c>information_schema.views</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateViewProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateViewProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateView_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var create = await executor.Execute(
            "CREATE VIEW \"leaked_customers\" AS SELECT * FROM \"Customers\""
        );
        create.Success.Should().BeTrue(create.ErrorMessage);

        // Independent connection: the true durability oracle. If the CREATE
        // VIEW had leaked, the view would surface in information_schema.views
        // on any subsequent session.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM information_schema.views "
                + "WHERE table_name = 'leaked_customers'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

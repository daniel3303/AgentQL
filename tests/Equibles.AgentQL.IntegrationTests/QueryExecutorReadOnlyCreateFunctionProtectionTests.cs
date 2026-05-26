using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against code-backdoor planting — an LLM could emit
/// <c>CREATE FUNCTION "backdoor_fn"() ...</c> to leave a callable artifact in
/// <c>pg_proc</c> that survives every later session, optionally with
/// <c>SECURITY DEFINER</c> for privilege-escalation when later invoked. The
/// catalog path (<c>pg_proc</c>) is distinct from the role catalog written by
/// the already-pinned CREATE USER variant, and from every table-shape DDL pin
/// already in place. With <c>ReadOnly = true</c> the rollback must undo the
/// function; verification runs on an INDEPENDENT connection against
/// <c>pg_proc</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateFunctionProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateFunctionProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateFunction_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var create = await executor.Execute(
            "CREATE FUNCTION \"backdoor_fn\"() RETURNS integer AS 'SELECT 42' LANGUAGE SQL"
        );
        create.Success.Should().BeTrue(create.ErrorMessage);

        // Independent connection: the true durability oracle. If the CREATE
        // FUNCTION had leaked, the new function would appear in pg_proc and
        // be invokable from any later session.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM pg_proc WHERE proname = 'backdoor_fn'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

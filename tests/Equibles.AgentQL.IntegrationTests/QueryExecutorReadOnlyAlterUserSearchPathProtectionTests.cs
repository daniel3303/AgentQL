using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against role-attribute hijack — an LLM could emit
/// <c>ALTER USER postgres SET search_path TO 'attacker_schema, public'</c> to
/// plant a malicious default on the executor's role so subsequent unqualified
/// references in every future session resolve to attacker-controlled objects.
/// Distinct code path from the already-pinned CREATE USER variant — modifies
/// the existing role's <c>pg_db_role_setting</c> rather than creating a
/// <c>pg_authid</c> row. With <c>ReadOnly = true</c> the rollback must undo
/// the setting; verification runs on an INDEPENDENT connection against
/// <c>pg_roles.rolconfig</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyAlterUserSearchPathProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyAlterUserSearchPathProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyAlterUserSearchPath_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var hijack = await executor.Execute(
            "ALTER USER postgres SET search_path TO 'attacker_schema, public'"
        );
        hijack.Success.Should().BeTrue(hijack.ErrorMessage);

        // Independent connection: the true durability oracle. If the ALTER
        // USER had leaked, rolconfig would contain the planted search_path
        // string and bias every later unqualified reference.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM pg_roles "
                + "WHERE rolname = 'postgres' "
                + "AND rolconfig::text LIKE '%attacker_schema%'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against role-creation backdoors — an LLM with
/// superuser access could emit
/// <c>CREATE USER "backdoor" WITH SUPERUSER PASSWORD '...'</c> to plant a
/// persistent superuser across the entire cluster, surviving every later
/// session. Distinct keyword class (role management) and cluster-wide scope
/// (writes the shared <c>pg_authid</c> catalog, not a per-database one) from
/// every table/database-scope DDL pin already in place. With
/// <c>ReadOnly = true</c> the rollback must undo the role; PostgreSQL
/// supports transactional CREATE ROLE so this is testable. Verification runs
/// on an INDEPENDENT connection against <c>pg_roles</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateUserProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateUserProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateSuperuser_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var create = await executor.Execute("CREATE USER \"backdoor\" WITH SUPERUSER PASSWORD 'p'");
        create.Success.Should().BeTrue(create.ErrorMessage);

        // Independent connection: the true durability oracle. If the CREATE
        // USER had leaked, the new role would appear in pg_roles cluster-wide,
        // not just in this database.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM pg_roles WHERE rolname = 'backdoor'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

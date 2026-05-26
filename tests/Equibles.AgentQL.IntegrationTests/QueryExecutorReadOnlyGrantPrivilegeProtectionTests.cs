using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against privilege-system bypass — an LLM could emit
/// <c>GRANT ALL ON "Customers" TO PUBLIC</c> to open an audit-protected table
/// to every role in the cluster, a backdoor that does not touch rows or
/// catalog shape and would slip past any future allowlist that only inspects
/// DML or DDL keywords. PostgreSQL applies privilege changes transactionally,
/// so the <c>ReadOnly = true</c> rollback must undo the grant the same way it
/// undoes DML. Verification runs on an INDEPENDENT connection against
/// <c>information_schema.role_table_grants</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyGrantPrivilegeProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyGrantPrivilegeProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyGrantToPublic_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var grant = await executor.Execute(
            "GRANT SELECT, INSERT, UPDATE, DELETE ON \"Customers\" TO PUBLIC"
        );
        grant.Success.Should().BeTrue(grant.ErrorMessage);

        // Independent connection: the true durability oracle. If the GRANT
        // had leaked, the catalog probe would surface PUBLIC's privilege rows
        // on the Customers table.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM information_schema.role_table_grants "
                + "WHERE table_name = 'Customers' AND grantee = 'PUBLIC'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

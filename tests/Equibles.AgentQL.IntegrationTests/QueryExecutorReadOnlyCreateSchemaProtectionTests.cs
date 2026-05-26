using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against namespace-creation attacks — a fresh schema is
/// a clean container an LLM can stash hidden tables, functions or views in,
/// shielded from any guard that only inspects the <c>public</c> schema. The
/// catalog path (<c>pg_namespace</c>) is distinct from every table-shape,
/// role, function, and trigger pin already in place. With
/// <c>ReadOnly = true</c> the rollback must undo the schema; verification
/// runs on an INDEPENDENT connection against <c>information_schema.schemata</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateSchemaProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateSchemaProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateSchema_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var create = await executor.Execute("CREATE SCHEMA \"shadow_schema\"");
        create.Success.Should().BeTrue(create.ErrorMessage);

        // Independent connection: the true durability oracle. If the CREATE
        // SCHEMA had leaked, the namespace would appear in
        // information_schema.schemata and be usable from any later session.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM information_schema.schemata "
                + "WHERE schema_name = 'shadow_schema'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

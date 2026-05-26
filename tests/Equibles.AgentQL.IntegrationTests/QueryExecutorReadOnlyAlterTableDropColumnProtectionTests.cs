using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee for column-level DDL — an LLM could emit
/// <c>ALTER TABLE "Customers" DROP COLUMN "InternalNotes"</c> to permanently
/// remove an audit-only column marked <c>[AgentQLIgnore]</c>, destroying
/// evidence the model isn't supposed to see in the first place. PostgreSQL
/// has fully transactional DDL, so the <c>ReadOnly = true</c> rollback must
/// undo the schema change just like it undoes row mutations. The path through
/// the catalog for an ALTER is different from the DROP TABLE / TRUNCATE
/// variants already pinned. Verification runs on an INDEPENDENT connection so
/// the catalog is queried fresh.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyAlterTableDropColumnProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyAlterTableDropColumnProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyAlterTableDropColumn_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var alter = await executor.Execute(
            "ALTER TABLE \"Customers\" DROP COLUMN \"InternalNotes\""
        );
        alter.Success.Should().BeTrue(alter.ErrorMessage);

        // Independent connection: the true durability oracle. If the ALTER had
        // leaked, the catalog probe below would return zero rows for the
        // dropped column.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM information_schema.columns "
                + "WHERE table_name = 'Customers' AND column_name = 'InternalNotes'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(1);
    }
}

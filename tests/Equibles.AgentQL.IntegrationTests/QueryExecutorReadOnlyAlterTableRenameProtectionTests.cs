using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee for table-rename DDL — an LLM could emit
/// <c>ALTER TABLE "Customers" RENAME TO "Customers_renamed"</c> to break the
/// EF Core mapping by shifting the underlying table out from under the
/// application until someone notices and renames it back. Different catalog
/// code path from the already-pinned <c>ALTER TABLE DROP COLUMN</c> variant —
/// rename touches <c>pg_class</c> rather than <c>pg_attribute</c>. The
/// contract is the same — with <c>ReadOnly = true</c> the rollback must undo
/// the rename — and verification runs on an INDEPENDENT connection so the
/// catalog is queried fresh.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyAlterTableRenameProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyAlterTableRenameProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyAlterTableRename_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var rename = await executor.Execute(
            "ALTER TABLE \"Customers\" RENAME TO \"Customers_renamed\""
        );
        rename.Success.Should().BeTrue(rename.ErrorMessage);

        // Independent connection: the true durability oracle. If the rename
        // had leaked, the original table name would be absent and a query
        // against "Customers" would surface "relation does not exist".
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM information_schema.tables "
                + "WHERE table_name = 'Customers'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(1);
    }
}

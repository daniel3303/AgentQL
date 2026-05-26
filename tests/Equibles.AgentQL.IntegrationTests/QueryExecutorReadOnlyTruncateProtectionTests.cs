using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against fast-path data wipes: <c>TRUNCATE</c> bypasses
/// row-by-row deletion, skips triggers in some configurations, and on engines
/// like MySQL auto-commits outside the transaction entirely. PostgreSQL keeps
/// it transactional, so the <c>ReadOnly = true</c> rollback must neutralise
/// it just as it does for <c>DELETE</c>. The wipe path is structurally
/// different from DML rollback, so the guarantee is pinned independently.
/// Durability is verified on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyTruncateProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyTruncateProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyTruncateTable_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var truncate = await executor.Execute("TRUNCATE TABLE \"Bookings\"");
        truncate.Success.Should().BeTrue(truncate.ErrorMessage);

        // Independent connection: the true durability oracle for "did it persist".
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var count = await verify.Execute("SELECT COUNT(*) AS c FROM \"Bookings\"");

        Convert.ToInt32(count.Data![0]["c"]).Should().Be(3);
    }
}

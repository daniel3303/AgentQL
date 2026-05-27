using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// SQLite analogue of <see cref="QueryExecutorReadOnlyCommitInjectionProtectionTests"/>.
/// Verifies that <c>PRAGMA query_only = 1</c> applied at the executor's
/// connection makes SQLite reject the trailing INSERT when an embedded
/// COMMIT closes the executor's managed transaction. Each test owns a
/// fresh on-disk database so the durability check on an independent
/// connection cannot read uncommitted state.
/// </summary>
public sealed class QueryExecutorSqliteReadOnlyCommitInjectionProtectionTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"agentql-sqlite-readonly-{Guid.NewGuid():N}.db"
    );

    [Fact]
    public async Task Execute_ReadOnlyCommitInjectedThenInsert_DoesNotPersistAcrossConnections()
    {
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        await using (var executorContext = CreateContext())
        {
            var executor = new QueryExecutor<TravelTestDbContext>(
                executorContext,
                new AgentQLOptions { ReadOnly = true },
                NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
            );

            await executor.Execute(
                "SELECT 1; COMMIT; INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('Mallory', 99)"
            );
        }

        await using var verifyContext = CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var poisoned = await verify.Execute(
            "SELECT COUNT(*) AS c FROM \"Customers\" WHERE \"Name\" = 'Mallory'"
        );

        Convert.ToInt32(poisoned.Data![0]["c"]).Should().Be(0);
    }

    private TravelTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TravelTestDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;
        return new TravelTestDbContext(options);
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }
}

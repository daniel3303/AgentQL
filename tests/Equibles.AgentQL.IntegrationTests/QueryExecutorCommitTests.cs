using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the non-read-only commit branch of <see cref="QueryExecutor{TContext}"/>
/// (the <c>else =&gt; CommitAsync()</c> path). Every other test runs with the
/// default <c>ReadOnly = true</c>, so the commit branch is otherwise zero-hit:
/// a regression that rolls back instead of committing — or never commits —
/// would silently discard every write while still reporting success.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorCommitTests : IntegrationTestBase
{
    public QueryExecutorCommitTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_NotReadOnly_CommitsWritesSoTheyPersistAcrossConnections()
    {
        var options = new AgentQLOptions { ReadOnly = false };

        await using (var writeContext = Fixture.CreateContext())
        {
            var executor = new QueryExecutor<TravelTestDbContext>(
                writeContext,
                options,
                NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
            );

            var insert = await executor.Execute(
                "INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('Committed', 0)"
            );

            insert.Success.Should().BeTrue();
        }

        // A fresh context (new connection) only sees the row if it was committed,
        // not merely buffered in the now-disposed transaction.
        await using var verifyContext = Fixture.CreateContext();
        var verifier = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            options,
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var count = await verifier.Execute(
            "SELECT COUNT(*) AS c FROM \"Customers\" WHERE \"Name\" = 'Committed'"
        );

        Convert.ToInt32(count.Data![0]["c"]).Should().Be(1);
    }
}

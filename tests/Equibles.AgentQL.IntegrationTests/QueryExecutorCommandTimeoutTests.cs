using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins QueryExecutor's behaviour when a query exceeds CommandTimeout: the
/// timeout breaks the Npgsql connection, so the inner
/// <c>transaction.RollbackAsync()</c> itself throws and must be swallowed
/// (the rollback-failure catch). An LLM can easily emit an expensive query;
/// a regression here would surface as an unhandled exception escaping
/// <see cref="QueryExecutor{TContext}.Execute"/> instead of a clean
/// <c>QueryResult</c> error.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorCommandTimeoutTests : IntegrationTestBase
{
    public QueryExecutorCommandTimeoutTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_QueryExceedsCommandTimeout_ReturnsErrorWithoutThrowing()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions { CommandTimeout = 1 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT pg_sleep(5)");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}

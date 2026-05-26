using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Slow-query guarantee against CTE wrapping. A plain <c>SELECT pg_sleep(N)</c>
/// is already pinned; this covers the CTE-wrapped shape an LLM could
/// plausibly use to slip a long-running call past naive "no pg_sleep at the
/// top" guards. The defence is the same — <c>CommandTimeout</c> breaks the
/// connection and the executor must surface a clean <c>QueryResult</c> error
/// without leaking an exception out of <see cref="QueryExecutor{TContext}.Execute"/>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorCteWrappedSleepTimeoutTests : IntegrationTestBase
{
    public QueryExecutorCteWrappedSleepTimeoutTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_CteWrappedPgSleepExceedsTimeout_ReturnsErrorWithoutThrowing()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions { CommandTimeout = 1 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("WITH s AS (SELECT pg_sleep(5)) SELECT * FROM s");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}

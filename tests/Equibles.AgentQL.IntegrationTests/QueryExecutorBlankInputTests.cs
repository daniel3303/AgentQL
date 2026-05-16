using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Execute is the LLM tool boundary: its contract is to always return a
/// QueryResult, never throw. Whitespace-only input collapses to an empty
/// command — that degenerate path must surface as a clean error result, not an
/// exception escaping into the caller / model loop. Untested boundary.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorBlankInputTests : IntegrationTestBase
{
    public QueryExecutorBlankInputTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_WhitespaceOnlySql_ReturnsErrorWithoutThrowing()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions(),
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("   \n\t  ");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}

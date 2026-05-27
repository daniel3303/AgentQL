using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// MaxSqlLength is the upstream DoS defense: a query longer than the cap
/// is refused before sanitization and before the DBMS sees it, so neither
/// the executor's per-character walk nor the server's parse/plan stage
/// pays the cost. The LLM's output token budget already caps realistic
/// SQL well below the default 8 KB; this cap protects against buggy LLM
/// output or hand-typed pathological SQL.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorMaxSqlLengthTests : IntegrationTestBase
{
    public QueryExecutorMaxSqlLengthTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_SqlExceedsMaxLength_ReturnsErrorBeforeReachingServer()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true, MaxSqlLength = 64 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var sql = "SELECT " + string.Join(", ", Enumerable.Range(1, 50).Select(i => $"{i}"));

        var result = await executor.Execute(sql);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MaxSqlLength");
        result.ErrorMessage.Should().Contain("64");
        result.ErrorMessage.Should().Contain(sql.Length.ToString());
    }

    [Fact]
    public async Task Execute_SqlAtMaxLength_Succeeds()
    {
        await using var executorContext = Fixture.CreateContext();
        const string sql = "SELECT 1 AS x";
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true, MaxSqlLength = sql.Length },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute(sql);

        result.Success.Should().BeTrue(result.ErrorMessage);
    }
}

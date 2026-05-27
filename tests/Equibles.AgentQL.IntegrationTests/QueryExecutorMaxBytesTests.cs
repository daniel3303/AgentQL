using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// MaxBytes caps the cumulative byte cost of a result set per query. It is
/// checked per value as the read loop materialises columns, so a single
/// oversized BLOB or wide text column aborts on the spot rather than after
/// the row is built. The cap is the last defense before the result reaches
/// the LLM — MaxRows bounds row count and MaxColumns bounds column count,
/// but neither stops a 25-row result whose cells are huge strings.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorMaxBytesTests : IntegrationTestBase
{
    public QueryExecutorMaxBytesTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_PayloadExceedsMaxBytes_ReturnsErrorWithLimit()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true, MaxBytes = 100 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        // 1 KB string in a single SELECT — well past the 100-byte cap.
        var result = await executor.Execute("SELECT repeat('x', 1024) AS payload");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MaxBytes");
        result.ErrorMessage.Should().Contain("100");
    }

    [Fact]
    public async Task Execute_PayloadAcrossRowsExceedsMaxBytes_AbortsMidStream()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true, MaxBytes = 50 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        // 10 rows × 20-byte string = 200 bytes total, cap is 50.
        var result = await executor.Execute(
            "SELECT repeat('x', 20) AS payload FROM generate_series(1, 10)"
        );

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MaxBytes");
    }

    [Fact]
    public async Task Execute_SmallPayloadUnderCap_Succeeds()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true, MaxBytes = 1024 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT 1 AS a, 'hello' AS b");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data.Should().ContainSingle();
    }
}

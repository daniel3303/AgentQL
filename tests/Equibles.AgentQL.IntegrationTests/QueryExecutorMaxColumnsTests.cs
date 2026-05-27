using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// MaxColumns is an extraction-defense layer on top of MaxRows: a query
/// that asks for more columns than the cap is refused with an error
/// message, so the LLM gets a clear signal to narrow its projection list.
/// Without the cap, a SELECT * against a wide table would dump every
/// column of every row up to MaxRows — multiplying the bytes per row by
/// the table's column count.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorMaxColumnsTests : IntegrationTestBase
{
    public QueryExecutorMaxColumnsTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ResultExceedsMaxColumns_ReturnsErrorAndDoesNotMaterializeRows()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true, MaxColumns = 2 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT 1 AS a, 2 AS b, 3 AS c");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("3 columns");
        result.ErrorMessage.Should().Contain("MaxColumns");
        result.ErrorMessage.Should().Contain("2");
        result.Data.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Execute_ResultAtMaxColumns_Succeeds()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true, MaxColumns = 2 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT 1 AS a, 2 AS b");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data.Should().ContainSingle();
        result.Data![0].Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_SelectStarOnWideTable_RejectedWhenColumnsExceedCap()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            // The Customers table has Id, Name, Tier, InternalNotes — 4 columns.
            new AgentQLOptions { ReadOnly = true, MaxColumns = 3 },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT * FROM \"Customers\"");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("MaxColumns");
    }
}

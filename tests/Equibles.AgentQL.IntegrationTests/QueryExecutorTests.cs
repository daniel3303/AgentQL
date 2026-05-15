using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class QueryExecutorTests : IntegrationTestBase
{
    public QueryExecutorTests(PostgresFixture fixture)
        : base(fixture) { }

    private QueryExecutor<TravelTestDbContext> CreateExecutor(
        TravelTestDbContext context,
        AgentQLOptions? options = null
    )
    {
        return new QueryExecutor<TravelTestDbContext>(
            context,
            options ?? new AgentQLOptions(),
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );
    }

    [Fact]
    public async Task Execute_Select_ReturnsSeededRows()
    {
        await using var context = Fixture.CreateContext();
        var executor = CreateExecutor(context);

        var result = await executor.Execute("SELECT \"Destination\" FROM \"Bookings\"");

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(3);
        result.Data!.Select(r => r["Destination"]).Should().Contain("Lisbon");
    }

    [Fact]
    public async Task Execute_MaxRowsExceeded_TruncatesResult()
    {
        await using var context = Fixture.CreateContext();
        const string allBookings = "SELECT \"Id\" FROM \"Bookings\"";

        // Control: unrestricted, the table genuinely has 3 rows.
        var unrestricted = await CreateExecutor(context).Execute(allBookings);
        unrestricted.RowCount.Should().Be(3);

        // Capped well below the available row count so truncation is unambiguous.
        var capped = await CreateExecutor(context, new AgentQLOptions { MaxRows = 1 })
            .Execute(allBookings);

        capped.Success.Should().BeTrue();
        capped.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Execute_QueryWithComments_StripsThemAndRuns()
    {
        await using var context = Fixture.CreateContext();
        var executor = CreateExecutor(context);

        var result = await executor.Execute("SELECT 1 AS n -- inline comment\n/* block comment */");

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(1);
        Convert.ToInt32(result.Data![0]["n"]).Should().Be(1);
        result.ExecutedSql.Should().NotContain("comment");
    }

    [Fact]
    public async Task Execute_InvalidSql_ReturnsErrorResult()
    {
        await using var context = Fixture.CreateContext();
        var executor = CreateExecutor(context);

        var result = await executor.Execute("SELECT * FROM table_that_does_not_exist");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Execute_ReadOnly_RollsBackWrites()
    {
        await using var context = Fixture.CreateContext();
        var executor = CreateExecutor(context, new AgentQLOptions { ReadOnly = true });

        var insert = await executor.Execute(
            "INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('Temp', 0)"
        );
        insert.Success.Should().BeTrue();

        var count = await executor.Execute("SELECT COUNT(*) AS c FROM \"Customers\"");

        Convert.ToInt32(count.Data![0]["c"]).Should().Be(2);
    }
}

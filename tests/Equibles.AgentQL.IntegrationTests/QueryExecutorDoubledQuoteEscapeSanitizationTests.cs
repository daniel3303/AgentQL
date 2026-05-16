using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Sanitization contract: a doubled quote (<c>''</c>) inside a single-quoted
/// literal is an escaped quote, not a terminator. This is the canonical SQL
/// apostrophe-escape construct — if the scanner ended the literal at the first
/// inner quote it would treat the remainder as executable SQL. No existing
/// sanitization test contains a doubled-quote escape; this pins that branch.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorDoubledQuoteEscapeSanitizationTests : IntegrationTestBase
{
    public QueryExecutorDoubledQuoteEscapeSanitizationTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_LiteralWithDoubledQuoteEscape_RoundTripsApostrophe()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions(),
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT 'O''Brien' AS s");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data![0]["s"].Should().Be("O'Brien");
    }
}

using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Sanitization contract: a PostgreSQL <c>$tag$...$tag$</c> dollar-quoted run is
/// a string literal copied verbatim, so a <c>--</c> inside it is data, not
/// syntax — the comment stripper must never reach into the tagged run. Sibling
/// to the existing untagged <c>$$...$$</c> literal test; this pins the
/// identifier-tagged variant, the dollar-quote form used for SQL function
/// bodies and a classic "hide comment syntax inside string data" injection
/// shape the LLM could emit.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorTaggedDollarQuotedSanitizationTests : IntegrationTestBase
{
    public QueryExecutorTaggedDollarQuotedSanitizationTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_TaggedDollarQuotedLiteralContainingComment_PreservesLiteral()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions(),
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT $body$x -- not a comment$body$ AS s");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data![0]["s"].Should().Be("x -- not a comment");
    }
}

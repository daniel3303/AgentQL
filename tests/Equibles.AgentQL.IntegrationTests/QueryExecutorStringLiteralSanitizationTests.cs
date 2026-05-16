using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class QueryExecutorStringLiteralSanitizationTests : IntegrationTestBase
{
    public QueryExecutorStringLiteralSanitizationTests(PostgresFixture fixture)
        : base(fixture) { }

    // Contract: SanitizeQuery strips SQL *comments*. A "--" sequence inside a
    // single-quoted string literal is data, not a comment, so it must survive
    // sanitization untouched and the literal must round-trip through Execute.
    [Fact]
    public async Task Execute_StringLiteralContainingDoubleDash_PreservesLiteral()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions(),
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT '--x' AS s");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data![0]["s"].Should().Be("--x");
    }

    // PostgreSQL dollar-quoted strings are literals too: a "--" and the inner
    // spaces inside $$...$$ are data and must survive sanitization verbatim.
    [Fact]
    public async Task Execute_DollarQuotedLiteralContainingComment_PreservesLiteral()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions(),
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT $$a -- not a comment$$ AS s");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data![0]["s"].Should().Be("a -- not a comment");
    }
}

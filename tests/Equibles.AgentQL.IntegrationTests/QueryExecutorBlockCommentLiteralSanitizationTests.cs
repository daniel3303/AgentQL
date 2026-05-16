using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Sanitization contract: a <c>/* */</c> run inside a single-quoted string
/// literal is data, not syntax — the block-comment stripper must never reach
/// into a quoted run. Sibling to the existing <c>--</c>-in-literal tests; this
/// pins the block-comment variant, the classic "embed comment syntax inside
/// string data" LLM injection shape.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorBlockCommentLiteralSanitizationTests : IntegrationTestBase
{
    public QueryExecutorBlockCommentLiteralSanitizationTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_StringLiteralContainingBlockComment_PreservesLiteral()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions(),
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT '/* not a comment */' AS s");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data![0]["s"].Should().Be("/* not a comment */");
    }
}

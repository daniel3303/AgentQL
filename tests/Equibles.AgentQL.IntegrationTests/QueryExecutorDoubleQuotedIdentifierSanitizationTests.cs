using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Sanitization contract: a <c>--</c> inside a double-quoted identifier is
/// data, not syntax. Existing tests cover single-quoted and dollar-quoted
/// runs; the double-quoted-identifier branch of the quote scanner is the
/// untested third form the doc-comment promises to preserve — corrupting it
/// would mangle a column/table name an LLM legitimately quoted.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorDoubleQuotedIdentifierSanitizationTests : IntegrationTestBase
{
    public QueryExecutorDoubleQuotedIdentifierSanitizationTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_DoubleQuotedIdentifierContainingDoubleDash_PreservesIdentifier()
    {
        await using var context = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            context,
            new AgentQLOptions(),
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute("SELECT 1 AS \"a--b\"");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data![0].Should().ContainKey("a--b");
        Convert.ToInt32(result.Data![0]["a--b"]).Should().Be(1);
    }
}

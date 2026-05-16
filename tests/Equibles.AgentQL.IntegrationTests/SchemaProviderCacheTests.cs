using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the schema cache short-circuit of <see cref="SchemaProvider{TContext}"/>
/// (the early <c>return _cachedSchema;</c>). It is exercised only on a second
/// call, so no existing test reaches it. The schema is sent on every LLM tool
/// invocation; a regression that drops the cache would silently re-introspect
/// the entire EF model per call — a pure perf cliff, invisible to behavioural
/// tests because the output is unchanged.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderCacheTests : IntegrationTestBase
{
    public SchemaProviderCacheTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task GetSchemaDescription_CalledTwice_ReturnsCachedInstanceWithoutRecomputing()
    {
        await using var context = Fixture.CreateContext();
        var provider = new SchemaProvider<TravelTestDbContext>(context, new AgentQLOptions());

        var first = await provider.GetSchemaDescription();
        var second = await provider.GetSchemaDescription();

        // Reference equality, not value equality: a recompute would build a new
        // StringBuilder and produce a distinct string instance with the same
        // content, so only the cached-return path can satisfy this.
        ReferenceEquals(first, second).Should().BeTrue();
    }
}

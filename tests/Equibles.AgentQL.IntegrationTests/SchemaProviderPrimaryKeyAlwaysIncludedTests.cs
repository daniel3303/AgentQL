using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Contract (CLAUDE.md): "Primary keys and discriminator columns are always
/// included regardless of configuration." The strongest form of "regardless of
/// configuration" is an explicit fluent <c>Property(pk).Exclude()</c> on the
/// key itself — the schema must still expose it, otherwise an LLM loses the
/// join/filter column and cannot construct valid queries.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderPrimaryKeyAlwaysIncludedTests : IntegrationTestBase
{
    public SchemaProviderPrimaryKeyAlwaysIncludedTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task GetSchemaDescription_PrimaryKeyExplicitlyExcluded_StillIncludesPkColumn()
    {
        var options = new AgentQLOptions();
        options.Entity<Customer>().Property<Customer>(c => c.Id).Exclude();

        await using var context = Fixture.CreateContext();
        var provider = new SchemaProvider<TravelTestDbContext>(context, options);

        var schema = await provider.GetSchemaDescription();

        schema.Should().Contain("TABLE: \"Customers\"");
        schema.Should().MatchRegex(@"-\s*Id\s*\([^)]*\)\s*\[PRIMARY KEY\]");
    }
}

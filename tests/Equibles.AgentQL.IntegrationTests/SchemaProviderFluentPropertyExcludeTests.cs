using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the fluent property-level <c>Exclude()</c> resolution branch of
/// <see cref="SchemaProvider{TContext}"/> (ShouldIncludeProperty's
/// <c>return propConfig.Included.Value</c>). Existing tests cover the
/// <c>[AgentQLIgnore]</c> attribute and fluent entity include, but not a
/// fluent property exclude — a regression there would leak a column the caller
/// explicitly hid into the prompt the LLM sees (same data-exposure risk class
/// as [AgentQLIgnore]).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderFluentPropertyExcludeTests : IntegrationTestBase
{
    public SchemaProviderFluentPropertyExcludeTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task GetSchemaDescription_FluentPropertyExclude_OmitsThatColumnButKeepsTable()
    {
        var options = new AgentQLOptions();
        options.Entity<Customer>().Property<Customer>(c => c.Name).Exclude();

        await using var context = Fixture.CreateContext();
        var provider = new SchemaProvider<TravelTestDbContext>(context, options);

        var schema = await provider.GetSchemaDescription();

        // Table still present (it's a column exclude, not an entity exclude)…
        schema.Should().Contain("TABLE: \"Customers\"");
        // …but the explicitly-excluded Name column line must be gone.
        schema.Should().NotMatchRegex(@"-\s*Name\s*\(");
    }
}

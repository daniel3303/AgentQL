using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the entity-level <c>PropertyDefault</c> rung of the include/exclude
/// hierarchy in <see cref="SchemaProvider{TContext}"/> (ShouldIncludeProperty's
/// <c>return entityConfig.PropertyDefault == IncludeBehavior.IncludeAll</c>).
/// It is distinct from the per-property and attribute rungs other tests cover;
/// a regression here would ignore an entity's "exclude by default" intent and
/// leak every unannotated column to the LLM.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderEntityPropertyDefaultTests : IntegrationTestBase
{
    public SchemaProviderEntityPropertyDefaultTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task GetSchemaDescription_EntityPropertyDefaultExcludeAll_DropsUnannotatedColumnsKeepsPk()
    {
        var options = new AgentQLOptions();
        options.Entity<Customer>().PropertyDefault = IncludeBehavior.ExcludeAll;

        await using var context = Fixture.CreateContext();
        var provider = new SchemaProvider<TravelTestDbContext>(context, options);

        var schema = await provider.GetSchemaDescription();

        // Entity stays (no entity-level exclude) and its PK is always kept,
        // but unannotated Name falls through to the entity PropertyDefault.
        schema.Should().Contain("TABLE: \"Customers\"");
        schema.Should().MatchRegex(@"-\s*Id\s*\(");
        schema.Should().NotMatchRegex(@"-\s*Name\s*\(");
    }
}

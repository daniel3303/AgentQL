using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public class SchemaProviderTests
{
    private readonly PostgresFixture _fixture;

    public SchemaProviderTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<string> Describe(AgentQLOptions? options = null)
    {
        await using var context = _fixture.CreateContext();
        var provider = new SchemaProvider<TravelTestDbContext>(
            context,
            options ?? new AgentQLOptions()
        );
        return await provider.GetSchemaDescription();
    }

    [Fact]
    public async Task GetSchemaDescription_DetectsPostgresAndListsTables()
    {
        var schema = await Describe();

        schema.Should().Contain("Database: PostgreSQL");
        schema.Should().Contain("TABLE: \"Customers\"");
        schema.Should().Contain("TABLE: \"Bookings\"");
    }

    [Fact]
    public async Task GetSchemaDescription_ListsEnumValues()
    {
        var schema = await Describe();

        schema.Should().Contain("0 = Bronze");
        schema.Should().Contain("1 = Silver");
        schema.Should().Contain("2 = Gold");
    }

    [Fact]
    public async Task GetSchemaDescription_ListsForeignKeyRelationship()
    {
        var schema = await Describe();

        schema.Should().Contain("RELATIONSHIPS:");
        schema.Should().MatchRegex(@"CustomerId\s*->\s*Customers\.Id");
    }

    [Fact]
    public async Task GetSchemaDescription_IncludesEntityAttributeDescription()
    {
        var schema = await Describe();

        schema.Should().Contain("A trip booked by a customer.");
    }

    [Fact]
    public async Task GetSchemaDescription_ExcludesPropertyMarkedWithAgentQLIgnore()
    {
        var schema = await Describe();

        schema.Should().NotContain("InternalNotes");
    }

    [Fact]
    public async Task GetSchemaDescription_ExcludeAllDefault_DropsUnconfiguredEntities_ButKeepsAttributedOnes()
    {
        var options = new AgentQLOptions { DefaultBehavior = IncludeBehavior.ExcludeAll };

        var schema = await Describe(options);

        // Customer has no AgentQL attribute/config, so ExcludeAll hides it...
        schema.Should().NotContain("TABLE: \"Customers\"");
        // ...but Booking carries [AgentQLEntity], which always forces inclusion.
        schema.Should().Contain("TABLE: \"Bookings\"");
    }

    [Fact]
    public async Task GetSchemaDescription_ExcludeAllDefault_FluentIncludeReinstatesEntity()
    {
        var options = new AgentQLOptions { DefaultBehavior = IncludeBehavior.ExcludeAll };
        options.Entity<Customer>().Include();

        var schema = await Describe(options);

        schema.Should().Contain("TABLE: \"Customers\"");
    }
}

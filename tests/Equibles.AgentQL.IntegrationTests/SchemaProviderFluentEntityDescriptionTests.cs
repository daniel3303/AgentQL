using System.Text.RegularExpressions;
using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the fluent entity-description path of <see cref="SchemaProvider{TContext}"/>
/// (GetEntityDescription's <c>return config.Description</c>). Existing tests
/// cover the <c>[AgentQLEntity]</c> attribute description and the fluent
/// *property* description, but not a fluent *entity* description — a regression
/// there would drop the table-level guidance the LLM relies on to pick the
/// right table, with no other test catching it.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderFluentEntityDescriptionTests : IntegrationTestBase
{
    public SchemaProviderFluentEntityDescriptionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task GetSchemaDescription_FluentEntityDescription_RendersDescriptionLine()
    {
        const string description = "Registered travellers and their loyalty tier.";
        var options = new AgentQLOptions();
        options.Entity<Customer>().Description = description;

        await using var context = Fixture.CreateContext();
        var provider = new SchemaProvider<TravelTestDbContext>(context, options);

        var schema = await provider.GetSchemaDescription();

        // The fluent description must win and surface as the table's
        // DESCRIPTION line (Customer carries no [AgentQLEntity] attribute, so
        // only the fluent-config branch can produce this).
        schema
            .Should()
            .MatchRegex("TABLE: \"Customers\"\\s*\\r?\\nDESCRIPTION: " + Regex.Escape(description));
    }
}

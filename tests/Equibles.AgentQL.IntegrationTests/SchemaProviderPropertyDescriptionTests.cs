using System.Text.RegularExpressions;
using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the fluent per-property description path: a description set via
/// <c>Entity&lt;T&gt;().Property(...).Description</c> must reach the rendered
/// schema as the column's <c>-- &lt;text&gt;</c> suffix. That suffix is the
/// only per-column guidance the LLM ever sees; a regression in the fluent
/// lookup (GetPropertyDescription) or the description append in
/// AppendTableInfo would silently strip it — no test otherwise asserts a
/// column description is emitted at all.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderPropertyDescriptionTests : IntegrationTestBase
{
    public SchemaProviderPropertyDescriptionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task GetSchemaDescription_FluentPropertyDescription_RendersOnColumnLine()
    {
        const string description = "The customer's full legal name.";
        var options = new AgentQLOptions();
        options.Entity<Customer>().Property<Customer>(c => c.Name).Description = description;

        await using var context = Fixture.CreateContext();
        var provider = new SchemaProvider<TravelTestDbContext>(context, options);

        var schema = await provider.GetSchemaDescription();

        // The description must be attached to the Name column specifically,
        // via the "  - Name (...) ... -- <description>" line, not merely
        // present somewhere in the document.
        schema.Should().MatchRegex(@"-\s*Name\s*\([^\n]*--\s*" + Regex.Escape(description));
    }
}

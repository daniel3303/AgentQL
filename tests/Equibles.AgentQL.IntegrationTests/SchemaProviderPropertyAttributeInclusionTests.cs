using AwesomeAssertions;
using Equibles.AgentQL.Attributes;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the attribute rung of the include/exclude hierarchy in
/// <see cref="SchemaProvider{TContext}"/>: ShouldIncludeProperty's
/// <c>[AgentQLProperty] =&gt; return true</c> branch. With the entity defaulting
/// to ExcludeAll, only the explicitly-annotated property must survive — a
/// regression collapsing that branch would silently drop an opted-in column
/// from the schema the LLM sees.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderPropertyAttributeInclusionTests
{
    private readonly PostgresFixture _fixture;

    public SchemaProviderPropertyAttributeInclusionTests(PostgresFixture fixture) =>
        _fixture = fixture;

    [Fact]
    public async Task GetSchemaDescription_PropertyAttributeUnderExcludeAllEntity_KeepsOnlyAnnotatedColumn()
    {
        // Schema generation is pure EF model introspection; only GetDatabaseInfo
        // touches the connection, and it never reads these (uncreated) tables.
        var options = new DbContextOptionsBuilder<GadgetDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var context = new GadgetDbContext(options);
        var provider = new SchemaProvider<GadgetDbContext>(context, new AgentQLOptions());

        var schema = await provider.GetSchemaDescription();

        schema.Should().Contain("TABLE: \"Gadgets\"");
        schema.Should().MatchRegex(@"-\s*SerialNumber\s*\(");
        schema.Should().NotMatchRegex(@"-\s*Secret\s*\(");
    }
}

[AgentQLEntity(PropertyDefault = IncludeBehavior.ExcludeAll)]
public class Gadget
{
    public int Id { get; set; }

    [AgentQLProperty(Description = "Manufacturer serial number.")]
    public string SerialNumber { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;
}

public class GadgetDbContext : DbContext
{
    public GadgetDbContext(DbContextOptions<GadgetDbContext> options)
        : base(options) { }

    public DbSet<Gadget> Gadgets => Set<Gadget>();
}

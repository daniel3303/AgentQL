using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the owned-entity recursion in <c>GetAllPropertiesRecursive</c> (the
/// branch that walks an owned navigation's target type). No existing model has
/// an owned type, so that branch is zero-hit: a regression dropping it would
/// silently omit every owned column's enum mapping from the schema, leaving the
/// LLM unable to filter on those columns.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderOwnedEntityEnumTests
{
    private readonly PostgresFixture _fixture;

    public SchemaProviderOwnedEntityEnumTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetSchemaDescription_OwnedTypeWithEnum_ReportsTheOwnedEnumValues()
    {
        // Schema generation is pure EF model introspection; only GetDatabaseInfo
        // touches the connection, and it never reads these (uncreated) tables.
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var context = new TicketDbContext(options);
        var provider = new SchemaProvider<TicketDbContext>(context, new AgentQLOptions());

        var schema = await provider.GetSchemaDescription();

        // The Priority enum lives on the OWNED Metadata type; it only appears
        // if the owned navigation was recursed into.
        schema.Should().Contain("ENUM VALUES:");
        schema.Should().MatchRegex(@"Meta_Priority:\s*0 = Low, 1 = High");
    }
}

public enum Priority
{
    Low,
    High,
}

[Owned]
public class Metadata
{
    public Priority Priority { get; set; }
    public string Note { get; set; } = string.Empty;
}

public class Ticket
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Metadata Meta { get; set; } = new();
}

public class TicketDbContext : DbContext
{
    public TicketDbContext(DbContextOptions<TicketDbContext> options)
        : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
}

using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Contract: the schema "enumerates enum values" for enum columns. A nullable
/// enum column (<c>TEnum?</c>) is just as queryable as a non-nullable one, so
/// the LLM still needs its value list to filter on it. Only the non-nullable
/// branch is pinned today (Customer.Tier); this attacks the nullable branch of
/// GetEnumInformationForTable.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderNullableEnumTests
{
    private readonly PostgresFixture _fixture;

    public SchemaProviderNullableEnumTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetSchemaDescription_NullableEnumColumn_ReportsEnumValues()
    {
        // Schema generation is pure EF model introspection; the (uncreated)
        // table is never read, so no migration/seed is required.
        var options = new DbContextOptionsBuilder<SubscriptionDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var context = new SubscriptionDbContext(options);
        var provider = new SchemaProvider<SubscriptionDbContext>(context, new AgentQLOptions());

        var schema = await provider.GetSchemaDescription();

        schema.Should().Contain("ENUM VALUES:");
        schema.Should().MatchRegex(@"Plan:\s*0 = Free, 1 = Pro, 2 = Enterprise");
    }
}

public enum SubscriptionPlan
{
    Free,
    Pro,
    Enterprise,
}

public class Subscription
{
    public int Id { get; set; }
    public string Account { get; set; } = string.Empty;

    // Nullable enum — the branch under test.
    public SubscriptionPlan? Plan { get; set; }
}

public class SubscriptionDbContext : DbContext
{
    public SubscriptionDbContext(DbContextOptions<SubscriptionDbContext> options)
        : base(options) { }

    public DbSet<Subscription> Subscriptions => Set<Subscription>();
}

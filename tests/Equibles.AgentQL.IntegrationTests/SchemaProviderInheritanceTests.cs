using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// The shared travel model is flat, so the Table-Per-Hierarchy branch of
/// <see cref="SchemaProvider{TContext}"/> (AnalyzeInheritancePatterns +
/// GetInheritanceDetailsForTable) is otherwise never executed. This pins the
/// human-readable TPH section: a regression that drops the discriminator
/// mapping or the derived-class list would leave the LLM unable to tell which
/// rows belong to which subtype — silent, and invisible without this test.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderInheritanceTests
{
    private readonly PostgresFixture _fixture;

    public SchemaProviderInheritanceTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetSchemaDescription_TablePerHierarchyModel_ReportsTphPatternAndDiscriminator()
    {
        // Schema generation is pure EF model introspection; only GetDatabaseInfo
        // touches the connection, and it never reads these (uncreated) tables.
        var options = new DbContextOptionsBuilder<VehicleTphDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var context = new VehicleTphDbContext(options);
        var provider = new SchemaProvider<VehicleTphDbContext>(context, new AgentQLOptions());

        var schema = await provider.GetSchemaDescription();

        schema.Should().Contain("TABLE-PER-HIERARCHY: Table \"Vehicles\"");
        schema.Should().Contain("Base class: Vehicle");
        schema.Should().Contain("Car");
        schema.Should().Contain("Truck");
        schema.Should().MatchRegex(@"Discriminator column:\s*\w+");
        schema.Should().Contain("(Table-Per-Hierarchy pattern)");
    }
}

public abstract class Vehicle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Car : Vehicle
{
    public int Doors { get; set; }
}

public class Truck : Vehicle
{
    public decimal PayloadTons { get; set; }
}

public class VehicleTphDbContext : DbContext
{
    public VehicleTphDbContext(DbContextOptions<VehicleTphDbContext> options)
        : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // No table mapping overrides => EF Core defaults the hierarchy to TPH
        // with an auto-generated "Discriminator" column.
        modelBuilder.Entity<Vehicle>();
        modelBuilder.Entity<Car>();
        modelBuilder.Entity<Truck>();
    }
}

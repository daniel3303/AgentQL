using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// The shared travel model is flat and the existing inheritance test only
/// exercises TPH, so the Table-Per-Type branch of
/// <see cref="SchemaProvider{TContext}"/> (the TPT block of
/// AnalyzeInheritancePatterns + the TPT details in
/// GetInheritanceDetailsForTable) is otherwise never executed. Without the
/// "must always be joined" guidance the LLM emits queries against the derived
/// table alone and silently loses every inherited column.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SchemaProviderTptInheritanceTests
{
    private readonly PostgresFixture _fixture;

    public SchemaProviderTptInheritanceTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetSchemaDescription_TablePerTypeModel_ReportsTptPatternAndJoinRequirement()
    {
        // Schema generation is pure EF model introspection; only GetDatabaseInfo
        // touches the connection, and it never reads these (uncreated) tables.
        var options = new DbContextOptionsBuilder<StaffTptDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        await using var context = new StaffTptDbContext(options);
        var provider = new SchemaProvider<StaffTptDbContext>(context, new AgentQLOptions());

        var schema = await provider.GetSchemaDescription();

        schema.Should().Contain("TABLE-PER-TYPE: Employee class (table: Employees)");
        schema.Should().Contain("Base table \"Employees\" contains common properties");
        schema
            .Should()
            .MatchRegex(
                "Derived table \"Managers\" must always be joined with table \"Employees\""
            );
        schema.Should().Contain("(Table-Per-Type pattern)");
    }
}

public abstract class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Manager : Employee
{
    public string Department { get; set; } = string.Empty;
}

public class Engineer : Employee
{
    public string Stack { get; set; } = string.Empty;
}

public class StaffTptDbContext : DbContext
{
    public StaffTptDbContext(DbContextOptions<StaffTptDbContext> options)
        : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // UseTptMappingStrategy maps each class to its own table, so the
        // derived entities get a BaseType with a different table name —
        // exactly the shape the TPT branch keys on.
        modelBuilder.Entity<Employee>().UseTptMappingStrategy().ToTable("Employees");
        modelBuilder.Entity<Manager>().ToTable("Managers");
        modelBuilder.Entity<Engineer>().ToTable("Engineers");
    }
}

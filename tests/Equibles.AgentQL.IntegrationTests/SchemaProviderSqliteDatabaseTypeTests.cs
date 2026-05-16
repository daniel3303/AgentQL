using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Contract (CLAUDE.md): the schema header detects the database type for each
/// supported provider — SQL Server, MySQL, PostgreSQL, SQLite, Oracle. SQLite
/// is a first-class supported provider (the Demo app runs on it) yet the
/// SQLite arm of GetDatabaseTypeFromProvider was zero-hit: every other test
/// runs on PostgreSQL. This pins the SQLite detection end-to-end.
/// </summary>
public class SchemaProviderSqliteDatabaseTypeTests
{
    private sealed class Trip
    {
        public int Id { get; set; }
        public string Destination { get; set; } = string.Empty;
    }

    private sealed class SqliteTripContext : DbContext
    {
        public SqliteTripContext(DbContextOptions<SqliteTripContext> options)
            : base(options) { }

        public DbSet<Trip> Trips => Set<Trip>();
    }

    [Fact]
    public async Task GetSchemaDescription_SqliteProvider_ReportsSqliteAsDatabaseType()
    {
        var options = new DbContextOptionsBuilder<SqliteTripContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using var context = new SqliteTripContext(options);
        var provider = new SchemaProvider<SqliteTripContext>(context, new AgentQLOptions());

        var schema = await provider.GetSchemaDescription();

        schema.Should().Contain("Database: SQLite");
        schema.Should().Contain("TABLE: \"Trips\"");
    }
}

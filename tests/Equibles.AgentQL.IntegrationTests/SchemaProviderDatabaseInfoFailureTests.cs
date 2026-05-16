using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Pins the graceful-degradation path of <c>GetDatabaseInfo</c>: when the
/// connection cannot be opened it must be caught and reported as
/// "Unknown Database (Error: ...)" so schema generation still returns the
/// model structure. A regression letting that exception escape would make the
/// whole schema unavailable to the LLM whenever the database is briefly
/// unreachable — no fixture needed, this is pure model introspection plus a
/// dead connection.
/// </summary>
public class SchemaProviderDatabaseInfoFailureTests
{
    [Fact]
    public async Task GetSchemaDescription_ConnectionUnreachable_ReportsUnknownDatabaseButStillListsTables()
    {
        // Routable-but-dead endpoint with a 1s connect timeout so OpenAsync
        // fails fast inside GetDatabaseInfo's try/catch.
        var options = new DbContextOptionsBuilder<TravelTestDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=none;Username=u;Password=p;Timeout=1;Command Timeout=1"
            )
            .Options;
        await using var context = new TravelTestDbContext(options);
        var provider = new SchemaProvider<TravelTestDbContext>(context, new AgentQLOptions());

        var schema = await provider.GetSchemaDescription();

        schema.Should().Contain("Database: Unknown Database (Error:");
        // Model introspection is connection-independent, so tables still render.
        schema.Should().Contain("TABLE: \"Customers\"");
    }
}

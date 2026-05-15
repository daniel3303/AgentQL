using System.Text.RegularExpressions;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Spins up a real PostgreSQL instance via Testcontainers, applies the test
/// schema, and seeds a small, deterministic data set shared by all tests in
/// the collection.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = default!;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        _container = BuildContainer();
        await _container.StartAsync();

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();

        var ada = new Customer { Name = "Ada Lovelace", Tier = LoyaltyTier.Gold };
        var alan = new Customer
        {
            Name = "Alan Turing",
            Tier = LoyaltyTier.Silver,
            InternalNotes = "VIP - do not surface to the model",
        };

        context.Customers.AddRange(ada, alan);
        context.Bookings.AddRange(
            new Booking
            {
                Destination = "Lisbon",
                Price = 1200.50m,
                Customer = ada,
            },
            new Booking
            {
                Destination = "Tokyo",
                Price = 3400.00m,
                Customer = ada,
            },
            new Booking
            {
                Destination = "Lisbon",
                Price = 900.00m,
                Customer = alan,
            }
        );

        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Testcontainers' image-name parser uses a regex with a hard 1-second
    /// timeout. The first (cold, pre-JIT) match can exceed that on slower or
    /// arm64 hosts; the regex is fast once warmed, so a short retry loop makes
    /// container construction reliable.
    /// </summary>
    private static PostgreSqlContainer BuildContainer()
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return new PostgreSqlBuilder("postgres:16-alpine").Build();
            }
            catch (RegexMatchTimeoutException) when (attempt < maxAttempts) { }
        }

        throw new InvalidOperationException(
            $"Could not build the PostgreSQL test container after {maxAttempts} "
                + "attempts (Testcontainers image-name regex kept timing out)."
        );
    }

    public TravelTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TravelTestDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TravelTestDbContext(options);
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;

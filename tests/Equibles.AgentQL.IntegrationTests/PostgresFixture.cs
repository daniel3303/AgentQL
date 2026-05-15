using System.Text.RegularExpressions;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Spins up a real PostgreSQL instance via Testcontainers and applies the test
/// schema once. Per-test isolation is provided by <see cref="ResetAsync"/>,
/// which truncates user tables with Respawn and re-seeds a small,
/// deterministic data set — so isolation no longer depends on the code under
/// test rolling its transaction back.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = default!;
    private Respawner _respawner = default!;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        _container = BuildContainer();
        await _container.StartAsync(ct);

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(ct);

        // Intentionally created before any seed data exists: the Postgres
        // adapter discovers tables from information_schema, not from rows, so
        // building the Respawner here (post-EnsureCreated, pre-seed) is correct
        // and is what lets every test reset to a clean baseline.
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions { DbAdapter = DbAdapter.Postgres }
        );
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Truncates every user table and restores the canonical seed data.
    /// Called once per test (xUnit v3 creates a fresh test-class instance per
    /// test) from <see cref="IntegrationTestBase"/>.
    /// </summary>
    public async Task ResetAsync()
    {
        var ct = TestContext.Current.CancellationToken;

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        await _respawner.ResetAsync(connection);

        await SeedAsync(ct);
    }

    public TravelTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TravelTestDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new TravelTestDbContext(options);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var context = CreateContext();

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

        await context.SaveChangesAsync(cancellationToken);
    }

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
}

/// <summary>
/// Every database-touching integration test MUST join this single collection.
/// The fixture owns one shared PostgreSQL instance and performs a global
/// Respawn truncate + reseed per test, which is not reentrant — running tests
/// in parallel against it would corrupt state. Parallelism is disabled via
/// <c>xunit.runner.json</c>; keeping all DB tests in one collection keeps that
/// guarantee intact.
/// </summary>
[CollectionDefinition(nameof(PostgresCollection))]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;

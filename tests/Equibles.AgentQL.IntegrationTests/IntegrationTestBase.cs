using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Resets the shared PostgreSQL database to the canonical seed before every
/// test (xUnit v3 instantiates the test class once per test, so
/// <see cref="InitializeAsync"/> runs per test). Guarantees isolation
/// independently of how the code under test handles transactions.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; }

    protected IntegrationTestBase(PostgresFixture fixture) => Fixture = fixture;

    public async ValueTask InitializeAsync() => await Fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

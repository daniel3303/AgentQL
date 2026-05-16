using AwesomeAssertions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Extensions;

/// <summary>
/// Pins the default arm of <c>AddAgentQLChat</c>'s provider switch — the
/// <c>_ =&gt; throw new ArgumentOutOfRangeException</c> guard, previously
/// zero-hit. If that throw is ever weakened to a silent fallback, a
/// misconfigured provider would resolve a wrong/broken chat client instead of
/// failing fast at startup.
/// </summary>
public class ServiceCollectionExtensionsInvalidProviderTests
{
    private sealed class StubDbContext : DbContext
    {
        public StubDbContext(DbContextOptions<StubDbContext> options)
            : base(options) { }
    }

    [Fact]
    public void AddAgentQLChat_UnknownProvider_ThrowsArgumentOutOfRangeOnResolve()
    {
        var services = new ServiceCollection();
        services.AddAgentQLChat<StubDbContext>(configureChat: o =>
        {
            o.Provider = (AiProvider)999;
            o.ApiKey = "test-key";
            o.ModelName = "model";
        });

        using var provider = services.BuildServiceProvider();
        var resolve = () => provider.GetRequiredService<IChatClient>();

        resolve.Should().Throw<ArgumentOutOfRangeException>();
    }
}

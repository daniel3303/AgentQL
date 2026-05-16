using AwesomeAssertions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Extensions;

/// <summary>
/// Pins the OpenAI arm of <c>AddAgentQLChat</c>'s <see cref="IChatClient"/>
/// factory together with <c>CreateOpenAIClient</c> (credential + endpoint +
/// client construction), all zero-hit. A regression mapping
/// <see cref="AiProvider.OpenAI"/> to the wrong factory, or breaking the
/// default-endpoint resolution, would silently leave the agent talking to the
/// wrong service with no compile-time signal.
/// </summary>
public class ServiceCollectionExtensionsOpenAIProviderTests
{
    private sealed class StubDbContext : DbContext
    {
        public StubDbContext(DbContextOptions<StubDbContext> options)
            : base(options) { }
    }

    [Fact]
    public void AddAgentQLChat_OpenAIProvider_ResolvesFunctionInvokingChatClient()
    {
        var services = new ServiceCollection();
        services.AddAgentQLChat<StubDbContext>(configureChat: o =>
        {
            o.Provider = AiProvider.OpenAI;
            o.ApiKey = "test-key";
            o.ModelName = "gpt-4o-mini";
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatClient>();

        // The FunctionInvokingChatClient decorator only exists if the OpenAI
        // inner client was built via CreateOpenAIClient and then wrapped — a
        // default fall-through would not produce it.
        client.Should().NotBeNull();
        client.GetService(typeof(FunctionInvokingChatClient)).Should().NotBeNull();
    }
}

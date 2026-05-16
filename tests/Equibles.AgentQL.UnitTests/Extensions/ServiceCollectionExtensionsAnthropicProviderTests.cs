using AwesomeAssertions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Extensions;

/// <summary>
/// Pins the Anthropic arm of <c>AddAgentQLChat</c>'s <see cref="IChatClient"/>
/// factory. The whole file was zero-hit: a regression mapping
/// <see cref="AiProvider.Anthropic"/> to the wrong factory — or breaking the
/// function-invocation wrapper — would leave the agent silently unable to call
/// tools, with no compile-time signal.
/// </summary>
public class ServiceCollectionExtensionsAnthropicProviderTests
{
    private sealed class StubDbContext : DbContext
    {
        public StubDbContext(DbContextOptions<StubDbContext> options)
            : base(options) { }
    }

    [Fact]
    public void AddAgentQLChat_AnthropicProvider_ResolvesFunctionInvokingChatClient()
    {
        var services = new ServiceCollection();
        services.AddAgentQLChat<StubDbContext>(configureChat: o =>
        {
            o.Provider = AiProvider.Anthropic;
            o.ApiKey = "test-key";
            o.ModelName = "claude-sonnet-4-5";
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatClient>();

        // FunctionInvokingChatClient is the decorator UseFunctionInvocation()
        // adds; its presence proves the Anthropic inner client was built and
        // wrapped, not that some default fell through.
        client.Should().NotBeNull();
        client.GetService(typeof(FunctionInvokingChatClient)).Should().NotBeNull();
    }
}

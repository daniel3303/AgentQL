using AwesomeAssertions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Extensions;

/// <summary>
/// Pins the Ollama arm of <c>AddAgentQLChat</c>'s <see cref="IChatClient"/>
/// factory (the <c>AiProvider.Ollama =&gt; CreateOpenAIClient</c> switch case),
/// previously zero-hit. Ollama deliberately reuses the OpenAI-compatible
/// client; a regression routing it elsewhere — or losing the function-invocation
/// wrapper — would silently break local-LLM users with no compile-time signal.
/// </summary>
public class ServiceCollectionExtensionsOllamaProviderTests
{
    private sealed class StubDbContext : DbContext
    {
        public StubDbContext(DbContextOptions<StubDbContext> options)
            : base(options) { }
    }

    [Fact]
    public void AddAgentQLChat_OllamaProvider_ResolvesFunctionInvokingChatClient()
    {
        var services = new ServiceCollection();
        services.AddAgentQLChat<StubDbContext>(configureChat: o =>
        {
            o.Provider = AiProvider.Ollama;
            o.ApiKey = "ollama";
            o.ModelName = "llama3";
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IChatClient>();

        // The decorator only exists if the Ollama case built the OpenAI-compat
        // inner client and wrapped it — a default fall-through would not.
        client.Should().NotBeNull();
        client.GetService(typeof(FunctionInvokingChatClient)).Should().NotBeNull();
    }
}

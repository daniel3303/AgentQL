using AwesomeAssertions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Configuration;

public class AgentQLChatOptionsTests
{
    [Theory]
    [InlineData(AiProvider.OpenAI, "https://api.openai.com/v1")]
    [InlineData(AiProvider.Ollama, "http://localhost:11434")]
    [InlineData(AiProvider.Anthropic, "https://api.anthropic.com/v1")]
    public void GetEndpoint_NoExplicitEndpoint_ReturnsProviderDefault(
        AiProvider provider,
        string expected
    )
    {
        var options = new AgentQLChatOptions { Provider = provider };

        options.GetEndpoint().Should().Be(expected);
    }

    [Fact]
    public void GetEndpoint_ExplicitEndpoint_OverridesProviderDefault()
    {
        var options = new AgentQLChatOptions
        {
            Provider = AiProvider.OpenAI,
            Endpoint = "http://localhost:1234/v1",
        };

        options.GetEndpoint().Should().Be("http://localhost:1234/v1");
    }

    [Fact]
    public void Defaults_AreProductionSafe()
    {
        var options = new AgentQLChatOptions();

        options.Provider.Should().Be(AiProvider.OpenAI);
        options.MaxOutputTokens.Should().Be(4096);
        options.SystemPrompt.Should().Contain("GetDatabaseSchema");
    }
}

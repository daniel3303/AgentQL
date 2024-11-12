using System.ClientModel;
using Anthropic;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Extensions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace Equibles.AgentQL.MicrosoftAI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentQLChat<TContext>(
        this IServiceCollection services,
        Action<AgentQLOptions> configureAgentQL = null,
        Action<AgentQLChatOptions> configureChat = null) where TContext : DbContext
    {
        services.AddAgentQL<TContext>(configureAgentQL);

        var chatOptions = new AgentQLChatOptions();
        configureChat?.Invoke(chatOptions);
        services.AddSingleton(chatOptions);

        services.AddScoped<AgentQLPlugin>();

        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<AgentQLChatOptions>();
            IChatClient innerClient = options.Provider switch
            {
                AiProvider.OpenAI => CreateOpenAIClient(options),
                AiProvider.Ollama => CreateOpenAIClient(options),
                AiProvider.Anthropic => CreateAnthropicClient(options),
                _ => throw new ArgumentOutOfRangeException(nameof(options.Provider))
            };

            return new ChatClientBuilder(innerClient)
                .UseFunctionInvocation()
                .Build();
        });

        return services;
    }

    private static IChatClient CreateOpenAIClient(AgentQLChatOptions options)
    {
        var credential = new ApiKeyCredential(options.ApiKey);
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(options.GetEndpoint()) };
        var client = new OpenAIClient(credential, clientOptions);

        return client.GetChatClient(options.ModelName).AsIChatClient();
    }

    private static IChatClient CreateAnthropicClient(AgentQLChatOptions options)
    {
        var client = new AnthropicClient { ApiKey = options.ApiKey };
        return client.AsIChatClient(options.ModelName);
    }
}

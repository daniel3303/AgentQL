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
        Action<AgentQLChatOptions> configureChat = null,
        Action<AgentQLSelfCorrectionOptions> configureSelfCorrection = null
    )
        where TContext : DbContext
    {
        services.AddAgentQL<TContext>(configureAgentQL);

        var chatOptions = new AgentQLChatOptions();
        configureChat?.Invoke(chatOptions);
        services.AddSingleton(chatOptions);

        var selfCorrectionOptions = new AgentQLSelfCorrectionOptions();
        configureSelfCorrection?.Invoke(selfCorrectionOptions);

        services.AddScoped<AgentQLPlugin>();

        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<AgentQLChatOptions>();
            IChatClient innerClient = options.Provider switch
            {
                AiProvider.OpenAI => CreateOpenAIClient(options),
                AiProvider.Ollama => CreateOpenAIClient(options),
                AiProvider.Anthropic => CreateAnthropicClient(options),
                _ => throw new ArgumentOutOfRangeException(nameof(options.Provider)),
            };

            // Self-correction is added before function invocation so it ends up
            // the outer wrapper around the whole tool loop (see
            // ChatClientBuilderExtensions.UseAgentQLSelfCorrection).
            return new ChatClientBuilder(innerClient)
                .UseAgentQLSelfCorrection(selfCorrectionOptions)
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

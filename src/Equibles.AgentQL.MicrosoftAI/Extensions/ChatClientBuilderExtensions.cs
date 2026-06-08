using Equibles.AgentQL.MicrosoftAI.Configuration;
using Microsoft.Extensions.AI;

namespace Equibles.AgentQL.MicrosoftAI.Extensions;

public static class ChatClientBuilderExtensions
{
    /// <summary>
    /// Adds the AgentQL self-correction guard to the pipeline. It re-prompts the
    /// model with a system reminder whenever a turn ends without an answer or a
    /// reported failure, so the agent can never return an empty result.
    /// <para>
    /// Add this BEFORE <see cref="FunctionInvocationChatClientBuilderExtensions.UseFunctionInvocation"/>
    /// on the builder — the builder applies factories inner-to-outer, so the
    /// guard must be registered first to end up wrapping the function-invocation
    /// loop.
    /// </para>
    /// </summary>
    public static ChatClientBuilder UseAgentQLSelfCorrection(
        this ChatClientBuilder builder,
        AgentQLSelfCorrectionOptions options = null
    )
    {
        return builder.Use(inner => new SelfCorrectingChatClient(
            inner,
            options ?? new AgentQLSelfCorrectionOptions()
        ));
    }
}

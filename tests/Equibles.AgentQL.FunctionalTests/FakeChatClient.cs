using System.Runtime.CompilerServices;
using Equibles.AgentQL.MicrosoftAI;
using Microsoft.Extensions.AI;

namespace Equibles.AgentQL.FunctionalTests;

/// <summary>
/// Deterministic stand-in for a real LLM. Turn 1: emit an ExecuteQuery
/// function call (driving AgentQL's tools through the real
/// FunctionInvokingChatClient the Demo wires up). Turn 2: see the tool result
/// fed back and produce the final text answer the chat UI renders. No network,
/// no API key.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly string _sqlToRequest;

    public FakeChatClient(string sqlToRequest) => _sqlToRequest = sqlToRequest;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var toolResult = messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .LastOrDefault();

        if (toolResult is null)
        {
            var call = new FunctionCallContent(
                callId: "call-1",
                name: nameof(AgentQLPlugin.ExecuteQuery),
                arguments: new Dictionary<string, object?> { ["sqlQuery"] = _sqlToRequest }
            );
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
        }

        var answer = new ChatMessage(ChatRole.Assistant, $"Result: {toolResult.Result}");
        return Task.FromResult(new ChatResponse(answer));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
        {
            yield return new ChatResponseUpdate(message.Role, message.Contents);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

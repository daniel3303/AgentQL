using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Equibles.AgentQL.IntegrationTests.Fakes;

/// <summary>
/// A deterministic LLM stand-in that replays a fixed script of assistant turns,
/// one per call — used to drive the self-correction guard through the real
/// function-invocation pipeline and the real query executor.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<ChatMessage> _turns;

    public ScriptedChatClient(IEnumerable<ChatMessage> turns) =>
        _turns = new Queue<ChatMessage>(turns);

    public int CallCount { get; private set; }

    public static ChatMessage ExecuteQueryCall(string callId, string sql)
    {
        var call = new FunctionCallContent(
            callId,
            nameof(Equibles.AgentQL.MicrosoftAI.AgentQLPlugin.ExecuteQuery),
            new Dictionary<string, object?> { ["sqlQuery"] = sql }
        );
        return new ChatMessage(ChatRole.Assistant, [call]);
    }

    public static ChatMessage Say(string text) => new(ChatRole.Assistant, text);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        CallCount++;
        var next = _turns.Count > 0 ? _turns.Dequeue() : Say(string.Empty);
        return Task.FromResult(new ChatResponse(next));
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

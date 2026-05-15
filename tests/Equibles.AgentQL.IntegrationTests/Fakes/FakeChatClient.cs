using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Equibles.AgentQL.IntegrationTests.Fakes;

/// <summary>
/// A deterministic stand-in for a real LLM. On the first turn it emits a
/// function call (driving AgentQL's tools through the real
/// <c>FunctionInvokingChatClient</c> pipeline); on the next turn it sees the
/// tool result that was fed back and produces a final text answer. This lets
/// the end-to-end tool loop be exercised without any network or API key.
/// </summary>
public class FakeChatClient : IChatClient
{
    private readonly string _sqlToRequest;

    public FakeChatClient(string sqlToRequest) => _sqlToRequest = sqlToRequest;

    public int CallCount { get; private set; }

    public string? CapturedToolResult { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        CallCount++;

        var toolResult = messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .LastOrDefault();

        if (toolResult is null)
        {
            var call = new FunctionCallContent(
                callId: "call-1",
                name: nameof(Equibles.AgentQL.MicrosoftAI.AgentQLPlugin.ExecuteQuery),
                arguments: new Dictionary<string, object?> { ["sqlQuery"] = _sqlToRequest }
            );

            var assistant = new ChatMessage(ChatRole.Assistant, [call]);
            return Task.FromResult(new ChatResponse(assistant));
        }

        CapturedToolResult = toolResult.Result?.ToString();

        var answer = new ChatMessage(ChatRole.Assistant, $"Result: {CapturedToolResult}");
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

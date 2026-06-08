using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Equibles.AgentQL.UnitTests.Fakes;

/// <summary>
/// A deterministic LLM stand-in that replays a fixed script of assistant turns,
/// one per call. A turn is either a tool call (which the real
/// <c>FunctionInvokingChatClient</c> executes before calling back) or a final
/// text message. It records the <c>&lt;system-reminder&gt;</c> messages the
/// guard injected (from the most recent call's history, which accumulates them
/// all), so tests can assert how many nudges happened.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<ChatMessage> _turns;
    private List<string> _reminders = [];

    public ScriptedChatClient(IEnumerable<ChatMessage> turns) =>
        _turns = new Queue<ChatMessage>(turns);

    public int CallCount { get; private set; }

    // Reminders are injected as separate user messages with identical text, so
    // they're counted (not de-duped) from the most recent call's history — the
    // last call sees every reminder injected over the whole run.
    public IReadOnlyList<string> Reminders => _reminders;

    public static ChatMessage ToolCall(
        string name,
        string callId,
        IDictionary<string, object> arguments
    )
    {
        return new ChatMessage(
            ChatRole.Assistant,
            [new FunctionCallContent(callId, name, arguments)]
        );
    }

    public static ChatMessage Say(string text) => new(ChatRole.Assistant, text);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options = null,
        CancellationToken cancellationToken = default
    )
    {
        CallCount++;

        _reminders = messages
            .Where(m =>
                m.Role == ChatRole.User && m.Text != null && m.Text.Contains("<system-reminder>")
            )
            .Select(m => m.Text)
            .ToList();

        var next = _turns.Count > 0 ? _turns.Dequeue() : Say(string.Empty);
        return Task.FromResult(new ChatResponse(next));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var message in response.Messages)
        {
            yield return new ChatResponseUpdate(message.Role, message.Contents);
        }
    }

    public object GetService(Type serviceType, object serviceKey = null) => null;

    public void Dispose() { }
}

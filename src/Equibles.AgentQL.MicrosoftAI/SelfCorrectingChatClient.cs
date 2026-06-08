using System.Runtime.CompilerServices;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Equibles.AgentQL.MicrosoftAI;

/// <summary>
/// A delegating chat client that refuses to let the query agent end its turn
/// unresolved.
/// <para>
/// The underlying <c>FunctionInvokingChatClient</c> ends the tool loop the
/// moment the model stops calling tools — including when it gives up with an
/// empty message after a failed query. This client wraps that loop and inspects
/// the terminal turn: if the model answered nothing, or stopped right after an
/// <c>ExecuteQuery</c> error without retrying or calling <c>ReportFailure</c>,
/// it injects a <c>&lt;system-reminder&gt;</c> message — restating the question
/// and telling the model to read the error, fix the SQL and retry, or report a
/// failure — then re-runs the loop, up to
/// <see cref="AgentQLSelfCorrectionOptions.MaxAttempts"/> times. If still
/// unresolved, it returns <see cref="AgentQLSelfCorrectionOptions.ExhaustionMessage"/>
/// so the caller never receives an empty answer.
/// </para>
/// <para>
/// Must sit ABOVE function invocation in the pipeline: add it to the builder
/// BEFORE <c>UseFunctionInvocation()</c> so it ends up the outer wrapper.
/// </para>
/// </summary>
public class SelfCorrectingChatClient : DelegatingChatClient
{
    public const string ExecuteQueryToolName = nameof(AgentQLPlugin.ExecuteQuery);
    public const string ReportFailureToolName = nameof(AgentQLPlugin.ReportFailure);

    // Copied out of the options object so a singleton instance is never affected
    // by later mutation of the shared (publicly settable) options.
    private readonly int _maxAttempts;
    private readonly string _exhaustionMessage;

    public SelfCorrectingChatClient(IChatClient innerClient, AgentQLSelfCorrectionOptions options)
        : base(innerClient)
    {
        options ??= new AgentQLSelfCorrectionOptions();
        _maxAttempts = options.MaxAttempts;
        _exhaustionMessage = options.ExhaustionMessage;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options = null,
        CancellationToken cancellationToken = default
    )
    {
        var history = messages.ToList();
        var question = ExtractQuestion(history);

        var response = await base.GetResponseAsync(history, options, cancellationToken);

        var attempts = 0;
        while (attempts < _maxAttempts && IsUnresolved(history, response))
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            // FunctionInvokingChatClient returns only the messages it generated
            // this turn (assistant tool calls, tool results, final text), so
            // appending them rebuilds the running conversation without duplicating
            // the input.
            history.AddRange(response.Messages);
            history.Add(BuildReminder(question));
            response = await base.GetResponseAsync(history, options, cancellationToken);
        }

        if (IsUnresolved(history, response))
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, _exhaustionMessage));
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        // The guard needs the whole terminal turn to classify it, so true token
        // streaming with mid-stream re-injection isn't possible. Run the guarded
        // loop to completion, then surface the final transcript as updates.
        var response = await GetResponseAsync(messages, options, cancellationToken);
        foreach (var update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }

    /// <summary>
    /// A turn is unresolved when the agent stopped without a usable result: an
    /// empty answer, or a non-empty answer when every <c>ExecuteQuery</c> the
    /// agent ran errored (so the answer can't be grounded in data). If any query
    /// succeeded the answer is trusted, even if a later refining query failed. An
    /// explicit <c>ReportFailure</c> is a valid terminal and is always resolved.
    /// </summary>
    private bool IsUnresolved(List<ChatMessage> priorHistory, ChatResponse response)
    {
        var all = priorHistory.Concat(response.Messages).ToList();

        if (CalledTool(all, ReportFailureToolName))
            return false;

        if (string.IsNullOrWhiteSpace(response.Text))
            return true;

        var (attempted, anySucceeded) = SummarizeExecuteQueries(all);
        return attempted && !anySucceeded;
    }

    private static bool CalledTool(IEnumerable<ChatMessage> messages, string toolName)
    {
        return messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Any(call => string.Equals(call.Name, toolName, StringComparison.Ordinal));
    }

    /// <summary>
    /// Reports whether the conversation ran any <c>ExecuteQuery</c> and whether at
    /// least one of those queries succeeded.
    /// </summary>
    private static (bool Attempted, bool AnySucceeded) SummarizeExecuteQueries(
        IReadOnlyList<ChatMessage> messages
    )
    {
        var executeCallIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
        {
            if (string.Equals(call.Name, ExecuteQueryToolName, StringComparison.Ordinal))
                executeCallIds.Add(call.CallId);
        }

        if (executeCallIds.Count == 0)
            return (false, false);

        var anySucceeded = messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Where(result => executeCallIds.Contains(result.CallId))
            .Any(result => ParseSuccess(result.Result) == true);

        return (true, anySucceeded);
    }

    private static bool? ParseSuccess(object result)
    {
        if (result is null)
            return null;

        var text = result as string ?? result.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            var token = JToken.Parse(text);
            if (token.Type != JTokenType.Object)
                return null;

            var success = token["Success"];
            return success != null && success.Type == JTokenType.Boolean
                ? success.Value<bool>()
                : (bool?)null;
        }
        catch (JsonReaderException)
        {
            // A non-JSON tool result can't be classified; treat it as inconclusive.
            return null;
        }
    }

    private static ChatMessage BuildReminder(string question)
    {
        var questionLine = string.IsNullOrWhiteSpace(question)
            ? string.Empty
            : $"\nUser's question: \"{question}\"\n";

        var reminder =
            "<system-reminder>\n"
            + "You ended your turn without answering and without reporting a failure.\n"
            + "If your last ExecuteQuery call returned an error, read its ErrorMessage, "
            + "call GetDatabaseSchema again if needed to verify the exact table and column "
            + "names, fix the SQL, and call ExecuteQuery again.\n"
            + "If you have already tried several queries and none work, call ReportFailure "
            + "with a short reason.\n"
            + "Never reply with an empty message."
            + questionLine
            + "</system-reminder>";

        return new ChatMessage(ChatRole.User, reminder);
    }

    private static string ExtractQuestion(IEnumerable<ChatMessage> messages)
    {
        // Skip any reminder this guard injected on a prior (replayed) turn so the
        // restated question is the real user question, not a reminder.
        return messages
            .Where(m => m.Role == ChatRole.User)
            .Select(m => m.Text)
            .LastOrDefault(text => text != null && !text.Contains("<system-reminder>"));
    }
}

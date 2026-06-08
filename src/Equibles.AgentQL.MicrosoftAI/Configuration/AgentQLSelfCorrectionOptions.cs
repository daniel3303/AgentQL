namespace Equibles.AgentQL.MicrosoftAI.Configuration;

/// <summary>
/// Tuning for <see cref="SelfCorrectingChatClient"/> — the guard that keeps the
/// query agent from ending a turn without an answer or an explicit failure.
/// </summary>
public class AgentQLSelfCorrectionOptions
{
    /// <summary>
    /// Maximum number of system-reminder nudges injected before the guard gives
    /// up and returns <see cref="ExhaustionMessage"/>. Each nudge re-runs the
    /// full tool loop, so this bounds the extra model round-trips on top of the
    /// first attempt.
    /// </summary>
    public int MaxAttempts { get; set; } = 4;

    /// <summary>
    /// Returned as the final answer when the agent never produces a usable
    /// result within <see cref="MaxAttempts"/>. English by default; override it
    /// to match the application's language.
    /// </summary>
    public string ExhaustionMessage { get; set; } =
        "I could not construct a valid query to answer this question after several attempts.";
}

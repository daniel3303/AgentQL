namespace Equibles.AgentQL.MicrosoftAI;

/// <summary>
/// Snapshot of the last successful query executed through <see cref="AgentQLPlugin"/>,
/// exposed via <see cref="AgentQLPlugin.LastSuccessfulResult"/> so host applications can
/// hand verified rows to an orchestrator (for charts, structured replies, …) instead of
/// re-parsing the model's prose.
/// </summary>
public class CapturedQueryResult
{
    /// <summary>The SQL statement that produced the rows.</summary>
    public string Sql { get; init; }

    /// <summary>The result rows exactly as returned to the model.</summary>
    public List<Dictionary<string, object>> Data { get; init; }

    /// <summary>Number of rows in <see cref="Data"/>.</summary>
    public int RowCount => Data?.Count ?? 0;

    /// <summary>
    /// The model-authored description of what the rows are (columns, filters, caveats).
    /// Null when no description was supplied with the query.
    /// </summary>
    public string Description { get; init; }
}

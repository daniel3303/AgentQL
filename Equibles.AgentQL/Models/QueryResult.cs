namespace Equibles.AgentQL.Models;

public class QueryResult {
    public bool Success { get; private set; }
    public List<Dictionary<string, object>> Data { get; private set; }
    public string ErrorMessage { get; private set; }
    public string ExecutedSql { get; private set; }
    public int RowCount => Data?.Count ?? 0;

    public static QueryResult FromSuccess(List<Dictionary<string, object>> data, string sql)
        => new() { Success = true, Data = data, ExecutedSql = sql };

    public static QueryResult FromError(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };
}

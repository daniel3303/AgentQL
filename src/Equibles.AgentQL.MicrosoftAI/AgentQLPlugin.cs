using System.ComponentModel;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Newtonsoft.Json;

namespace Equibles.AgentQL.MicrosoftAI;

public class AgentQLPlugin
{
    private readonly ISchemaProvider _schemaProvider;
    private readonly IQueryExecutor _queryExecutor;

    public AgentQLPlugin(ISchemaProvider schemaProvider, IQueryExecutor queryExecutor)
    {
        _schemaProvider = schemaProvider;
        _queryExecutor = queryExecutor;
    }

    /// <summary>
    /// The last successful query executed through this plugin instance (either
    /// <see cref="ExecuteQuery"/> or <see cref="ExecuteQueryWithDescription"/>), or null
    /// when no query has succeeded yet. Failed queries never overwrite a prior capture.
    /// The plugin is registered scoped, so the capture is per resolution scope; it is
    /// not safe for concurrent query execution within a single scope.
    /// </summary>
    public CapturedQueryResult LastSuccessfulResult { get; private set; }

    [Description(
        "Gets the database schema description including all tables, columns, relationships, and enum values. "
            + "Call this first to understand the database structure before writing any SQL queries."
    )]
    public async Task<string> GetDatabaseSchema()
    {
        var schema = await _schemaProvider.GetSchemaDescription();

        return "## Workflow\n"
            + "1. Review the database schema below\n"
            + "2. Construct a SQL SELECT query to answer the user's question\n"
            + "3. Execute the query using the query execution tool\n"
            + "4. If you cannot construct a valid query, use ReportFailure\n\n"
            + "## Query Rules\n"
            + "- Only SELECT queries are allowed\n"
            + "- Do not modify data (no INSERT, UPDATE, DELETE)\n"
            + $"- Maximum {_queryExecutor.MaxRows} rows will be returned\n"
            + "- Use proper JOIN syntax for related tables\n"
            + "- Use table aliases for readability\n\n"
            + "## Database Schema\n"
            + schema;
    }

    [Description(
        "Executes a SQL SELECT query against the database and returns the results as JSON."
    )]
    public async Task<string> ExecuteQuery(
        [Description("The SQL SELECT query to execute")] string sqlQuery
    )
    {
        return await ExecuteAndCapture(sqlQuery, description: null);
    }

    [Description(
        "Executes a SQL SELECT query against the database and returns the results as JSON. "
            + "Alongside the SQL, provide a short description of the result set so the host "
            + "application can present the rows without re-interpreting them."
    )]
    public async Task<string> ExecuteQueryWithDescription(
        [Description("The SQL SELECT query to execute")] string sqlQuery,
        [Description(
            "One or two sentences describing what the result rows are: what each column "
                + "means, the filters and period applied, and any caveat (e.g. partial year, "
                + "credit notes excluded)."
        )]
            string resultDescription
    )
    {
        return await ExecuteAndCapture(sqlQuery, resultDescription);
    }

    [Description(
        "Reports a failure when a valid SQL query cannot be constructed for the user's question."
    )]
    public string ReportFailure(
        [Description("The reason why the query cannot be constructed")] string reason
    )
    {
        return JsonConvert.SerializeObject(new { Success = false, Reason = reason });
    }

    // Shared execution path: runs the query, records the capture on success (a query
    // with zero rows is still a successful answer — "no data matches"), and serializes
    // the result for the model exactly as before.
    private async Task<string> ExecuteAndCapture(string sqlQuery, string description)
    {
        var result = await _queryExecutor.Execute(sqlQuery);

        if (result.Success)
        {
            LastSuccessfulResult = new CapturedQueryResult
            {
                Sql = result.ExecutedSql,
                Data = result.Data,
                Description = description,
            };
        }

        return JsonConvert.SerializeObject(
            new
            {
                result.Success,
                result.RowCount,
                result.Data,
                result.ErrorMessage,
            }
        );
    }
}

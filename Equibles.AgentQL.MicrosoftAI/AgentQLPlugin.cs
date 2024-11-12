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

    [Description(
        "Gets the database schema description including all tables, columns, relationships, and enum values. " +
        "Call this first to understand the database structure before writing any SQL queries.")]
    public async Task<string> GetDatabaseSchema()
    {
        var schema = await _schemaProvider.GetSchemaDescription();

        return "## Workflow\n" +
               "1. Review the database schema below\n" +
               "2. Construct a SQL SELECT query to answer the user's question\n" +
               "3. Execute the query using the ExecuteQuery tool\n" +
               "4. If you cannot construct a valid query, use ReportFailure\n\n" +
               "## Query Rules\n" +
               "- Only SELECT queries are allowed\n" +
               "- Do not modify data (no INSERT, UPDATE, DELETE)\n" +
               $"- Maximum {_queryExecutor.MaxRows} rows will be returned\n" +
               "- Use proper JOIN syntax for related tables\n" +
               "- Use table aliases for readability\n\n" +
               "## Database Schema\n" +
               schema;
    }

    [Description("Executes a SQL SELECT query against the database and returns the results as JSON.")]
    public async Task<string> ExecuteQuery(
        [Description("The SQL SELECT query to execute")] string sqlQuery)
    {
        var result = await _queryExecutor.Execute(sqlQuery);

        return JsonConvert.SerializeObject(new
        {
            result.Success,
            result.RowCount,
            result.Data,
            result.ErrorMessage
        });
    }

    [Description("Reports a failure when a valid SQL query cannot be constructed for the user's question.")]
    public string ReportFailure(
        [Description("The reason why the query cannot be constructed")] string reason)
    {
        return JsonConvert.SerializeObject(new
        {
            Success = false,
            Reason = reason
        });
    }
}

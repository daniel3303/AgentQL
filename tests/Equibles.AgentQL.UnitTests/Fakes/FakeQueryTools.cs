using System.ComponentModel;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;

namespace Equibles.AgentQL.UnitTests.Fakes;

/// <summary>
/// Stand-in AgentQL tools whose names and result shapes match the real
/// <c>AgentQLPlugin</c>, but without a database. <c>ExecuteQuery</c> fails when
/// the SQL contains "bad" and succeeds otherwise, so a script can drive the
/// error-then-recover path deterministically.
/// </summary>
public sealed class FakeQueryTools
{
    [Description("Executes a SQL query")]
    public string ExecuteQuery([Description("The SQL query")] string sqlQuery)
    {
        var failed = sqlQuery != null && sqlQuery.Contains("bad");
        return JsonConvert.SerializeObject(
            failed
                ? new
                {
                    Success = false,
                    RowCount = 0,
                    Data = (object)null,
                    ErrorMessage = "column does not exist",
                }
                : new
                {
                    Success = true,
                    RowCount = 1,
                    Data = new[] { new { c = 3 } },
                    ErrorMessage = (string)null,
                }
        );
    }

    [Description("Executes a SQL query and describes the result rows")]
    public string ExecuteQueryWithDescription(
        [Description("The SQL query")] string sqlQuery,
        [Description("What the rows are")] string resultDescription
    )
    {
        return ExecuteQuery(sqlQuery);
    }

    [Description("Reports a failure")]
    public string ReportFailure([Description("The reason")] string reason)
    {
        return JsonConvert.SerializeObject(new { Success = false, Reason = reason });
    }

    [Description("Gets the schema")]
    public string GetDatabaseSchema() => "schema: Bookings(Destination)";

    public IList<AITool> AsTools() =>
        [
            AIFunctionFactory.Create(ExecuteQuery),
            AIFunctionFactory.Create(ExecuteQueryWithDescription),
            AIFunctionFactory.Create(ReportFailure),
            AIFunctionFactory.Create(GetDatabaseSchema),
        ];
}

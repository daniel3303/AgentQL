using Equibles.AgentQL.Models;

namespace Equibles.AgentQL.EntityFrameworkCore.Query;

public interface IQueryExecutor {
    int MaxRows { get; }
    Task<QueryResult> Execute(string sql);
}

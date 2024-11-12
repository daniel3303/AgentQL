namespace Equibles.AgentQL.EntityFrameworkCore.Schema;

public interface ISchemaProvider {
    Task<string> GetSchemaDescription();
}

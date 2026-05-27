namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

internal static class ReadOnlySessionEnforcerFactory
{
    public static IReadOnlySessionEnforcer Resolve(string providerName)
    {
        var name = (providerName ?? string.Empty).ToLowerInvariant();

        if (name.Contains("npgsql") || name.Contains("postgresql"))
            return PostgreSqlReadOnlySessionEnforcer.Instance;

        if (name.Contains("sqlite"))
            return SqliteReadOnlySessionEnforcer.Instance;

        if (name.Contains("sqlserver"))
            return SqlServerReadOnlySessionEnforcer.Instance;

        return NullReadOnlySessionEnforcer.Instance;
    }
}

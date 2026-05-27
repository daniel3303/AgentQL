using System.Data.Common;

namespace Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;

internal sealed class NullReadOnlySessionEnforcer : IReadOnlySessionEnforcer
{
    public static readonly NullReadOnlySessionEnforcer Instance = new();

    private NullReadOnlySessionEnforcer() { }

    public Task Apply(DbConnection connection) => Task.CompletedTask;

    public Task Reset(DbConnection connection) => Task.CompletedTask;

    public bool IsReadOnlyViolation(Exception ex) => false;
}

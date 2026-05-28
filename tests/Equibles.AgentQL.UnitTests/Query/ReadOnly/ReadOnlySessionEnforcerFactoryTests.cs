using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

/// <summary>
/// <c>ReadOnlySessionEnforcerFactory.Resolve</c> maps an EF Core provider name
/// to the matching session enforcer. The caller passes
/// <c>DbContext.Database.ProviderName</c>, which is nullable, so a null or
/// unrecognised provider has no known DBMS-level read-only mechanism and must
/// fall back to the no-op <c>NullReadOnlySessionEnforcer</c> — never throw.
/// </summary>
public class ReadOnlySessionEnforcerFactoryTests
{
    [Fact]
    public void Resolve_NullProviderName_ReturnsNullEnforcer()
    {
        // A null provider name is a real input (ProviderName is nullable); the
        // contract is a safe no-op fallback, not a NullReferenceException.
        var enforcer = ReadOnlySessionEnforcerFactory.Resolve(null!);

        enforcer.Should().BeSameAs(NullReadOnlySessionEnforcer.Instance);
    }
}

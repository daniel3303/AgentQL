using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Configuration;

public class EntityConfigurationValueTypePropertyTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
    }

    // Contract: Property<T>(expr) resolves the member name from a
    // property-access lambda. For a value-type property the compiler inserts a
    // boxing Convert node (s => (object)s.Id); resolution must see through it.
    // Existing tests only cover the reference-type (no-Convert) path.
    [Fact]
    public void Property_WithValueTypeExpression_ResolvesMemberNameThroughBoxingConvert()
    {
        var entity = new AgentQLOptions().Entity<Sample>();

        var byExpression = entity.Property<Sample>(s => s.Id);
        var byName = entity.Property("Id");

        byExpression.Should().BeSameAs(byName);
    }
}

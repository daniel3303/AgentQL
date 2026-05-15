using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Configuration;

public class AgentQLOptionsTests
{
    private class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [Fact]
    public void Defaults_AreReadOnlyIncludeAll()
    {
        var options = new AgentQLOptions();

        options.DefaultBehavior.Should().Be(IncludeBehavior.IncludeAll);
        options.MaxRows.Should().Be(25);
        options.CommandTimeout.Should().Be(15);
        options.ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Entity_CalledTwiceForSameType_ReturnsSameConfiguration()
    {
        var options = new AgentQLOptions();

        var first = options.Entity<Sample>();
        var second = options.Entity<Sample>();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Entity_WithConfigureAction_InvokesActionAndReturnsConfiguration()
    {
        var options = new AgentQLOptions();

        var config = options.Entity<Sample>(c => c.Description = "A sample entity");

        config.Description.Should().Be("A sample entity");
        options.Entity<Sample>().Description.Should().Be("A sample entity");
    }

    [Fact]
    public void Property_SameNameTwice_ReturnsSameConfiguration()
    {
        var entity = new AgentQLOptions().Entity<Sample>();

        var first = entity.Property("Name");
        var second = entity.Property("Name");

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Property_WithExpression_ResolvesMemberName()
    {
        var entity = new AgentQLOptions().Entity<Sample>();

        var byExpression = entity.Property<Sample>(s => s.Name);
        var byName = entity.Property("Name");

        byExpression.Should().BeSameAs(byName);
    }

    [Fact]
    public void Property_WithNonMemberExpression_Throws()
    {
        var entity = new AgentQLOptions().Entity<Sample>();

        var act = () => entity.Property<Sample>(s => s.Id + 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Include_ReturnsSameInstance_ForChaining()
    {
        var entity = new AgentQLOptions().Entity<Sample>();

        entity.Include().Should().BeSameAs(entity);
        entity.Property("Name").Exclude().Should().BeSameAs(entity.Property("Name"));
    }
}

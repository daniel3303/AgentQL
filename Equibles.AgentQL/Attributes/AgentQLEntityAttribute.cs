using Equibles.AgentQL.Configuration;

namespace Equibles.AgentQL.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class AgentQLEntityAttribute : Attribute {
    public string Description { get; set; }
    public IncludeBehavior PropertyDefault { get; set; } = IncludeBehavior.IncludeAll;
}

namespace Equibles.AgentQL.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public class AgentQLPropertyAttribute : Attribute {
    public string Description { get; set; }
}

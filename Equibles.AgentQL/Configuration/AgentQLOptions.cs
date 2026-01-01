using System.Linq.Expressions;

namespace Equibles.AgentQL.Configuration;

public class AgentQLOptions {
    public IncludeBehavior DefaultBehavior { get; set; } = IncludeBehavior.IncludeAll;
    public int MaxRows { get; set; } = 25;
    public int CommandTimeout { get; set; } = 15;
    public bool ReadOnly { get; set; } = true;

    internal Dictionary<Type, EntityConfiguration> EntityConfigurations { get; } = new();

    public EntityConfiguration Entity<T>() where T : class {
        return Entity(typeof(T));
    }

    public EntityConfiguration Entity<T>(Action<EntityConfiguration> configure) where T : class {
        var config = Entity(typeof(T));
        configure(config);
        return config;
    }

    internal EntityConfiguration Entity(Type type) {
        if (!EntityConfigurations.TryGetValue(type, out var config)) {
            config = new EntityConfiguration(type);
            EntityConfigurations[type] = config;
        }

        return config;
    }
}

public class EntityConfiguration {
    internal Type ClrType { get; }
    public string Description { get; set; }
    public IncludeBehavior? PropertyDefault { get; set; }
    internal bool? Included { get; private set; }
    internal Dictionary<string, PropertyConfiguration> PropertyConfigurations { get; } = new();

    internal EntityConfiguration(Type clrType) {
        ClrType = clrType;
    }

    public EntityConfiguration Include() {
        Included = true;
        return this;
    }

    public EntityConfiguration Exclude() {
        Included = false;
        return this;
    }

    public PropertyConfiguration Property<T>(Expression<Func<T, object>> propertyExpression) {
        var memberName = GetMemberName(propertyExpression);
        return Property(memberName);
    }

    public PropertyConfiguration Property(string propertyName) {
        if (!PropertyConfigurations.TryGetValue(propertyName, out var config)) {
            config = new PropertyConfiguration(propertyName);
            PropertyConfigurations[propertyName] = config;
        }

        return config;
    }

    private static string GetMemberName<T>(Expression<Func<T, object>> expression) {
        var body = expression.Body;

        if (body is UnaryExpression unary)
            body = unary.Operand;

        if (body is MemberExpression member)
            return member.Member.Name;

        throw new ArgumentException("Expression must be a property access expression.");
    }
}

public class PropertyConfiguration {
    internal string PropertyName { get; }
    public string Description { get; set; }
    internal bool? Included { get; private set; }

    internal PropertyConfiguration(string propertyName) {
        PropertyName = propertyName;
    }

    public PropertyConfiguration Include() {
        Included = true;
        return this;
    }

    public PropertyConfiguration Exclude() {
        Included = false;
        return this;
    }
}

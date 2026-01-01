using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Equibles.AgentQL.EntityFrameworkCore.Extensions;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddAgentQL<TContext>(this IServiceCollection services, Action<AgentQLOptions> configure = null) where TContext : DbContext {
        var options = new AgentQLOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<ISchemaProvider, SchemaProvider<TContext>>();
        services.AddScoped<IQueryExecutor, QueryExecutor<TContext>>();

        return services;
    }
}

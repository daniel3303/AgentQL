using Equibles.AgentQL.Demo.Components;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Equibles.AgentQL.Demo;

// Split into ConfigureServices / SeedDatabase / ConfigurePipeline so a
// functional-test host can build the exact same app on real Kestrel and swap
// only the IChatClient. Behaviour is identical to the previous top-level
// program — the same calls in the same order.
public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureServices(builder);

        var app = builder.Build();
        SeedDatabase(app);
        ConfigurePipeline(app);

        app.Run();
    }

    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        var connectionString =
            builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "DataSource=travel.db";

        builder.Services.AddDbContext<TravelDbContext>(options =>
            options.UseSqlite(connectionString)
        );

        var agentQLSection = builder.Configuration.GetSection("AgentQL");

        builder.Services.AddAgentQLChat<TravelDbContext>(configureChat: options =>
        {
            if (Enum.TryParse<AiProvider>(agentQLSection["Provider"], out var provider))
                options.Provider = provider;

            options.ApiKey = agentQLSection["ApiKey"];
            options.Endpoint = agentQLSection["Endpoint"];
            options.ModelName = agentQLSection["ModelName"];
        });
    }

    public static void SeedDatabase(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TravelDbContext>();
        db.Database.EnsureCreated();
        DataSeeder.Seed(db);
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.UseStaticFiles();

        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
    }
}

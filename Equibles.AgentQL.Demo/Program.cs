using Equibles.AgentQL.Demo;
using Equibles.AgentQL.Demo.Components;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "DataSource=travel.db";

builder.Services.AddDbContext<TravelDbContext>(options =>
    options.UseSqlite(connectionString));

var agentQLSection = builder.Configuration.GetSection("AgentQL");

builder.Services.AddAgentQLChat<TravelDbContext>(configureChat: options =>
{
    if (Enum.TryParse<AiProvider>(agentQLSection["Provider"], out var provider))
        options.Provider = provider;

    options.ApiKey = agentQLSection["ApiKey"];
    options.Endpoint = agentQLSection["Endpoint"];
    options.ModelName = agentQLSection["ModelName"];
});

var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TravelDbContext>();
    db.Database.EnsureCreated();
    DataSeeder.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

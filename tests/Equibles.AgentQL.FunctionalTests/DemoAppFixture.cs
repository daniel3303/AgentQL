using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Equibles.AgentQL.FunctionalTests;

/// <summary>
/// Hosts the real Demo Blazor app on Kestrel at a random port, against a fresh
/// temp SQLite database, with only the IChatClient swapped for a deterministic
/// fake. Everything else — DI wiring, AgentQL plugin, query execution, the
/// FunctionInvokingChatClient pipeline, Razor components — is the production
/// app. Playwright drives a real browser against <see cref="BaseUrl"/>.
/// </summary>
public sealed class DemoAppFixture : IAsyncLifetime
{
    /// <summary>The SQL the fake LLM is scripted to request via ExecuteQuery.</summary>
    public const string ScriptedSql = "SELECT COUNT(*) AS c FROM Customers";

    private WebApplication? _app;
    private string? _dbPath;

    public string BaseUrl { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"agentql-func-{Guid.NewGuid():N}.db");

        // ContentRoot must point at the Demo's bin output so its Razor-component
        // static assets resolve when hosted from the test assembly.
        var demoContentRoot = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "Equibles.AgentQL.Demo",
                "bin",
                GetConfiguration(),
                "net10.0"
            )
        );

        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                ContentRootPath = demoContentRoot,
                ApplicationName = "Equibles.AgentQL.Demo",
                EnvironmentName = Environments.Development,
            }
        );

        builder.Configuration["ConnectionStrings:DefaultConnection"] = $"DataSource={_dbPath}";

        Equibles.AgentQL.Demo.Program.ConfigureServices(builder);

        // Swap only the LLM. Keep the production FunctionInvocation pipeline so
        // the scripted tool call really reaches AgentQLPlugin → QueryExecutor.
        builder.Services.RemoveAll<IChatClient>();
        builder.Services.AddSingleton<IChatClient>(
            new ChatClientBuilder(new FakeChatClient(ScriptedSql)).UseFunctionInvocation().Build()
        );

        _app = builder.Build();
        Equibles.AgentQL.Demo.Program.SeedDatabase(_app);
        Equibles.AgentQL.Demo.Program.ConfigurePipeline(_app);

        _app.Urls.Add("http://127.0.0.1:0");
        await _app.StartAsync();
        BaseUrl = _app.Urls.First();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        if (_dbPath is not null && File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup; a held SQLite handle is not a test failure.
            }
        }
    }

    private static string GetConfiguration() =>
        AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}"
        )
            ? "Release"
            : "Debug";
}

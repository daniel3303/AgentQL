using Microsoft.Playwright;
using Xunit;

namespace Equibles.AgentQL.FunctionalTests;

/// <summary>
/// Owns the Demo host plus a headless Chromium. Installs the browser on first
/// use (idempotent — exits 0 if already present) so the suite is self-contained
/// without a separate pwsh step.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private readonly DemoAppFixture _demo = new();
    private IPlaywright _playwright = default!;
    private IBrowser _browser = default!;

    public string BaseUrl => _demo.BaseUrl;

    public async ValueTask InitializeAsync()
    {
        await _demo.InitializeAsync();

        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Playwright Chromium install failed with exit code {exitCode}."
            );
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await _browser.NewContextAsync(new() { BaseURL = BaseUrl });
        return await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
        await _demo.DisposeAsync();
    }
}

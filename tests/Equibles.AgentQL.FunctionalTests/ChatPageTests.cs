using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Equibles.AgentQL.FunctionalTests;

/// <summary>
/// True end-to-end: a real Chromium drives the Demo's Blazor chat UI. The
/// scripted LLM asks AgentQL to run SQL; the production
/// FunctionInvokingChatClient → AgentQLPlugin → QueryExecutor path executes it
/// against the seeded SQLite database and the answer is rendered back in the
/// browser. Only the LLM is faked.
/// </summary>
public class ChatPageTests : IClassFixture<PlaywrightFixture>
{
    private readonly PlaywrightFixture _fx;

    public ChatPageTests(PlaywrightFixture fx) => _fx = fx;

    [Fact]
    public async Task ChatPage_AskQuestion_RoundTripsThroughAgentQLAndRendersAnswer()
    {
        var page = await _fx.NewPageAsync();
        await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle });

        // prerender:false — the chat input only exists once the Blazor circuit
        // has connected and rendered interactively.
        var input = page.Locator("textarea");
        await input.WaitForAsync(new() { Timeout = 30_000 });

        await input.FillAsync("How many customers are there?");
        await page.Locator("button.send-button").ClickAsync();

        // The ExecuteQuery tool call surfaced in the UI — proves the scripted
        // call traversed the real function-invocation pipeline into the plugin.
        var phrase = page.Locator(".assistant-search-phrase");
        await Assertions
            .Expect(phrase)
            .ToContainTextAsync(DemoAppFixture.ScriptedSql, new() { Timeout = 30_000 });

        // Final answer is built from the real query result fed back to the
        // model: a successful QueryResult JSON for the seeded Customers table.
        var answer = page.Locator(".assistant-message-text assistant-message");
        await Assertions
            .Expect(answer)
            .ToHaveAttributeAsync(
                "markdown",
                new Regex("\"Success\"\\s*:\\s*true"),
                new() { Timeout = 30_000 }
            );
    }
}

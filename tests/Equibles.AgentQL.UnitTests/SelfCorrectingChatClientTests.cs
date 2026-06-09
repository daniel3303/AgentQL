using AwesomeAssertions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Equibles.AgentQL.UnitTests.Fakes;
using Microsoft.Extensions.AI;
using Xunit;

namespace Equibles.AgentQL.UnitTests;

/// <summary>
/// Drives <see cref="Equibles.AgentQL.MicrosoftAI.SelfCorrectingChatClient"/>
/// through the real Microsoft.Extensions.AI function-invocation pipeline with a
/// scripted model and fake AgentQL tools, proving the guard re-prompts on an
/// unresolved turn and never lets the agent return an empty answer.
/// </summary>
public class SelfCorrectingChatClientTests
{
    private const string Question = "How many bookings go to Lisbon?";

    private static IChatClient BuildPipeline(
        ScriptedChatClient scripted,
        AgentQLSelfCorrectionOptions options
    )
    {
        // Self-correction added before function invocation so it wraps the loop.
        return new ChatClientBuilder(scripted)
            .UseAgentQLSelfCorrection(options)
            .UseFunctionInvocation()
            .Build();
    }

    private static Task<ChatResponse> Run(IChatClient pipeline, IList<AITool> tools)
    {
        return pipeline.GetResponseAsync(
            [new ChatMessage(ChatRole.User, Question)],
            new ChatOptions { Tools = tools },
            TestContext.Current.CancellationToken
        );
    }

    private static IDictionary<string, object> Sql(string value) =>
        new Dictionary<string, object> { ["sqlQuery"] = value };

    [Fact]
    public async Task GetResponse_EmptyAnswerAfterFailedQuery_InjectsReminderAndRecovers()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.ToolCall("ExecuteQuery", "c1", Sql("SELECT bad")),
            ScriptedChatClient.Say(string.Empty),
            ScriptedChatClient.ToolCall("ExecuteQuery", "c2", Sql("SELECT good")),
            ScriptedChatClient.Say("There are 3 bookings to Lisbon."),
        ]);

        var response = await Run(
            BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()),
            tools.AsTools()
        );

        response.Text.Should().Be("There are 3 bookings to Lisbon.");
        scripted.Reminders.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetResponse_NonEmptyGiveUpAfterFailedQuery_StillNudgesToRetry()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.ToolCall("ExecuteQuery", "c1", Sql("SELECT bad")),
            ScriptedChatClient.Say("I couldn't find that information."),
            ScriptedChatClient.ToolCall("ExecuteQuery", "c2", Sql("SELECT good")),
            ScriptedChatClient.Say("Found 3 bookings."),
        ]);

        var response = await Run(
            BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()),
            tools.AsTools()
        );

        response.Text.Should().Be("Found 3 bookings.");
        scripted.Reminders.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetResponse_EmptyAnswerWithoutQuery_NudgesUntilAnswered()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.Say(string.Empty),
            ScriptedChatClient.Say("A real answer."),
        ]);

        var response = await Run(
            BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()),
            tools.AsTools()
        );

        response.Text.Should().Be("A real answer.");
        scripted.Reminders.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetResponse_AnswersOnFirstTry_DoesNotInjectReminder()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.ToolCall("ExecuteQuery", "c1", Sql("SELECT good")),
            ScriptedChatClient.Say("There are 3 bookings."),
        ]);

        var response = await Run(
            BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()),
            tools.AsTools()
        );

        response.Text.Should().Be("There are 3 bookings.");
        scripted.Reminders.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResponse_DescribedQuerySucceeds_CountsAsGroundedAnswer()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.ToolCall(
                "ExecuteQueryWithDescription",
                "c1",
                new Dictionary<string, object>
                {
                    ["sqlQuery"] = "SELECT good",
                    ["resultDescription"] = "Booking counts per destination",
                }
            ),
            ScriptedChatClient.Say("DONE"),
        ]);

        var response = await Run(
            BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()),
            tools.AsTools()
        );

        response.Text.Should().Be("DONE");
        scripted.Reminders.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResponse_DescribedQueryFails_StillNudgesToRetry()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.ToolCall(
                "ExecuteQueryWithDescription",
                "c1",
                new Dictionary<string, object>
                {
                    ["sqlQuery"] = "SELECT bad",
                    ["resultDescription"] = "Booking counts",
                }
            ),
            ScriptedChatClient.Say("I give up."),
            ScriptedChatClient.ToolCall(
                "ExecuteQueryWithDescription",
                "c2",
                new Dictionary<string, object>
                {
                    ["sqlQuery"] = "SELECT good",
                    ["resultDescription"] = "Booking counts",
                }
            ),
            ScriptedChatClient.Say("DONE"),
        ]);

        var response = await Run(
            BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()),
            tools.AsTools()
        );

        response.Text.Should().Be("DONE");
        scripted.Reminders.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetResponse_AnswerGroundedInEarlierSuccess_NotNudgedByLaterFailedQuery()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.ToolCall("ExecuteQuery", "c1", Sql("SELECT good")),
            ScriptedChatClient.ToolCall("ExecuteQuery", "c2", Sql("SELECT bad")),
            ScriptedChatClient.Say("Based on the first query, there are 3 bookings."),
        ]);

        var response = await Run(
            BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()),
            tools.AsTools()
        );

        response.Text.Should().Be("Based on the first query, there are 3 bookings.");
        scripted.Reminders.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResponse_ModelReportsFailure_TreatedAsResolved()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.ToolCall("ExecuteQuery", "c1", Sql("SELECT bad")),
            ScriptedChatClient.ToolCall(
                "ReportFailure",
                "c2",
                new Dictionary<string, object> { ["reason"] = "no such data" }
            ),
            ScriptedChatClient.Say("Sorry, that data is not available."),
        ]);

        var response = await Run(
            BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()),
            tools.AsTools()
        );

        response.Text.Should().Be("Sorry, that data is not available.");
        scripted.Reminders.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResponse_NeverRecovers_ReturnsExhaustionMessageAfterMaxAttempts()
    {
        var options = new AgentQLSelfCorrectionOptions
        {
            MaxAttempts = 2,
            ExhaustionMessage = "Could not answer.",
        };
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.ToolCall("ExecuteQuery", "c1", Sql("SELECT bad")),
            ScriptedChatClient.Say(string.Empty),
            ScriptedChatClient.Say(string.Empty),
            ScriptedChatClient.Say(string.Empty),
        ]);

        var response = await Run(BuildPipeline(scripted, options), tools.AsTools());

        response.Text.Should().Be("Could not answer.");
        scripted.Reminders.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetResponse_InjectedReminder_IsWrappedAndCarriesTheQuestion()
    {
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.Say(string.Empty),
            ScriptedChatClient.Say("Answer."),
        ]);

        await Run(BuildPipeline(scripted, new AgentQLSelfCorrectionOptions()), tools.AsTools());

        var reminder = scripted.Reminders.Single();
        reminder.Should().StartWith("<system-reminder>");
        reminder.Should().Contain(Question);
        reminder.Should().Contain("ReportFailure");
    }
}

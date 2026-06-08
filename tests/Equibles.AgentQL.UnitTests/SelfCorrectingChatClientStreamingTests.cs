using System.Text;
using AwesomeAssertions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Equibles.AgentQL.UnitTests.Fakes;
using Microsoft.Extensions.AI;
using Xunit;

namespace Equibles.AgentQL.UnitTests;

public class SelfCorrectingChatClientStreamingTests
{
    private const string Question = "How many bookings go to Lisbon?";

    [Fact]
    public async Task GetStreamingResponse_EmptyAnswerThenRecovery_StreamsCorrectedAnswer()
    {
        // Contract: the streaming override must run the same guarded loop as
        // GetResponseAsync — an unresolved (empty) turn is corrected before any
        // updates surface, so the streamed text is the recovered answer and
        // never the empty give-up.
        var tools = new FakeQueryTools();
        var scripted = new ScriptedChatClient([
            ScriptedChatClient.Say(string.Empty),
            ScriptedChatClient.Say("A real answer."),
        ]);
        var pipeline = new ChatClientBuilder(scripted)
            .UseAgentQLSelfCorrection(new AgentQLSelfCorrectionOptions())
            .UseFunctionInvocation()
            .Build();

        var streamed = new StringBuilder();
        await foreach (
            var update in pipeline.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, Question)],
                new ChatOptions { Tools = tools.AsTools() },
                TestContext.Current.CancellationToken
            )
        )
        {
            streamed.Append(update.Text);
        }

        streamed.ToString().Should().Be("A real answer.");
        scripted.Reminders.Should().HaveCount(1);
    }
}

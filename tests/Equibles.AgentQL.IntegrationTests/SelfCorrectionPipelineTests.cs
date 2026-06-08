using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.Fakes;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Equibles.AgentQL.MicrosoftAI;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Equibles.AgentQL.MicrosoftAI.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// End-to-end proof of the self-correction guard against a real PostgreSQL
/// database: the model first runs SQL referencing a non-existent column (a real
/// <c>42703</c> error from <see cref="QueryExecutor{TContext}"/>) and gives up
/// with an empty answer; the guard injects a reminder and the model recovers
/// with a valid query. This is the exact production failure the guard fixes.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SelfCorrectionPipelineTests : IntegrationTestBase
{
    public SelfCorrectionPipelineTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Pipeline_ModelHallucinatesColumnThenGivesUp_GuardRecoversWithValidQuery()
    {
        await using var context = Fixture.CreateContext();
        var plugin = new AgentQLPlugin(
            new SchemaProvider<TravelTestDbContext>(context, new AgentQLOptions()),
            new QueryExecutor<TravelTestDbContext>(
                context,
                new AgentQLOptions(),
                NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
            )
        );

        var scripted = new ScriptedChatClient([
            // Hallucinated column -> real PostgreSQL "column does not exist".
            ScriptedChatClient.ExecuteQueryCall(
                "c1",
                "SELECT \"Country\" FROM \"Bookings\" WHERE \"Country\" = 'Lisbon'"
            ),
            ScriptedChatClient.Say(string.Empty),
            // After the reminder, a valid query against the real schema.
            ScriptedChatClient.ExecuteQueryCall(
                "c2",
                "SELECT COUNT(*) AS c FROM \"Bookings\" WHERE \"Destination\" = 'Lisbon'"
            ),
            ScriptedChatClient.Say("There are 2 bookings to Lisbon."),
        ]);

        IChatClient pipeline = new ChatClientBuilder(scripted)
            .UseAgentQLSelfCorrection(new AgentQLSelfCorrectionOptions())
            .UseFunctionInvocation()
            .Build();

        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(plugin.ExecuteQuery),
                AIFunctionFactory.Create(plugin.GetDatabaseSchema),
                AIFunctionFactory.Create(plugin.ReportFailure),
            ],
        };

        var response = await pipeline.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "How many bookings go to Lisbon?")],
            options,
            TestContext.Current.CancellationToken
        );

        // The guard refused the empty give-up, re-prompted, and the recovered
        // query produced the grounded answer — never an empty response.
        response.Text.Should().Be("There are 2 bookings to Lisbon.");
        scripted.CallCount.Should().Be(4);
    }
}

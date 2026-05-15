using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.IntegrationTests.Fakes;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Equibles.AgentQL.MicrosoftAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Drives the AgentQL plugin through the real Microsoft.Extensions.AI
/// function-invocation pipeline, substituting only the LLM with
/// <see cref="FakeChatClient"/>. Proves the full loop: model emits a tool
/// call → middleware invokes <see cref="AgentQLPlugin.ExecuteQuery"/> →
/// <see cref="QueryExecutor{TContext}"/> runs real SQL on PostgreSQL →
/// result is fed back to the model.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class FakeLlmPipelineTests
{
    private readonly PostgresFixture _fixture;

    public FakeLlmPipelineTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Pipeline_ModelRequestsExecuteQuery_RunsSqlAndFeedsResultBack()
    {
        await using var context = _fixture.CreateContext();
        var plugin = new AgentQLPlugin(
            new SchemaProvider<TravelTestDbContext>(context, new AgentQLOptions()),
            new QueryExecutor<TravelTestDbContext>(
                context,
                new AgentQLOptions(),
                NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
            )
        );

        var fake = new FakeChatClient(
            "SELECT COUNT(*) AS c FROM \"Bookings\" WHERE \"Destination\" = 'Lisbon'"
        );

        IChatClient pipeline = new ChatClientBuilder(fake).UseFunctionInvocation().Build();

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

        // The middleware actually invoked the tool and looped back to the model.
        fake.CallCount.Should().Be(2);
        fake.CapturedToolResult.Should().NotBeNull();

        // Assert on the parsed tool payload, not its serialized text shape.
        var toolPayload = JObject.Parse(fake.CapturedToolResult!);
        toolPayload["Success"]!.Value<bool>().Should().BeTrue();
        toolPayload["RowCount"]!.Value<int>().Should().Be(1);
        toolPayload["Data"]![0]!["c"]!.Value<int>().Should().Be(2);

        // The fed-back result reached the model and shaped its final answer.
        response.Text.Should().StartWith("Result:");
        response.Text.Should().Contain(fake.CapturedToolResult!);
    }
}

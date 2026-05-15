using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.EntityFrameworkCore.Schema;
using Equibles.AgentQL.MicrosoftAI;
using Equibles.AgentQL.Models;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Xunit;

namespace Equibles.AgentQL.UnitTests;

public class AgentQLPluginTests
{
    private readonly ISchemaProvider _schemaProvider = Substitute.For<ISchemaProvider>();
    private readonly IQueryExecutor _queryExecutor = Substitute.For<IQueryExecutor>();
    private readonly AgentQLPlugin _sut;

    public AgentQLPluginTests() => _sut = new AgentQLPlugin(_schemaProvider, _queryExecutor);

    [Fact]
    public async Task GetDatabaseSchema_PrependsWorkflowAndMaxRows_AroundSchemaText()
    {
        _queryExecutor.MaxRows.Returns(42);
        _schemaProvider.GetSchemaDescription().Returns("=== DATABASE SCHEMA ===");

        var result = await _sut.GetDatabaseSchema();

        result.Should().Contain("## Workflow");
        result.Should().Contain("Maximum 42 rows will be returned");
        result.Should().Contain("=== DATABASE SCHEMA ===");
        await _schemaProvider.Received(1).GetSchemaDescription();
    }

    [Fact]
    public async Task ExecuteQuery_SuccessfulResult_SerializesRowsAsJson()
    {
        var data = new List<Dictionary<string, object>> { new() { ["Name"] = "Paris" } };
        _queryExecutor
            .Execute("SELECT Name FROM Destinations")
            .Returns(QueryResult.FromSuccess(data, "SELECT Name FROM Destinations"));

        var json = await _sut.ExecuteQuery("SELECT Name FROM Destinations");

        var parsed = JObject.Parse(json);
        parsed["Success"]!.Value<bool>().Should().BeTrue();
        parsed["RowCount"]!.Value<int>().Should().Be(1);
        parsed["Data"]![0]!["Name"]!.Value<string>().Should().Be("Paris");
    }

    [Fact]
    public async Task ExecuteQuery_FailedResult_SerializesErrorMessage()
    {
        _queryExecutor.Execute(Arg.Any<string>()).Returns(QueryResult.FromError("syntax error"));

        var json = await _sut.ExecuteQuery("SELECT bad");

        var parsed = JObject.Parse(json);
        parsed["Success"]!.Value<bool>().Should().BeFalse();
        parsed["ErrorMessage"]!.Value<string>().Should().Be("syntax error");
    }

    [Fact]
    public void ReportFailure_ReturnsStructuredReason()
    {
        var json = _sut.ReportFailure("no matching table");

        var parsed = JObject.Parse(json);
        parsed["Success"]!.Value<bool>().Should().BeFalse();
        parsed["Reason"]!.Value<string>().Should().Be("no matching table");
    }
}

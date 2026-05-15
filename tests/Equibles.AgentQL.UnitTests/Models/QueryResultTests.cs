using AwesomeAssertions;
using Equibles.AgentQL.Models;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Models;

public class QueryResultTests
{
    [Fact]
    public void FromSuccess_WithRows_SetsSuccessAndRowCount()
    {
        var data = new List<Dictionary<string, object>>
        {
            new() { ["Id"] = 1 },
            new() { ["Id"] = 2 },
        };

        var result = QueryResult.FromSuccess(data, "SELECT * FROM X");

        result.Success.Should().BeTrue();
        result.Data.Should().BeSameAs(data);
        result.RowCount.Should().Be(2);
        result.ExecutedSql.Should().Be("SELECT * FROM X");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void FromSuccess_WithEmptyData_RowCountIsZero()
    {
        var result = QueryResult.FromSuccess([], "SELECT 1");

        result.Success.Should().BeTrue();
        result.RowCount.Should().Be(0);
    }

    [Fact]
    public void FromError_SetsFailureAndMessage()
    {
        var result = QueryResult.FromError("boom");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("boom");
        result.Data.Should().BeNull();
        result.RowCount.Should().Be(0);
    }
}

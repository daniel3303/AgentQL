using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

public class ReadOnlyStatementValidatorExplainAnalyzeTests
{
    // Contract: only SELECT / VALUES / TABLE / WITH are allowed. In PostgreSQL
    // `EXPLAIN ANALYZE` actually executes its target statement, so
    // `EXPLAIN ANALYZE INSERT ...` is a real write that must be rejected, not
    // silently accepted as a read.
    [Fact]
    public void Validate_ExplainAnalyzeInsert_IsRejected()
    {
        var violation = ReadOnlyStatementValidator.Validate(
            "EXPLAIN ANALYZE INSERT INTO Bookings (Destination) VALUES ('x')"
        );

        violation.Should().NotBeNull();
    }
}

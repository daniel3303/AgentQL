using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

/// <summary>
/// A PostgreSQL positional parameter (<c>$1</c>) starts with <c>$</c> but is
/// not a dollar-quote opener — the character after <c>$</c> is a digit, not a
/// tag identifier. The statement splitter must recognise this and keep
/// scanning, so the following <c>; DROP TABLE</c> is parsed as its own
/// statement and rejected. If <c>$1</c> were mistaken for the start of a
/// dollar-quoted literal, the splitter would search for a closing delimiter
/// that never comes, swallow the rest of the batch as opaque "string data",
/// and the forbidden write would slip past the whitelist.
/// </summary>
public class ReadOnlyStatementValidatorPositionalParameterTests
{
    [Fact]
    public void Validate_PositionalParameterBeforeStackedDrop_RejectsTheDrop()
    {
        var violation = ReadOnlyStatementValidator.Validate(
            "SELECT * FROM foo WHERE id = $1; DROP TABLE foo"
        );

        violation.Should().NotBeNull();
        violation.Should().Contain("DROP");
    }
}

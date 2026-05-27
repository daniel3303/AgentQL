using AwesomeAssertions;
using Equibles.AgentQL.EntityFrameworkCore.Query.ReadOnly;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Query.ReadOnly;

/// <summary>
/// Direct unit tests for the read-only statement whitelist. The integration
/// suite covers the headline cases (every forbidden category, simple CTEs);
/// these tests cover the parser internals the integration suite cannot
/// reach without a live database: empty / whitespace input, malformed
/// WITH, dollar-quoted PG strings, nested parens, ';' inside string
/// literals, multi-statement batches with one forbidden, and
/// case-insensitive matching.
/// </summary>
public class ReadOnlyStatementValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public void Validate_EmptyOrWhitespace_ReturnsNull(string sql)
    {
        ReadOnlyStatementValidator.Validate(sql).Should().BeNull();
    }

    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("select 1")]
    [InlineData("SeLeCt 1")]
    [InlineData("VALUES (1)")]
    [InlineData("TABLE foo")]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x")]
    [InlineData("WITH RECURSIVE x AS (SELECT 1) SELECT * FROM x")]
    [InlineData("WITH x AS MATERIALIZED (SELECT 1) SELECT * FROM x")]
    [InlineData("WITH x AS NOT MATERIALIZED (SELECT 1) SELECT * FROM x")]
    [InlineData("WITH a AS (SELECT 1), b AS (SELECT 2) SELECT * FROM a, b")]
    [InlineData("WITH a AS (WITH b AS (SELECT 1) SELECT * FROM b) SELECT * FROM a")]
    public void Validate_AllowedShapes_ReturnsNull(string sql)
    {
        ReadOnlyStatementValidator.Validate(sql).Should().BeNull();
    }

    [Theory]
    [InlineData("INSERT INTO foo VALUES (1)", "INSERT")]
    [InlineData("UPDATE foo SET a = 1", "UPDATE")]
    [InlineData("DELETE FROM foo", "DELETE")]
    [InlineData("MERGE INTO foo USING bar ON true WHEN MATCHED THEN DELETE", "MERGE")]
    [InlineData("TRUNCATE foo", "TRUNCATE")]
    [InlineData("DROP TABLE foo", "DROP")]
    [InlineData("CREATE TABLE foo (id int)", "CREATE")]
    [InlineData("ALTER TABLE foo ADD COLUMN x int", "ALTER")]
    [InlineData("GRANT SELECT ON foo TO public", "GRANT")]
    [InlineData("REVOKE SELECT ON foo FROM public", "REVOKE")]
    [InlineData("COMMIT", "COMMIT")]
    [InlineData("ROLLBACK", "ROLLBACK")]
    [InlineData("BEGIN", "BEGIN")]
    [InlineData("START TRANSACTION", "START")]
    [InlineData("SAVEPOINT s1", "SAVEPOINT")]
    [InlineData("RELEASE SAVEPOINT s1", "RELEASE")]
    [InlineData("SET x = 1", "SET")]
    [InlineData("RESET ALL", "RESET")]
    [InlineData("DO $$ BEGIN END $$", "DO")]
    [InlineData("CALL my_proc()", "CALL")]
    [InlineData("VACUUM foo", "VACUUM")]
    [InlineData("ANALYZE foo", "ANALYZE")]
    public void Validate_ForbiddenVerb_ReturnsViolationNamingTheVerb(string sql, string verb)
    {
        var violation = ReadOnlyStatementValidator.Validate(sql);
        violation.Should().NotBeNull();
        violation.Should().Contain(verb);
        violation.Should().Contain("ReadOnly");
    }

    [Theory]
    [InlineData("WITH x AS (INSERT INTO foo VALUES (1) RETURNING *) SELECT * FROM x", "INSERT")]
    [InlineData("WITH x AS (UPDATE foo SET a = 1 RETURNING *) SELECT * FROM x", "UPDATE")]
    [InlineData("WITH x AS (DELETE FROM foo RETURNING *) SELECT * FROM x", "DELETE")]
    [InlineData(
        "WITH a AS (SELECT 1), b AS (INSERT INTO foo VALUES (1)) SELECT * FROM a, b",
        "INSERT"
    )]
    [InlineData(
        "WITH a AS (WITH b AS (UPDATE foo SET x=1) SELECT * FROM b) SELECT * FROM a",
        "UPDATE"
    )]
    public void Validate_WriteInsideCte_RejectedRecursively(string sql, string innerVerb)
    {
        var violation = ReadOnlyStatementValidator.Validate(sql);
        violation.Should().NotBeNull();
        violation.Should().Contain(innerVerb);
    }

    [Theory]
    [InlineData("WITH x AS (UPDATE foo SET name = 'has a ) paren') SELECT * FROM x")]
    [InlineData("WITH x AS (INSERT INTO foo VALUES ('with ; semicolon')) SELECT * FROM x")]
    public void Validate_QuotedStringInsideCteBody_DoesNotConfuseParenScanner(string sql)
    {
        // The balanced-paren scanner must skip past quoted runs — a `)`
        // inside a string literal must not close the CTE body early, and
        // a `;` inside a string literal must not split the statement.
        var violation = ReadOnlyStatementValidator.Validate(sql);
        violation.Should().NotBeNull();
    }

    [Fact]
    public void Validate_ForbiddenKeywordInsideStringLiteral_IsAccepted()
    {
        // 'UPDATE' inside a string literal must not be treated as a verb.
        ReadOnlyStatementValidator
            .Validate("SELECT 'please UPDATE your settings' AS message")
            .Should()
            .BeNull();
    }

    [Fact]
    public void Validate_ForbiddenKeywordInsideQuotedIdentifier_IsAccepted()
    {
        // "UPDATE_TIME" is a column-name reference, not a verb.
        ReadOnlyStatementValidator.Validate("SELECT \"UPDATE_TIME\" FROM foo").Should().BeNull();
    }

    [Fact]
    public void Validate_ForbiddenKeywordInsidePgDollarQuoted_IsAccepted()
    {
        // $$...$$ is a PG string literal — UPDATE inside it is data.
        ReadOnlyStatementValidator
            .Validate("SELECT $$please UPDATE$$ AS message")
            .Should()
            .BeNull();
    }

    [Fact]
    public void Validate_ForbiddenKeywordInsidePgTaggedDollarQuoted_IsAccepted()
    {
        ReadOnlyStatementValidator
            .Validate("SELECT $tag$please UPDATE$tag$ AS message")
            .Should()
            .BeNull();
    }

    [Fact]
    public void Validate_SemicolonInsideStringLiteral_DoesNotSplitStatement()
    {
        // The statement splitter must not treat ; inside '...' as a
        // boundary — otherwise the string after it (DROP TABLE) would be
        // parsed as a separate forbidden statement.
        ReadOnlyStatementValidator
            .Validate("SELECT 'foo;DROP TABLE bar' AS s")
            .Should()
            .BeNull();
    }

    [Fact]
    public void Validate_MultiStatementWithOneForbidden_RejectsWholeBatch()
    {
        var violation = ReadOnlyStatementValidator.Validate("SELECT 1; INSERT INTO foo VALUES (1)");
        violation.Should().NotBeNull();
        violation.Should().Contain("INSERT");
    }

    [Fact]
    public void Validate_TrailingSemicolon_DoesNotProduceEmptyStatementViolation()
    {
        ReadOnlyStatementValidator.Validate("SELECT 1;").Should().BeNull();
    }

    [Fact]
    public void Validate_LeadingSemicolons_AreSkipped()
    {
        ReadOnlyStatementValidator.Validate(";;SELECT 1").Should().BeNull();
    }

    [Theory]
    [InlineData("WITH")]
    [InlineData("WITH x")]
    [InlineData("WITH x AS")]
    [InlineData("WITH x AS (SELECT 1)")]
    public void Validate_MalformedWith_ReturnsViolation(string sql)
    {
        // Each of these is missing either a CTE name, the AS keyword, the
        // CTE body parens, or the outer body — the validator refuses to
        // guess and treats them as malformed.
        ReadOnlyStatementValidator.Validate(sql).Should().NotBeNull();
    }
}

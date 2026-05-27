using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Verifies the whitelist that runs before the executor opens a connection:
/// under ReadOnly mode, only SELECT / VALUES / TABLE and CTE-bodied
/// equivalents reach the database. Every other statement shape — DML, DDL,
/// transaction control, permissions, session settings, anonymous blocks —
/// is neutralised by the executor with the same silent-rollback contract
/// used by the DBMS-level rejection from #106 (Success=true, empty data,
/// no write reaches the connection). The check is provider-agnostic, so it
/// closes the SQL Server and Oracle gap that the DBMS-level enforcement
/// cannot.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyStatementWhitelistTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyStatementWhitelistTests(PostgresFixture fixture)
        : base(fixture) { }

    [Theory]
    [InlineData("INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('M', 0)")]
    [InlineData("UPDATE \"Customers\" SET \"Name\" = 'M'")]
    [InlineData("DELETE FROM \"Customers\"")]
    [InlineData(
        "MERGE INTO \"Customers\" USING (SELECT 1) s ON true WHEN NOT MATCHED THEN INSERT DEFAULT VALUES"
    )]
    [InlineData("TRUNCATE \"Customers\"")]
    [InlineData("DROP TABLE \"Customers\"")]
    [InlineData("CREATE TABLE x (id int)")]
    [InlineData("ALTER TABLE \"Customers\" ADD COLUMN x int")]
    [InlineData("RENAME TABLE \"Customers\" TO x")]
    [InlineData("COMMENT ON TABLE \"Customers\" IS 'x'")]
    [InlineData("GRANT SELECT ON \"Customers\" TO public")]
    [InlineData("REVOKE SELECT ON \"Customers\" FROM public")]
    [InlineData("COMMIT")]
    [InlineData("ROLLBACK")]
    [InlineData("BEGIN")]
    [InlineData("START TRANSACTION")]
    [InlineData("SAVEPOINT s1")]
    [InlineData("RELEASE SAVEPOINT s1")]
    [InlineData("PREPARE TRANSACTION 'x'")]
    [InlineData("SET default_transaction_read_only = off")]
    [InlineData("RESET default_transaction_read_only")]
    [InlineData(
        "DO $$ BEGIN INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('M', 0); END $$"
    )]
    [InlineData("CALL my_proc()")]
    [InlineData("VACUUM \"Customers\"")]
    [InlineData("ANALYZE \"Customers\"")]
    [InlineData("REINDEX TABLE \"Customers\"")]
    [InlineData("COPY \"Customers\" FROM '/tmp/data.csv'")]
    [InlineData("EXPLAIN ANALYZE UPDATE \"Customers\" SET \"Name\" = 'M'")]
    [InlineData("SELECT 1; INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('M', 0)")]
    [InlineData(
        "WITH x AS (INSERT INTO \"Customers\" (\"Name\", \"Tier\") VALUES ('M', 0) RETURNING *) SELECT * FROM x"
    )]
    [InlineData("WITH x AS (UPDATE \"Customers\" SET \"Name\" = 'M' RETURNING *) SELECT * FROM x")]
    [InlineData("WITH x AS (DELETE FROM \"Customers\" RETURNING *) SELECT * FROM x")]
    public async Task Execute_ReadOnlyForbiddenStatement_NeutralizedAndDoesNotExecute(string sql)
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute(sql);

        // Same silent-neutralization contract as the DBMS-level rejection
        // from #106: Success=true with empty data, write did not persist.
        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Data.Should().BeEmpty();

        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );
        var count = await verify.Execute(
            "SELECT COUNT(*) AS c FROM \"Customers\" WHERE \"Name\" = 'M'"
        );
        Convert.ToInt32(count.Data![0]["c"]).Should().Be(0);
    }

    [Theory]
    [InlineData("SELECT 1 AS x")]
    [InlineData("SELECT * FROM \"Customers\"")]
    [InlineData("VALUES (1), (2), (3)")]
    [InlineData("TABLE \"Customers\"")]
    [InlineData("WITH x AS (SELECT 1 AS a) SELECT * FROM x")]
    [InlineData(
        "WITH RECURSIVE x AS (SELECT 1 AS a UNION ALL SELECT a + 1 FROM x WHERE a < 3) SELECT * FROM x"
    )]
    [InlineData("WITH x AS MATERIALIZED (SELECT 1 AS a) SELECT * FROM x")]
    [InlineData("WITH x AS NOT MATERIALIZED (SELECT 1 AS a) SELECT * FROM x")]
    [InlineData("WITH a AS (SELECT 1 AS x), b AS (SELECT 2 AS y) SELECT * FROM a, b")]
    // Keywords inside string literals and quoted identifiers must not be
    // treated as statement verbs — the test confirms a benign SELECT is
    // accepted even when its data mentions a forbidden keyword.
    [InlineData("SELECT 'please UPDATE your settings' AS message")]
    [InlineData("SELECT \"Name\" AS \"UPDATE_TIME\" FROM \"Customers\"")]
    public async Task Execute_ReadOnlyAllowedStatement_Succeeds(string sql)
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var result = await executor.Execute(sql);

        result.Success.Should().BeTrue();
    }
}

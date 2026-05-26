using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the SQL-2003 <c>MERGE</c> statement — the
/// newest DML keyword in the standard (PostgreSQL 15+), so a future top-level
/// allowlist that filters only the classic <c>INSERT</c>/<c>UPDATE</c>/
/// <c>DELETE</c> trio would miss it entirely. The contract is the same as for
/// plain INSERT — with <c>ReadOnly = true</c> the rollback must neutralise
/// the write — but the mutation is expressed through a single MERGE statement
/// whose leading token is neither <c>INSERT</c> nor part of a CTE. Durability
/// is verified on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyMergeStatementProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyMergeStatementProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyMergeStatementInsert_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "MERGE INTO \"Customers\" AS c "
                + "USING (SELECT 'Mallory' AS name, 99 AS tier) AS s "
                + "ON c.\"Name\" = s.name "
                + "WHEN NOT MATCHED THEN INSERT (\"Name\", \"Tier\") VALUES (s.name, s.tier)"
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

        // Independent connection: the true durability oracle for "did it persist".
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var poisoned = await verify.Execute(
            "SELECT COUNT(*) AS c FROM \"Customers\" WHERE \"Name\" = 'Mallory'"
        );

        Convert.ToInt32(poisoned.Data![0]["c"]).Should().Be(0);
    }
}

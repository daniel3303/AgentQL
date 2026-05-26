using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against composed CTE + MERGE bypass — both shapes are
/// pinned separately, but an LLM could combine them as
/// <c>WITH x AS (...) MERGE INTO ... USING x ...</c> to defeat a hypothetical
/// allowlist that scans for either CTE-with-DML OR a top-level MERGE leading
/// keyword in isolation. The composition keeps <c>WITH</c> as the leading
/// token while the destructive verb hides one layer deeper. The contract is
/// the same — with <c>ReadOnly = true</c> the rollback must neutralise the
/// inserted row — and verification runs on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCteWrappedMergeProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCteWrappedMergeProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCteWrappedMergeInsert_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "WITH x AS (SELECT 'Mallory' AS name, 99 AS tier) "
                + "MERGE INTO \"Customers\" AS c "
                + "USING x AS s ON c.\"Name\" = s.name "
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

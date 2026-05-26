using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against the CTE-with-DML bypass, UPDATE variant. The
/// matching DELETE shape is already pinned; the UPDATE shape exercises a
/// different DML verb wrapped inside a single <c>WITH ... SELECT</c> that
/// looks SELECT-shaped to a naive guard. It is the most plausible second
/// move for an LLM steered by "you may only SELECT" once it discovers the
/// CTE trick. The contract is the same as for plain UPDATE — with
/// <c>ReadOnly = true</c> the rollback must neutralise the write. Durability
/// is verified on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCteUpdateProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCteUpdateProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCteWrappedUpdate_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var smuggled = await executor.Execute(
            "WITH u AS (UPDATE \"Customers\" SET \"Tier\" = 99 RETURNING *) SELECT * FROM u"
        );
        smuggled.Success.Should().BeTrue(smuggled.ErrorMessage);

        // Independent connection: the true durability oracle for "did it persist".
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var tampered = await verify.Execute(
            "SELECT COUNT(*) AS c FROM \"Customers\" WHERE \"Tier\" = 99"
        );

        Convert.ToInt32(tampered.Data![0]["c"]).Should().Be(0);
    }
}

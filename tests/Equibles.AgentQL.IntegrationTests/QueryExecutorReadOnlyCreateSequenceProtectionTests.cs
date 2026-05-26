using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee for sequence DDL — sequences are a distinct catalog
/// object (rows in <c>pg_sequence</c> alongside a <c>pg_class</c> entry),
/// useful to an LLM as poison-row identifier generators or as named markers
/// across a multi-stage attack that wants to coordinate state. The contract
/// is the same as for other create-DDL pins — with <c>ReadOnly = true</c> the
/// rollback must undo the sequence — but the path is independent of the
/// table / view / index / schema variants already pinned. Verification runs
/// on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateSequenceProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateSequenceProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateSequence_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var create = await executor.Execute("CREATE SEQUENCE \"bomb_seq\"");
        create.Success.Should().BeTrue(create.ErrorMessage);

        // Independent connection: the true durability oracle. If the CREATE
        // SEQUENCE had leaked, the new sequence would surface in
        // information_schema.sequences and be callable from any later session.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM information_schema.sequences "
                + "WHERE sequence_name = 'bomb_seq'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

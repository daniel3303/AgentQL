using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee for index DDL — an LLM could emit
/// <c>CREATE INDEX "bomb_idx" ON "Customers" ("Name")</c> to consume disk and
/// CPU during the build (a quiet DoS), or to bias query planning toward
/// expressions an attacker controls. Different catalog path from the existing
/// table-level DDL pins (DROP / ALTER / CREATE TABLE / CREATE VIEW) — index
/// creation writes <c>pg_index</c> rather than <c>pg_class</c>/
/// <c>pg_attribute</c>. With <c>ReadOnly = true</c> the rollback must undo
/// the index; verification runs on an INDEPENDENT connection against
/// <c>pg_indexes</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateIndexProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateIndexProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateIndex_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var create = await executor.Execute(
            "CREATE INDEX \"bomb_idx\" ON \"Customers\" (\"Name\")"
        );
        create.Success.Should().BeTrue(create.ErrorMessage);

        // Independent connection: the true durability oracle. If the CREATE
        // INDEX had leaked, this catalog probe would return one row for the
        // new index.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM pg_indexes WHERE indexname = 'bomb_idx'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

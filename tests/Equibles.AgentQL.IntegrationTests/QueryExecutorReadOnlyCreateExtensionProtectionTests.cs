using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee against capability-expansion attacks — an LLM could
/// install contrib extensions like <c>hstore</c>, <c>dblink</c>, or
/// <c>file_fdw</c> to widen the available attack surface (cross-database
/// queries, file-system access, additional functions). Different threat class
/// from the row/catalog mutations already pinned, and a distinct catalog
/// path (<c>pg_extension</c>). <c>hstore</c> is shipped with the postgres
/// alpine image and is not installed in the fixture's fresh database; with
/// <c>ReadOnly = true</c> the rollback must undo the install. Verification
/// runs on an INDEPENDENT connection.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyCreateExtensionProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyCreateExtensionProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyCreateExtension_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var create = await executor.Execute("CREATE EXTENSION \"hstore\"");
        create.Success.Should().BeTrue(create.ErrorMessage);

        // Independent connection: the true durability oracle. If the CREATE
        // EXTENSION had leaked, the new extension would surface in
        // pg_extension and its functions would be callable from every session.
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT COUNT(*) AS c FROM pg_extension WHERE extname = 'hstore'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        Convert.ToInt32(probe.Data![0]["c"]).Should().Be(0);
    }
}

using AwesomeAssertions;
using Equibles.AgentQL.Configuration;
using Equibles.AgentQL.EntityFrameworkCore.Query;
using Equibles.AgentQL.IntegrationTests.TestModel;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Equibles.AgentQL.IntegrationTests;

/// <summary>
/// Security guarantee for the lockdown/DoS shape — the inverse of the GRANT
/// pin. An LLM could emit
/// <c>REVOKE USAGE ON SCHEMA public FROM PUBLIC</c> to cut every non-owner
/// role's access to objects in the public schema in one statement — a quiet
/// denial-of-service attack distinct from data mutation or backdoor opening.
/// PostgreSQL ships <c>public</c> with PUBLIC granted USAGE by default, so
/// the catalog ACL encodes that grant as <c>=U/</c> inside <c>nspacl</c>;
/// a successful REVOKE removes the <c>U</c>. With <c>ReadOnly = true</c> the
/// rollback must keep the grant intact. Verification runs on an INDEPENDENT
/// connection by checking the ACL text still contains the PUBLIC USAGE entry.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class QueryExecutorReadOnlyRevokeSchemaUsageProtectionTests : IntegrationTestBase
{
    public QueryExecutorReadOnlyRevokeSchemaUsageProtectionTests(PostgresFixture fixture)
        : base(fixture) { }

    [Fact]
    public async Task Execute_ReadOnlyRevokeSchemaUsageFromPublic_DoesNotPersistAcrossConnections()
    {
        await using var executorContext = Fixture.CreateContext();
        var executor = new QueryExecutor<TravelTestDbContext>(
            executorContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var revoke = await executor.Execute("REVOKE USAGE ON SCHEMA public FROM PUBLIC");
        revoke.Success.Should().BeTrue(revoke.ErrorMessage);

        // Independent connection: the true durability oracle. If the REVOKE
        // had leaked, the PUBLIC USAGE entry would be gone from nspacl
        // (the `=U/` pattern represents PUBLIC having USAGE).
        await using var verifyContext = Fixture.CreateContext();
        var verify = new QueryExecutor<TravelTestDbContext>(
            verifyContext,
            new AgentQLOptions { ReadOnly = true },
            NullLogger<QueryExecutor<TravelTestDbContext>>.Instance
        );

        var probe = await verify.Execute(
            "SELECT POSITION('=U/' IN nspacl::text) > 0 AS public_usage_present "
                + "FROM pg_namespace WHERE nspname = 'public'"
        );

        probe.Success.Should().BeTrue(probe.ErrorMessage);
        probe.Data![0]["public_usage_present"].Should().Be(true);
    }
}

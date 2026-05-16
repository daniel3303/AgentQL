using AwesomeAssertions;
using Equibles.AgentQL.MicrosoftAI.Configuration;
using Xunit;

namespace Equibles.AgentQL.UnitTests.Configuration;

public class AgentQLChatOptionsUnknownProviderTests
{
    // Contract: with no explicit Endpoint, GetEndpoint resolves a default per
    // provider; an unrecognized AiProvider must fail loud (throw), never return
    // null/empty that would surface later as an opaque HTTP failure. Existing
    // tests cover only the three defined providers + the explicit override.
    [Fact]
    public void GetEndpoint_UnknownProviderWithoutExplicitEndpoint_Throws()
    {
        var options = new AgentQLChatOptions { Provider = (AiProvider)999 };

        var act = () => options.GetEndpoint();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}

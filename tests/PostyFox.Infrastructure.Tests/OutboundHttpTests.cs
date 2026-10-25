using PostyFox.Infrastructure.Http;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class OutboundHttpTests
{
    [Fact]
    public void UseDefaultUserAgent_sends_the_PostyFox_user_agent()
    {
        using var client = new HttpClient();
        OutboundHttp.UseDefaultUserAgent(client);
        Assert.Equal(OutboundHttp.UserAgent, client.DefaultRequestHeaders.UserAgent.ToString());
    }
}

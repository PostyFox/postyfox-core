using System.Net;
using PostyFox.Api.Core.Tests.Support;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class AuthTests
{
    [Fact]
    public async Task Requests_without_identity_are_unauthorized_when_devmode_off()
    {
        using var factory = new CustomWebApplicationFactory { DevMode = false };
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        using var factory = new CustomWebApplicationFactory { DevMode = false };
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}

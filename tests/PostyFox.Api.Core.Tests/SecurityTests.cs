using System.Net;
using PostyFox.Api.Core.Tests.Support;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class SecurityTests
{
    [Fact]
    public async Task Security_headers_are_present()
    {
        using var factory = new CustomWebApplicationFactory();
        var resp = await factory.CreateClient().GetAsync("/healthz");
        Assert.Equal("nosniff", resp.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", resp.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Rate_limiter_returns_429_when_exceeded()
    {
        using var factory = new CustomWebApplicationFactory { RateLimitPermits = 3 };
        var client = factory.CreateClient();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            codes.Add((await client.GetAsync("/healthz")).StatusCode);

        Assert.Equal(3, codes.Count(c => c == HttpStatusCode.OK));
        Assert.Contains(HttpStatusCode.TooManyRequests, codes);
    }
}

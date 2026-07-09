using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Api.Post.Tests.Support;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Application.Triggers;
using Xunit;

namespace PostyFox.Api.Post.Tests;

public class WebhookEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Secret = "webhook-secret";
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WebhookEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        // Seed the signing secret + a trigger bound to the factory's seeded connector.
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISecretStore>()
            .SetSecretAsync("trigger-generic-signing", Secret).GetAwaiter().GetResult();
        scope.ServiceProvider.GetRequiredService<ExternalTriggerService>()
            .RegisterAsync("dev-user", new TriggerRegistrationRequest("generic", "acme", null, factory.SeededConnectorId, 0))
            .GetAwaiter().GetResult();
    }

    private static HttpRequestMessage Signed(string body, string messageId)
    {
        var sig = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body)));
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/generic")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("X-Signature", sig);
        req.Headers.Add("X-Message-Id", messageId);
        return req;
    }

    [Fact]
    public async Task Valid_webhook_is_processed()
    {
        var resp = await _client.SendAsync(Signed("{\"account\":\"acme\",\"variables\":{}}", "wh1"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains("processed", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Challenge_is_echoed()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/generic")
        { Content = new StringContent("{\"challenge\":\"hello-echo\"}", Encoding.UTF8, "application/json") };
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("hello-echo", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Bad_signature_is_unauthorized()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/generic")
        { Content = new StringContent("{\"account\":\"acme\"}", Encoding.UTF8, "application/json") };
        req.Headers.Add("X-Signature", "deadbeef");
        req.Headers.Add("X-Message-Id", "bad1");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Unknown_source_is_not_found()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/nope")
        { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

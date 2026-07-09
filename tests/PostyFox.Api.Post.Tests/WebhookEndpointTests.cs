using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Api.Post.Tests.Support;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Application.Triggers;
using Xunit;

namespace PostyFox.Api.Post.Tests;

public class WebhookEndpointTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Secret = "topsecret";

    private static string Sign(string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body)));

    private async Task ConfigureSecretAndTriggerAsync(string account)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISecretStore>().SetSecretAsync("trigger-generic-signing", Secret);
        await scope.ServiceProvider.GetRequiredService<ExternalTriggerService>()
            .RegisterAsync("dev-user", new TriggerRegistrationRequest("generic", account, null, factory.SeededConnectorId, 24));
    }

    private static HttpRequestMessage Signed(string body, string messageId)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/generic") { Content = new StringContent(body) };
        req.Headers.Add("X-Signature", Sign(body));
        req.Headers.Add("X-Message-Id", messageId);
        return req;
    }

    [Fact]
    public async Task Valid_signed_webhook_fans_out()
    {
        await ConfigureSecretAndTriggerAsync("valid-acct");
        var body = "{\"account\":\"valid-acct\",\"variables\":{}}";

        var resp = await factory.CreateClient().SendAsync(Signed(body, "wh-1"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var payload = await resp.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        Assert.Equal(1, payload!["processed"]);
    }

    [Fact]
    public async Task Challenge_is_echoed()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/generic")
        { Content = new StringContent("{\"challenge\":\"echo-me\"}") };
        var resp = await factory.CreateClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("echo-me", await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Bad_signature_is_unauthorized()
    {
        await ConfigureSecretAndTriggerAsync("bad-acct");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/generic")
        { Content = new StringContent("{\"account\":\"bad-acct\"}") };
        req.Headers.Add("X-Signature", "bad");
        req.Headers.Add("X-Message-Id", "wh-2");
        var resp = await factory.CreateClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Unknown_source_is_not_found()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/nope") { Content = new StringContent("{}") };
        var resp = await factory.CreateClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

using System.Net;
using Microsoft.Extensions.Options;
using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Connectors;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class HttpConnectorTests
{
    private static HttpConnector New(StubHttpHandler handler) => new(
        "BlueSky",
        new ConnectorDescriptor("BlueSky", "Bluesky", false, true, true, 300),
        new StubHttpClientFactory(handler),
        Options.Create(new NodeConnectorsOptions { BaseUrl = "http://node:8090", InternalToken = "tok" }));

    private static ConnectorContext Ctx() => new(Guid.NewGuid(), "u1", "{\"Handle\":\"a.bsky\"}", "{\"AppPassword\":\"p\"}", null);
    private static RenderedPost Post(IReadOnlyList<MediaRef>? media = null) => new(null, "hello", [], media ?? []);

    [Fact]
    public async Task Deliver_success_maps_external_id_and_url_and_sends_token()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"success\":true,\"externalId\":\"at://x\",\"externalUrl\":\"https://bsky.app/p\"}");
        var result = await New(handler).DeliverAsync(Ctx(), Post());

        Assert.True(result.Success);
        Assert.Equal("at://x", result.ExternalId);
        Assert.Equal("https://bsky.app/p", result.ExternalUrl);
        Assert.Equal("tok", handler.LastRequest!.Headers.GetValues("X-Internal-Token").Single());
        Assert.EndsWith("/connectors/BlueSky/deliver", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Deliver_sends_media_refs_with_alt_not_bytes()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"success\":true}");
        var post = Post([new MediaRef("media", "u1/x/pic.png", "image/png", "a cat")]);
        await New(handler).DeliverAsync(Ctx(), post);

        Assert.Contains("\"key\":\"u1/x/pic.png\"", handler.LastBody);
        Assert.Contains("\"alt\":\"a cat\"", handler.LastBody);
        Assert.DoesNotContain("dataBase64", handler.LastBody);
    }

    [Fact]
    public async Task Deliver_failure_maps_error()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"success\":false,\"error\":\"login failed\"}");
        var result = await New(handler).DeliverAsync(Ctx(), Post());
        Assert.False(result.Success);
        Assert.Equal("login failed", result.Error);
    }

    [Fact]
    public async Task Non_success_http_reports_unavailable()
    {
        var handler = new StubHttpHandler(HttpStatusCode.InternalServerError, "boom");
        var result = await New(handler).DeliverAsync(Ctx(), Post());
        Assert.False(result.Success);
        Assert.Contains("unavailable", result.Error);
    }

    [Fact]
    public async Task IsAuthenticated_parses_response()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"isAuthenticated\":true}");
        Assert.True((await New(handler).IsAuthenticatedAsync(Ctx())).IsAuthenticated);
    }

    [Fact]
    public async Task ListTargets_parses_array()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"targets\":[{\"id\":\"a\",\"name\":\"Blog A\"}]}");
        var targets = await New(handler).ListTargetsAsync(Ctx());
        Assert.Single(targets);
        Assert.Equal("Blog A", targets[0].Name);
    }
}

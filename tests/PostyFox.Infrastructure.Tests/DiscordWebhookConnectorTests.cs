using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Connectors;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class DiscordWebhookConnectorTests
{
    private static DiscordWebhookConnector New(StubHttpHandler handler, IMediaResolver? resolver = null) =>
        new(new StubHttpClientFactory(handler), resolver ?? new FakeMediaResolver(), NullLogger<DiscordWebhookConnector>.Instance);

    private static ConnectorContext Context(string configJson) => new(Guid.NewGuid(), "u1", configJson, null, null);
    private static RenderedPost Post(IReadOnlyList<MediaRef>? media = null) => new("Title", "Hello world", [], media ?? []);

    [Fact]
    public async Task Deliver_posts_to_webhook_and_returns_message_id()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"id\":\"999\"}");
        var result = await New(handler).DeliverAsync(Context("{\"Webhook\":\"http://discord/wh\"}"), Post());

        Assert.True(result.Success);
        Assert.Equal("999", result.ExternalId);
        Assert.Contains("wait=true", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("Hello world", handler.LastBody);
    }

    [Fact]
    public async Task Deliver_returns_failure_on_http_error()
    {
        var handler = new StubHttpHandler(HttpStatusCode.BadRequest, "nope");
        var result = await New(handler).DeliverAsync(Context("{\"Webhook\":\"http://discord/wh\"}"), Post());
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Deliver_fails_when_no_webhook_configured()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{}");
        var result = await New(handler).DeliverAsync(Context("{}"), Post());
        Assert.False(result.Success);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task Deliver_with_media_resolves_normalized_media_and_sends_multipart()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"id\":\"55\"}");
        var resolver = new FakeMediaResolver();
        var post = Post([new MediaRef("media", "u1/abc/pic.png", "image/png")]);

        var result = await New(handler, resolver).DeliverAsync(Context("{\"Webhook\":\"http://discord/wh\"}"), post);

        Assert.True(result.Success);
        Assert.Equal("multipart/form-data", handler.LastRequest!.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("pic.png", handler.LastBody);
        Assert.Contains("payload_json", handler.LastBody);
        // The connector must route media through the resolver with Discord's declared spec.
        Assert.Same(PostyFox.Infrastructure.Media.PlatformMediaSpecs.Discord, resolver.LastSpec);
    }

    [Fact]
    public void Describe_reports_platform_and_media_support()
    {
        var d = New(new StubHttpHandler(HttpStatusCode.OK, "{}")).Describe();
        Assert.Equal("DiscordWH", d.Platform);
        Assert.True(d.SupportsMedia);
    }
}

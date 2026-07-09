using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Connectors;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class DiscordWebhookConnectorTests
{
    private static ConnectorContext Context(string configJson) =>
        new(Guid.NewGuid(), "u1", configJson, null, null);

    private static RenderedPost Post() => new("Title", "Hello world", [], []);

    [Fact]
    public async Task Deliver_posts_to_webhook_and_returns_message_id()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"id\":\"999\"}");
        var connector = new DiscordWebhookConnector(new StubHttpClientFactory(handler), NullLogger<DiscordWebhookConnector>.Instance);

        var result = await connector.DeliverAsync(Context("{\"Webhook\":\"http://discord/wh\"}"), Post());

        Assert.True(result.Success);
        Assert.Equal("999", result.ExternalId);
        Assert.Contains("wait=true", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("Hello world", handler.LastBody);
    }

    [Fact]
    public async Task Deliver_returns_failure_on_http_error()
    {
        var handler = new StubHttpHandler(HttpStatusCode.BadRequest, "nope");
        var connector = new DiscordWebhookConnector(new StubHttpClientFactory(handler), NullLogger<DiscordWebhookConnector>.Instance);

        var result = await connector.DeliverAsync(Context("{\"Webhook\":\"http://discord/wh\"}"), Post());
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Deliver_fails_when_no_webhook_configured()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{}");
        var connector = new DiscordWebhookConnector(new StubHttpClientFactory(handler), NullLogger<DiscordWebhookConnector>.Instance);

        var result = await connector.DeliverAsync(Context("{}"), Post());
        Assert.False(result.Success);
        Assert.Null(handler.LastRequest); // never called out
    }

    [Fact]
    public void Describe_reports_platform()
    {
        var connector = new DiscordWebhookConnector(new StubHttpClientFactory(new StubHttpHandler(HttpStatusCode.OK, "{}")), NullLogger<DiscordWebhookConnector>.Instance);
        Assert.Equal("DiscordWH", connector.Describe().Platform);
    }
}

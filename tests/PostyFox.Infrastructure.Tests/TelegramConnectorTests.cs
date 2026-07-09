using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Connectors;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class TelegramConnectorTests
{
    private static ConnectorContext Ctx(string config) => new(Guid.NewGuid(), "u1", config, null, null);
    private static RenderedPost Post(IReadOnlyList<MediaRef>? media = null) => new("Title", "<b>hi</b>", [], media ?? []);

    [Fact]
    public async Task Deliver_sends_via_gateway_to_configured_chat()
    {
        var gw = new FakeTelegramGateway();
        var result = await new TelegramConnector(gw).DeliverAsync(Ctx("{\"PhoneNumber\":\"+123\",\"DefaultPostingTarget\":\"555\"}"), Post());

        Assert.True(result.Success);
        Assert.Equal("555", gw.LastSend!.Value.chatId);
        Assert.Equal("+123", gw.LastSend.Value.phone);
        Assert.Contains("<b>Title</b>", gw.LastSend.Value.body);
    }

    [Fact]
    public async Task Deliver_fails_without_phone() =>
        Assert.False((await new TelegramConnector(new FakeTelegramGateway()).DeliverAsync(Ctx("{}"), Post())).Success);

    [Fact]
    public async Task Deliver_fails_without_target() =>
        Assert.False((await new TelegramConnector(new FakeTelegramGateway()).DeliverAsync(Ctx("{\"PhoneNumber\":\"+1\"}"), Post())).Success);

    [Fact]
    public async Task IsAuthenticated_reflects_gateway()
    {
        var gw = new FakeTelegramGateway { Authenticated = false };
        var state = await new TelegramConnector(gw).IsAuthenticatedAsync(Ctx("{\"PhoneNumber\":\"+1\"}"));
        Assert.False(state.IsAuthenticated);
    }

    [Fact]
    public async Task ListTargets_returns_gateway_chats() =>
        Assert.Single(await new TelegramConnector(new FakeTelegramGateway()).ListTargetsAsync(Ctx("{\"PhoneNumber\":\"+1\"}")));

    [Fact]
    public async Task Deliver_passes_media_refs_to_gateway()
    {
        var gw = new FakeTelegramGateway();
        var media = new List<MediaRef> { new("media", "k1", "image/jpeg"), new("media", "k2", "image/png") };
        await new TelegramConnector(gw).DeliverAsync(Ctx("{\"PhoneNumber\":\"+1\",\"DefaultPostingTarget\":\"5\"}"), Post(media));
        Assert.Equal(2, gw.LastMediaCount);
    }
}

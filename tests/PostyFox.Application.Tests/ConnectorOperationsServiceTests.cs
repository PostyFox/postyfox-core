using PostyFox.Application.Connectors;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class ConnectorOperationsServiceTests
{
    private static async Task<Guid> SeedAsync(TestDbContext db, string platform, string config)
    {
        db.ServiceDefinitions.Add(new ServiceDefinition { Id = platform, Name = platform, Platform = platform, Enabled = true });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector { Id = id, UserId = "u1", ServiceDefinitionId = platform, DisplayName = platform, ConfigJson = config, Enabled = true });
        await db.SaveChangesAsync();
        return id;
    }

    private static ConnectorOperationsService New(TestDbContext db, params IConnector[] connectors) =>
        new(db, new FakeSecretStore(), new FakeRegistry(connectors), new FakeTelegramGateway());

    [Fact]
    public async Task IsAuthenticated_dispatches_to_connector()
    {
        using var db = TestDbContext.Create();
        var id = await SeedAsync(db, "DiscordWH", "{\"Webhook\":\"http://x\"}");
        var svc = New(db, new FakeConnector("DiscordWH"));

        var state = await svc.IsAuthenticatedAsync("u1", id);
        Assert.NotNull(state);
        Assert.True(state!.IsAuthenticated);
    }

    [Fact]
    public async Task Unknown_connector_returns_null()
    {
        using var db = TestDbContext.Create();
        Assert.Null(await New(db).IsAuthenticatedAsync("u1", Guid.NewGuid()));
    }

    [Fact]
    public async Task Platform_without_registered_connector_reports_unauthenticated()
    {
        using var db = TestDbContext.Create();
        var id = await SeedAsync(db, "Ghost", "{}");
        var state = await New(db).IsAuthenticatedAsync("u1", id); // no connectors registered
        Assert.NotNull(state);
        Assert.False(state!.IsAuthenticated);
    }

    [Fact]
    public async Task TelegramLogin_advances_flow_using_configured_phone()
    {
        using var db = TestDbContext.Create();
        var id = await SeedAsync(db, "Telegram", "{\"PhoneNumber\":\"+123\"}");
        var gw = new FakeTelegramGateway();
        gw.Steps.Enqueue(new TelegramLoginStep(TelegramLoginStep.NeedsCode, "value", "Verification Code"));
        var svc = new ConnectorOperationsService(db, new FakeSecretStore(), new FakeRegistry(), gw);

        var step = await svc.TelegramLoginAsync("u1", id, null);
        Assert.Equal(TelegramLoginStep.NeedsCode, step!.Status);
    }

    [Fact]
    public async Task TelegramLogin_without_phone_returns_null()
    {
        using var db = TestDbContext.Create();
        var id = await SeedAsync(db, "Telegram", "{}");
        var svc = new ConnectorOperationsService(db, new FakeSecretStore(), new FakeRegistry(), new FakeTelegramGateway());
        Assert.Null(await svc.TelegramLoginAsync("u1", id, null));
    }
}

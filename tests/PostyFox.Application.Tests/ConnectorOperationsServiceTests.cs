using PostyFox.Application.Connectors;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class ConnectorOperationsServiceTests
{
    private static async Task<Guid> SeedAsync(TestDbContext db, string platform, string config, string displayName = "")
    {
        db.ServiceDefinitions.Add(new ServiceDefinition { Id = platform, Name = platform, Platform = platform, Enabled = true });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector { Id = id, UserId = "u1", ServiceDefinitionId = platform, DisplayName = string.IsNullOrEmpty(displayName) ? platform : displayName, ConfigJson = config, Enabled = true });
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

    [Fact]
    public async Task GetLimits_exposes_media_spec_size_caps_for_in_process_connectors()
    {
        using var db = TestDbContext.Create();
        var spec = new MediaSpec(
            new ImageSpec(1024, 1024, 5_000_000, ["image/jpeg"]),
            new VideoSpec(1920, 1080, 50_000_000, 60, ["video/mp4"]));
        var connector = new FakeConnectorWithMediaSpec("Platform1", spec);
        var id = await SeedAsync(db, "Platform1", "{}");
        var svc = New(db, connector);

        var limits = await svc.GetLimitsAsync("u1", id);

        Assert.NotNull(limits);
        Assert.Equal(5_000_000, limits!.ImageSizeLimit);
        Assert.Equal(50_000_000, limits.VideoSizeLimit);
    }

    [Fact]
    public async Task CheckMedia_flags_oversized_image_as_will_resize()
    {
        using var db = TestDbContext.Create();
        var spec = new MediaSpec(
            new ImageSpec(null, null, 1_000_000, []),
            new VideoSpec(null, null, null, null, []));
        var id = await SeedAsync(db, "Platform2", "{}", "My Platform");
        var svc = New(db, new FakeConnectorWithMediaSpec("Platform2", spec));

        var results = await svc.CheckMediaAsync("u1", [id], fileSize: 2_000_000, mimeType: "image/jpeg");

        Assert.Single(results);
        Assert.True(results[0].WillResize);
        Assert.Equal(id, results[0].ConnectorId);
        Assert.Equal("My Platform", results[0].DisplayName);
        Assert.Equal(1_000_000, results[0].ImageSizeLimit);
    }

    [Fact]
    public async Task CheckMedia_does_not_flag_image_within_limits()
    {
        using var db = TestDbContext.Create();
        var spec = new MediaSpec(
            new ImageSpec(null, null, 5_000_000, []),
            new VideoSpec(null, null, null, null, []));
        var id = await SeedAsync(db, "Platform3", "{}");
        var svc = New(db, new FakeConnectorWithMediaSpec("Platform3", spec));

        var results = await svc.CheckMediaAsync("u1", [id], fileSize: 1_000_000, mimeType: "image/png");

        Assert.Single(results);
        Assert.False(results[0].WillResize);
    }

    [Fact]
    public async Task CheckMedia_flags_oversized_video_as_will_resize()
    {
        using var db = TestDbContext.Create();
        var spec = new MediaSpec(
            new ImageSpec(null, null, null, []),
            new VideoSpec(null, null, 10_000_000, null, []));
        var id = await SeedAsync(db, "Platform4", "{}");
        var svc = New(db, new FakeConnectorWithMediaSpec("Platform4", spec));

        var results = await svc.CheckMediaAsync("u1", [id], fileSize: 20_000_000, mimeType: "video/mp4");

        Assert.Single(results);
        Assert.True(results[0].WillResize);
        Assert.Equal(10_000_000, results[0].VideoSizeLimit);
    }

    [Fact]
    public async Task CheckMedia_skips_unknown_connector_ids()
    {
        using var db = TestDbContext.Create();
        var svc = New(db, new FakeConnector("Platform5"));

        var results = await svc.CheckMediaAsync("u1", [Guid.NewGuid()], fileSize: 1_000_000, mimeType: "image/jpeg");

        Assert.Empty(results);
    }
}

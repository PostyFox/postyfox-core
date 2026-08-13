using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class ConnectorDestinationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedConnectorAsync(TestDbContext db, string userId, string platform = "Telegram", bool enabled = true)
    {
        if (!db.ServiceDefinitions.Any(s => s.Id == platform))
            db.ServiceDefinitions.Add(new ServiceDefinition { Id = platform, Name = platform, Platform = platform, Enabled = true });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = id, UserId = userId, ServiceDefinitionId = platform, DisplayName = platform, Enabled = enabled
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static ConnectorDestinationService New(TestDbContext db, FakeRegistry registry) =>
        new(db, new FixedClock(Now), registry);

    [Fact]
    public async Task ListAsync_returns_null_when_connector_is_not_the_users()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "owner");
        var svc = New(db, new FakeRegistry(new FakeConnector("Telegram")));

        var result = await svc.ListAsync("someone-else", connectorId);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_adds_new_destinations()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1");
        var svc = New(db, new FakeRegistry(new FakeMultiTargetConnector("Telegram")));

        var result = await svc.SetAsync("u1", connectorId, [
            new ConnectorDestinationInput("-100111", "Channel A"),
            new ConnectorDestinationInput("-100222", "Channel B")
        ]);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Contains(result, d => d.ExternalId == "-100111" && d.Name == "Channel A");
        Assert.Contains(result, d => d.ExternalId == "-100222" && d.Name == "Channel B");
    }

    [Fact]
    public async Task SetAsync_removes_destinations_no_longer_selected()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1");
        var svc = New(db, new FakeRegistry(new FakeMultiTargetConnector("Telegram")));
        await svc.SetAsync("u1", connectorId, [
            new ConnectorDestinationInput("-100111", "Channel A"),
            new ConnectorDestinationInput("-100222", "Channel B")
        ]);

        var result = await svc.SetAsync("u1", connectorId, [
            new ConnectorDestinationInput("-100111", "Channel A")
        ]);

        Assert.NotNull(result);
        var only = Assert.Single(result!);
        Assert.Equal("-100111", only.ExternalId);
    }

    [Fact]
    public async Task SetAsync_updates_the_name_of_an_existing_destination()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1");
        var svc = New(db, new FakeRegistry(new FakeMultiTargetConnector("Telegram")));
        await svc.SetAsync("u1", connectorId, [new ConnectorDestinationInput("-100111", "Old Name")]);

        var result = await svc.SetAsync("u1", connectorId, [new ConnectorDestinationInput("-100111", "New Name")]);

        Assert.NotNull(result);
        var only = Assert.Single(result!);
        Assert.Equal("New Name", only.Name);
    }

    [Fact]
    public async Task SetAsync_returns_null_when_connector_does_not_support_multiple_targets()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1", "DiscordWH");
        var svc = New(db, new FakeRegistry(new FakeConnector("DiscordWH"))); // SupportsMultipleTargets: false

        var result = await svc.SetAsync("u1", connectorId, [new ConnectorDestinationInput("x", "y")]);

        Assert.Null(result);
        Assert.Empty(db.ConnectorDestinations);
    }

    [Fact]
    public async Task SetAsync_returns_null_when_connector_is_not_the_users()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "owner");
        var svc = New(db, new FakeRegistry(new FakeMultiTargetConnector("Telegram")));

        var result = await svc.SetAsync("someone-else", connectorId, [new ConnectorDestinationInput("x", "y")]);

        Assert.Null(result);
    }

    [Fact]
    public async Task ListAllAsync_flattens_destinations_across_connectors_with_platform_and_display_name()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1");
        var svc = New(db, new FakeRegistry(new FakeMultiTargetConnector("Telegram")));
        await svc.SetAsync("u1", connectorId, [new ConnectorDestinationInput("-100111", "Channel A")]);

        var all = await svc.ListAllAsync("u1");

        var only = Assert.Single(all);
        Assert.Equal("Telegram", only.Platform);
        Assert.Equal(connectorId, only.ConnectorId);
        Assert.Equal("-100111", only.ExternalId);
        Assert.Equal("Channel A", only.Name);
    }

    [Fact]
    public async Task ListAllAsync_excludes_other_users_destinations()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1");
        var svc = New(db, new FakeRegistry(new FakeMultiTargetConnector("Telegram")));
        await svc.SetAsync("u1", connectorId, [new ConnectorDestinationInput("-100111", "Channel A")]);

        var all = await svc.ListAllAsync("someone-else");

        Assert.Empty(all);
    }
}

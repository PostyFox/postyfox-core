using Microsoft.Extensions.Options;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Application.Tests;

public class PostIntakeServiceTests
{
    private static async Task<Guid> SeedConnectorAsync(TestDbContext db, string userId, bool enabled = true)
    {
        db.ServiceDefinitions.Add(new ServiceDefinition { Id = "DiscordWH", Name = "Discord", Platform = "DiscordWH", Enabled = true });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = id, UserId = userId, ServiceDefinitionId = "DiscordWH", DisplayName = "d", Enabled = enabled
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static PostIntakeService New(TestDbContext db, FakeBus bus, FixedClock clock) =>
        new(db, new FakeObjectStore(), bus, clock, new FakeRegistry(new FakeConnector("DiscordWH")),
            Microsoft.Extensions.Options.Options.Create(new PipelineOptions()));

    [Fact]
    public async Task Create_persists_post_and_enqueues_generate_per_target()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1");
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(DateTimeOffset.UnixEpoch));

        var result = await svc.CreateAsync("u1", new CreatePostRequest(
            [connectorId], "Title", "Body", "<p>Body</p>", ["tag"], null, null, null, null,
            ContentRating.Mature));

        Assert.NotNull(result);
        var post = Assert.Single(db.Posts);
        Assert.Single(db.PostTargets);
        Assert.Equal(ContentRating.Mature, post.Rating);
        var cmd = Assert.Single(bus.Of<GenerateTargetCommand>());
        Assert.Equal(post.Id, cmd.PostId);
    }

    [Fact]
    public async Task Create_with_no_valid_targets_returns_null()
    {
        using var db = TestDbContext.Create();
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(DateTimeOffset.UnixEpoch));

        var result = await svc.CreateAsync("u1", new CreatePostRequest(
            [Guid.NewGuid()], "t", "b", null, null, null, null, null, null));

        Assert.Null(result);
        Assert.Empty(bus.Messages);
    }

    [Fact]
    public async Task Disabled_connector_is_not_targeted()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1", enabled: false);
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(DateTimeOffset.UnixEpoch));

        var result = await svc.CreateAsync("u1", new CreatePostRequest(
            [connectorId], "t", "b", null, null, null, null, null, null));

        Assert.Null(result);
    }

    [Fact]
    public async Task Future_schedule_publishes_with_delay()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1");
        var bus = new FakeBus();
        var now = DateTimeOffset.UnixEpoch;
        var svc = New(db, bus, new FixedClock(now));

        await svc.CreateAsync("u1", new CreatePostRequest(
            [connectorId], "t", "b", null, null, null, null, null, now.AddHours(2)));

        var published = Assert.Single(bus.Messages);
        Assert.NotNull(published.Delay);
        Assert.Equal(TimeSpan.FromHours(2), published.Delay!.Value);
    }

    [Fact]
    public async Task Create_resolves_a_connector_destination_selection_to_its_chat_id()
    {
        using var db = TestDbContext.Create();
        db.ServiceDefinitions.Add(new ServiceDefinition { Id = "Telegram", Name = "Telegram", Platform = "Telegram", Enabled = true });
        var connectorId = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = connectorId, UserId = "u1", ServiceDefinitionId = "Telegram", DisplayName = "Telegram", Enabled = true
        });
        var destinationId = Guid.NewGuid();
        db.ConnectorDestinations.Add(new ConnectorDestination
        {
            Id = destinationId, ConnectorId = connectorId, ExternalId = "-100111", Name = "Channel A"
        });
        await db.SaveChangesAsync();
        var bus = new FakeBus();
        var svc = new PostIntakeService(db, new FakeObjectStore(), bus, new FixedClock(DateTimeOffset.UnixEpoch),
            new FakeRegistry(new FakeMultiTargetConnector("Telegram")),
            Microsoft.Extensions.Options.Options.Create(new PipelineOptions()));

        var result = await svc.CreateAsync("u1", new CreatePostRequest(
            [destinationId], "Title", "Body", null, null, null, null, null, null));

        Assert.NotNull(result);
        var target = Assert.Single(db.PostTargets);
        Assert.Equal(connectorId, target.ConnectorId);
        Assert.Equal("-100111", target.TargetId);
        Assert.Equal("Channel A", target.TargetName);
    }

    [Fact]
    public async Task Create_resolves_mixed_connector_and_destination_selections_into_separate_targets()
    {
        using var db = TestDbContext.Create();
        var discordId = await SeedConnectorAsync(db, "u1");
        db.ServiceDefinitions.Add(new ServiceDefinition { Id = "Telegram", Name = "Telegram", Platform = "Telegram", Enabled = true });
        var telegramConnectorId = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = telegramConnectorId, UserId = "u1", ServiceDefinitionId = "Telegram", DisplayName = "Telegram", Enabled = true
        });
        var destinationId = Guid.NewGuid();
        db.ConnectorDestinations.Add(new ConnectorDestination
        {
            Id = destinationId, ConnectorId = telegramConnectorId, ExternalId = "-100111", Name = "Channel A"
        });
        await db.SaveChangesAsync();
        var bus = new FakeBus();
        var svc = new PostIntakeService(db, new FakeObjectStore(), bus, new FixedClock(DateTimeOffset.UnixEpoch),
            new FakeRegistry(new FakeConnector("DiscordWH"), new FakeMultiTargetConnector("Telegram")),
            Microsoft.Extensions.Options.Options.Create(new PipelineOptions()));

        var result = await svc.CreateAsync("u1", new CreatePostRequest(
            [discordId, destinationId], "Title", "Body", null, null, null, null, null, null));

        Assert.NotNull(result);
        Assert.Equal(2, db.PostTargets.Count());
        var discordTarget = db.PostTargets.Single(t => t.ConnectorId == discordId);
        Assert.Null(discordTarget.TargetId);
        var telegramTarget = db.PostTargets.Single(t => t.ConnectorId == telegramConnectorId);
        Assert.Equal("-100111", telegramTarget.TargetId);
    }
}

using Microsoft.Extensions.Options;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
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
        new(db, new FakeObjectStore(), bus, clock, Microsoft.Extensions.Options.Options.Create(new PipelineOptions()));

    [Fact]
    public async Task Create_persists_post_and_enqueues_generate_per_target()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedConnectorAsync(db, "u1");
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(DateTimeOffset.UnixEpoch));

        var result = await svc.CreateAsync("u1", new CreatePostRequest(
            [connectorId], "Title", "Body", "<p>Body</p>", ["tag"], null, null, null, null));

        Assert.NotNull(result);
        var post = Assert.Single(db.Posts);
        Assert.Single(db.PostTargets);
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
}

using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Application.Tests;

/// <summary>
/// Covers "post again" re-selection: GetContentAsync must resolve a delivered PostTarget's chat id
/// back to the ConnectorDestination the compose form originally selected, not just its connector.
/// </summary>
public class PostStatusServiceGetContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

    private static PostStatusService New(TestDbContext db) =>
        new(db, new FixedClock(Now), Microsoft.Extensions.Options.Options.Create(new RetentionOptions()));

    [Fact]
    public async Task Content_resolves_target_id_back_to_the_originally_selected_destination()
    {
        using var db = TestDbContext.Create();
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
        var post = new Post { Id = Guid.NewGuid(), UserId = "u1", Title = "t", RootStatus = PostRootStatus.Delivered, CreatedAt = Now, UpdatedAt = Now };
        post.Targets.Add(new PostTarget
        {
            Id = Guid.NewGuid(), ConnectorId = connectorId, Platform = "Telegram", TargetId = "-100111",
            Status = TargetStatus.Delivered, CreatedAt = Now
        });
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var content = await New(db).GetContentAsync("u1", post.Id);

        Assert.NotNull(content);
        Assert.Equal([destinationId], content!.ConnectorIds);
    }

    [Fact]
    public async Task Content_falls_back_to_the_connector_when_the_destination_is_no_longer_exposed()
    {
        using var db = TestDbContext.Create();
        var connectorId = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = connectorId, UserId = "u1", ServiceDefinitionId = "Telegram", DisplayName = "Telegram", Enabled = true
        });
        // No ConnectorDestination seeded — it has since been un-exposed.
        var post = new Post { Id = Guid.NewGuid(), UserId = "u1", Title = "t", RootStatus = PostRootStatus.Delivered, CreatedAt = Now, UpdatedAt = Now };
        post.Targets.Add(new PostTarget
        {
            Id = Guid.NewGuid(), ConnectorId = connectorId, Platform = "Telegram", TargetId = "-100111",
            Status = TargetStatus.Delivered, CreatedAt = Now
        });
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var content = await New(db).GetContentAsync("u1", post.Id);

        Assert.NotNull(content);
        Assert.Equal([connectorId], content!.ConnectorIds);
    }

    [Fact]
    public async Task Content_keys_target_options_by_the_resolved_destination_id()
    {
        using var db = TestDbContext.Create();
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
        var post = new Post { Id = Guid.NewGuid(), UserId = "u1", Title = "t", RootStatus = PostRootStatus.Delivered, CreatedAt = Now, UpdatedAt = Now };
        post.Targets.Add(new PostTarget
        {
            Id = Guid.NewGuid(), ConnectorId = connectorId, Platform = "Telegram", TargetId = "-100111",
            OptionsJson = """{"Silent":"true"}""",
            Status = TargetStatus.Delivered, CreatedAt = Now
        });
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var content = await New(db).GetContentAsync("u1", post.Id);

        Assert.NotNull(content);
        var options = Assert.Single(content!.TargetOptions);
        Assert.Equal(destinationId, options.Key);
        Assert.Equal("true", options.Value["Silent"]);
    }
}

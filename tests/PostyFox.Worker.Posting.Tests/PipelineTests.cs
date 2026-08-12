using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Application.Posting;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Worker.Posting.Tests.Support;
using Xunit;

namespace PostyFox.Worker.Posting.Tests;

public class PipelineTests
{
    private static async Task CreatePostAsync(PipelineHarness h, string userId, params Guid[] targets)
    {
        using var scope = h.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<PostIntakeService>();
        var result = await intake.CreateAsync(userId, new CreatePostRequest(
            targets, "Title", "Hello **world**", null, ["tag"], null, null, null, null));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Happy_path_generates_and_delivers()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: true);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "DiscordWH");

        await CreatePostAsync(h, "u1", cid);

        var target = await h.InScopeAsync(db => db.PostTargets.FirstAsync());
        Assert.Equal(TargetStatus.Delivered, target.Status);
        Assert.Equal("ext-1", target.ExternalId);
        Assert.NotNull(target.RenderedContentJson);

        var post = await h.InScopeAsync(db => db.Posts.FirstAsync());
        Assert.Equal(PostRootStatus.Delivered, post.RootStatus);
        Assert.Equal(1, connector.Calls);
    }

    [Fact]
    public async Task Failing_delivery_retries_to_limit_then_fails()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: false);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "DiscordWH");

        await CreatePostAsync(h, "u1", cid);

        var target = await h.InScopeAsync(db => db.PostTargets.FirstAsync());
        Assert.Equal(TargetStatus.Failed, target.Status);
        Assert.Equal(3, target.Attempts);        // MaxDeliveryAttempts default
        Assert.Equal(3, connector.Calls);
        var post = await h.InScopeAsync(db => db.Posts.FirstAsync());
        Assert.Equal(PostRootStatus.Failed, post.RootStatus);
    }

    [Fact]
    public async Task Missing_connector_marks_target_failed()
    {
        using var h = new PipelineHarness(); // no connectors registered
        var cid = await h.SeedConnectorAsync("u1", "Ghost");

        await CreatePostAsync(h, "u1", cid);

        var target = await h.InScopeAsync(db => db.PostTargets.FirstAsync());
        Assert.Equal(TargetStatus.Failed, target.Status);
        Assert.Contains("No connector", target.Error);
    }

    [Fact]
    public async Task Mixed_outcomes_produce_partial_failure()
    {
        using var h = new PipelineHarness(
            new ProgrammableConnector("DiscordWH", succeed: true),
            new ProgrammableConnector("Flaky", succeed: false));
        var good = await h.SeedConnectorAsync("u1", "DiscordWH");
        var bad = await h.SeedConnectorAsync("u1", "Flaky");

        await CreatePostAsync(h, "u1", good, bad);

        var targets = await h.InScopeAsync(db => db.PostTargets.ToListAsync());
        Assert.Equal(2, targets.Count);
        Assert.Contains(targets, t => t.Status == TargetStatus.Delivered);
        Assert.Contains(targets, t => t.Status == TargetStatus.Failed);

        var post = await h.InScopeAsync(db => db.Posts.FirstAsync());
        Assert.Equal(PostRootStatus.PartiallyFailed, post.RootStatus);
    }

    [Fact]
    public async Task Cancelled_target_is_skipped_when_its_queued_message_fires()
    {
        // A post cancelled while its generate/deliver message sat on the delayed queue: when the
        // message finally fires, the handlers must no-op rather than resurrect the delivery.
        var connector = new ProgrammableConnector("DiscordWH", succeed: true);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "DiscordWH");

        Guid postId = Guid.NewGuid(), targetId = Guid.NewGuid();
        await h.InScopeAsync(async db =>
        {
            db.Posts.Add(new Post
            {
                Id = postId, UserId = "u1", Title = "t", RootStatus = PostRootStatus.Cancelled,
                Targets =
                {
                    new PostTarget
                    {
                        Id = targetId, PostId = postId, ConnectorId = cid, Platform = "DiscordWH",
                        Status = TargetStatus.Cancelled,
                        // Rendered already, so a non-skipping deliver handler *would* call the connector.
                        RenderedContentJson = "{\"Title\":null,\"Body\":\"x\",\"Media\":[]}"
                    }
                }
            });
            return await db.SaveChangesAsync();
        });

        using (var scope = h.Services.CreateScope())
        {
            var generate = scope.ServiceProvider.GetRequiredService<IMessageHandler<GenerateTargetCommand>>();
            await generate.HandleAsync(new GenerateTargetCommand { PostId = postId, TargetId = targetId }, default);
            var deliver = scope.ServiceProvider.GetRequiredService<IMessageHandler<DeliverTargetCommand>>();
            await deliver.HandleAsync(new DeliverTargetCommand { PostId = postId, TargetId = targetId }, default);
        }

        Assert.Equal(0, connector.Calls);
        var target = await h.InScopeAsync(db => db.PostTargets.FirstAsync());
        Assert.Equal(TargetStatus.Cancelled, target.Status);
    }

    [Fact]
    public async Task Media_is_fetched_from_object_store_and_passed_to_connector()
    {
        var connector = new ProgrammableConnector("DiscordWH", succeed: true);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "DiscordWH");

        using (var scope = h.Services.CreateScope())
        {
            var intake = scope.ServiceProvider.GetRequiredService<PostIntakeService>();
            await intake.CreateAsync("u1", new CreatePostRequest(
                [cid], "T", "body", null, null,
                [new MediaRef("post", "k1", "image/png")], null, null, null));
        }

        Assert.Equal(1, connector.LastMediaCount);
    }

    // Per-submission platform choices (FurAffinity's category/species/…) travel on the post, not the
    // connector, but reach the connector through the same ConfigJson it already reads.
    private const string ChoiceSchema = """
        { "Category": { "label": "Category", "options": [
            { "value": "1", "label": "All" }, { "value": "13", "label": "Story" } ] } }
        """;

    [Fact]
    public async Task Post_options_reach_the_connector_and_supersede_stale_account_config()
    {
        var connector = new ProgrammableConnector("Gallery", succeed: true, postOptionsSchema: ChoiceSchema);
        using var h = new PipelineHarness(connector);
        // Category left behind by an older release, when it was a connector setting.
        var cid = await h.SeedConnectorAsync(
            "u1", "Gallery", configJson: """{"Category":"1","Account":"keep-me"}""");

        using (var scope = h.Services.CreateScope())
        {
            var intake = scope.ServiceProvider.GetRequiredService<PostIntakeService>();
            await intake.CreateAsync("u1", new CreatePostRequest(
                [cid], "T", "body", null, null, null, null, null, null, null,
                new Dictionary<Guid, IReadOnlyDictionary<string, string>>
                {
                    [cid] = new Dictionary<string, string> { ["Category"] = "13" }
                }));
        }

        var config = JsonDocument.Parse(connector.LastConfigJson!).RootElement;
        Assert.Equal("13", config.GetProperty("Category").GetString());
        Assert.Equal("keep-me", config.GetProperty("Account").GetString()); // genuine account config survives

        var target = await h.InScopeAsync(db => db.PostTargets.FirstAsync());
        Assert.Equal("""{"Category":"13"}""", target.OptionsJson);
    }

    [Fact]
    public async Task A_stale_account_value_is_dropped_when_the_author_chose_nothing()
    {
        var connector = new ProgrammableConnector("Gallery", succeed: true, postOptionsSchema: ChoiceSchema);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync(
            "u1", "Gallery", configJson: """{"Category":"1","Account":"keep-me"}""");

        await CreatePostAsync(h, "u1", cid); // no target options at all

        var config = JsonDocument.Parse(connector.LastConfigJson!).RootElement;
        // Not "1" — the field moved to the post, so the platform's own default applies instead.
        Assert.False(config.TryGetProperty("Category", out _));
        Assert.Equal("keep-me", config.GetProperty("Account").GetString());
    }

    [Fact]
    public async Task An_invalid_post_option_is_rejected_at_intake()
    {
        var connector = new ProgrammableConnector("Gallery", succeed: true, postOptionsSchema: ChoiceSchema);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "Gallery");

        using var scope = h.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<PostIntakeService>();
        var ex = await Assert.ThrowsAsync<ConnectorValidationException>(() => intake.CreateAsync(
            "u1", new CreatePostRequest(
                [cid], "T", "body", null, null, null, null, null, null, null,
                new Dictionary<Guid, IReadOnlyDictionary<string, string>>
                {
                    [cid] = new Dictionary<string, string> { ["Category"] = "999" }
                })));

        Assert.Contains("Category is not one of the available choices.", ex.Message);
        Assert.Equal(0, connector.Calls);
        Assert.Empty(await h.InScopeAsync(db => db.Posts.ToListAsync()));
    }

    [Fact]
    public async Task Posting_to_a_connector_destination_resolves_target_id_and_delivers_once_per_destination()
    {
        var connector = new ProgrammableConnector("Telegram", succeed: true, supportsMultipleTargets: true);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "Telegram");
        var chatA = await h.SeedDestinationAsync(cid, "-100111", "Channel A");
        var chatB = await h.SeedDestinationAsync(cid, "-100222", "Channel B");

        await CreatePostAsync(h, "u1", chatA, chatB);

        var targets = await h.InScopeAsync(db => db.PostTargets.OrderBy(t => t.TargetId).ToListAsync());
        Assert.Equal(2, targets.Count);
        Assert.All(targets, t => Assert.Equal(TargetStatus.Delivered, t.Status));
        Assert.Equal(["-100111", "-100222"], targets.Select(t => t.TargetId).OrderBy(x => x));
        Assert.Equal(2, connector.Calls);
    }

    [Fact]
    public async Task Delivering_to_a_destination_passes_its_chat_id_into_the_connector_context()
    {
        var connector = new ProgrammableConnector("Telegram", succeed: true, supportsMultipleTargets: true);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "Telegram");
        var chatA = await h.SeedDestinationAsync(cid, "-100111", "Channel A");

        await CreatePostAsync(h, "u1", chatA);

        Assert.Equal("-100111", connector.LastTargetId);
    }
}


using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Application.Dtos;
using PostyFox.Application.Posting;
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
}

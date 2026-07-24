using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Application.Tests;

public class PostLifecycleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

    private static PostLifecycleService New(TestDbContext db, FakeObjectStore store) =>
        new(db, TestFactories.PayloadCleaner(store), new FixedClock(Now));

    private static async Task<Post> SeedAsync(
        TestDbContext db, string userId = "u1", params (string platform, TargetStatus st)[] targets)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(), UserId = userId, Title = "t",
            RootStatus = PostRootStatus.Queued, CreatedAt = Now, UpdatedAt = Now,
            MediaManifestJson = "[]"
        };
        foreach (var (platform, st) in targets)
            post.Targets.Add(new PostTarget
            {
                Id = Guid.NewGuid(), ConnectorId = Guid.NewGuid(), Platform = platform, Status = st, CreatedAt = Now
            });
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    // ----- cancel -------------------------------------------------------------

    [Fact]
    public async Task Cancel_moves_pending_targets_to_cancelled()
    {
        using var db = TestDbContext.Create();
        var post = await SeedAsync(db, "u1", ("DiscordWH", TargetStatus.Queued), ("BlueSky", TargetStatus.Ready));

        var outcome = await New(db, new FakeObjectStore()).CancelAsync("u1", post.Id);

        Assert.Equal(CancelOutcome.Cancelled, outcome);
        var reloaded = db.Posts.Single();
        Assert.All(db.PostTargets, t => Assert.Equal(TargetStatus.Cancelled, t.Status));
        Assert.Equal(PostRootStatus.Cancelled, reloaded.RootStatus);
    }

    [Fact]
    public async Task Cancel_keeps_delivered_and_inflight_targets()
    {
        using var db = TestDbContext.Create();
        var post = await SeedAsync(db, "u1",
            ("DiscordWH", TargetStatus.Delivered),
            ("BlueSky", TargetStatus.Delivering),
            ("Mastodon", TargetStatus.Queued));

        var outcome = await New(db, new FakeObjectStore()).CancelAsync("u1", post.Id);

        Assert.Equal(CancelOutcome.Cancelled, outcome);
        Assert.Equal(TargetStatus.Delivered, db.PostTargets.Single(t => t.Platform == "DiscordWH").Status);
        Assert.Equal(TargetStatus.Delivering, db.PostTargets.Single(t => t.Platform == "BlueSky").Status);
        Assert.Equal(TargetStatus.Cancelled, db.PostTargets.Single(t => t.Platform == "Mastodon").Status);
    }

    [Fact]
    public async Task Cancel_when_nothing_pending_returns_nothing_to_cancel()
    {
        using var db = TestDbContext.Create();
        var post = await SeedAsync(db, "u1", ("DiscordWH", TargetStatus.Delivered));

        var outcome = await New(db, new FakeObjectStore()).CancelAsync("u1", post.Id);

        Assert.Equal(CancelOutcome.NothingToCancel, outcome);
    }

    [Fact]
    public async Task Cancel_other_users_post_is_not_found()
    {
        using var db = TestDbContext.Create();
        var post = await SeedAsync(db, "u1", ("DiscordWH", TargetStatus.Queued));

        var outcome = await New(db, new FakeObjectStore()).CancelAsync("someone-else", post.Id);

        Assert.Equal(CancelOutcome.NotFound, outcome);
        Assert.Equal(TargetStatus.Queued, db.PostTargets.Single().Status); // untouched
    }

    // ----- delete -------------------------------------------------------------

    [Fact]
    public async Task Delete_removes_post_targets_and_payload()
    {
        using var db = TestDbContext.Create();
        var store = new FakeObjectStore();
        var post = await SeedAsync(db, "u1", ("DiscordWH", TargetStatus.Cancelled));
        store.Text[$"post/{post.Id}/title"] = "t";
        store.Text[$"post/{post.Id}/description"] = "d";

        var deleted = await New(db, store).DeleteAsync("u1", post.Id);

        Assert.True(deleted);
        Assert.Empty(db.Posts);
        Assert.Empty(db.PostTargets); // cascade
        Assert.DoesNotContain($"post/{post.Id}/title", store.Text.Keys);
        Assert.DoesNotContain($"post/{post.Id}/description", store.Text.Keys);
    }

    [Fact]
    public async Task Delete_other_users_post_is_rejected()
    {
        using var db = TestDbContext.Create();
        var post = await SeedAsync(db, "u1", ("DiscordWH", TargetStatus.Delivered));

        var deleted = await New(db, new FakeObjectStore()).DeleteAsync("someone-else", post.Id);

        Assert.False(deleted);
        Assert.Single(db.Posts);
    }
}

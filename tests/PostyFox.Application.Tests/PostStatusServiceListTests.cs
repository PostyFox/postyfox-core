using Microsoft.Extensions.Options;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Application.Tests;

public class PostStatusServiceListTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

    private static PostStatusService New(TestDbContext db, int retentionDays = 30) =>
        new(db, new FixedClock(Now), Microsoft.Extensions.Options.Options.Create(new RetentionOptions { PostRetentionDays = retentionDays }));

    private static async Task SeedAsync(
        TestDbContext db, string userId, DateTimeOffset createdAt, PostRootStatus status, params (string platform, TargetStatus st)[] targets)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(), UserId = userId, Title = "t", RootStatus = status,
            CreatedAt = createdAt, UpdatedAt = createdAt
        };
        foreach (var (platform, st) in targets)
            post.Targets.Add(new PostTarget { Id = Guid.NewGuid(), Platform = platform, Status = st, CreatedAt = createdAt });
        db.Posts.Add(post);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task List_returns_only_own_posts_newest_first()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, "u1", Now.AddDays(-3), PostRootStatus.Delivered, ("DiscordWH", TargetStatus.Delivered));
        await SeedAsync(db, "u1", Now.AddDays(-1), PostRootStatus.Delivered, ("DiscordWH", TargetStatus.Delivered));
        await SeedAsync(db, "u2", Now.AddDays(-1), PostRootStatus.Delivered, ("DiscordWH", TargetStatus.Delivered));

        var list = await New(db).ListAsync("u1", activeOnly: false, limit: 50);

        Assert.Equal(2, list.Count);
        Assert.True(list[0].CreatedAt > list[1].CreatedAt);
    }

    [Fact]
    public async Task List_active_only_excludes_terminal_posts()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, "u1", Now.AddDays(-1), PostRootStatus.Delivering, ("DiscordWH", TargetStatus.Delivering));
        await SeedAsync(db, "u1", Now.AddDays(-1), PostRootStatus.Delivered, ("DiscordWH", TargetStatus.Delivered));
        await SeedAsync(db, "u1", Now.AddDays(-1), PostRootStatus.Failed, ("DiscordWH", TargetStatus.Failed));

        var active = await New(db).ListAsync("u1", activeOnly: true, limit: 50);

        Assert.Single(active);
        Assert.Equal(PostRootStatus.Delivering, active[0].RootStatus);
    }

    [Fact]
    public async Task List_excludes_posts_older_than_retention_window()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, "u1", Now.AddDays(-31), PostRootStatus.Delivered, ("DiscordWH", TargetStatus.Delivered));
        await SeedAsync(db, "u1", Now.AddDays(-5), PostRootStatus.Delivered, ("DiscordWH", TargetStatus.Delivered));

        var list = await New(db).ListAsync("u1", activeOnly: false, limit: 50);

        Assert.Single(list);
    }

    [Fact]
    public async Task List_summarises_platforms_and_target_counts()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, "u1", Now.AddDays(-1), PostRootStatus.PartiallyFailed,
            ("DiscordWH", TargetStatus.Delivered), ("BlueSky", TargetStatus.Failed));

        var row = Assert.Single(await New(db).ListAsync("u1", activeOnly: false, limit: 50));

        Assert.Equal(2, row.TargetCount);
        Assert.Equal(1, row.DeliveredCount);
        Assert.Equal(1, row.FailedCount);
        Assert.Equal(["BlueSky", "DiscordWH"], row.Platforms);
    }

    [Fact]
    public async Task List_clamps_limit()
    {
        using var db = TestDbContext.Create();
        for (var i = 0; i < 3; i++)
            await SeedAsync(db, "u1", Now.AddDays(-i - 1), PostRootStatus.Delivered, ("DiscordWH", TargetStatus.Delivered));

        var list = await New(db).ListAsync("u1", activeOnly: false, limit: 2);

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task List_includes_pending_automations_with_due_or_estimated_times()
    {
        using var db = TestDbContext.Create();
        var postAt = Now.AddDays(2);
        var dueAt = Now.AddHours(5);
        var post = new Post
        {
            Id = Guid.NewGuid(), UserId = "u1", Title = "t", RootStatus = PostRootStatus.PartiallyFailed,
            CreatedAt = Now.AddHours(-1), UpdatedAt = Now, PostAt = postAt
        };
        var delivered = new PostTarget { Id = Guid.NewGuid(), Platform = "BlueSky", Status = TargetStatus.Delivered, CreatedAt = Now };
        delivered.Automations.Add(new PostTargetAutomation
            { Id = Guid.NewGuid(), Action = AutomationAction.Delete, DelayHours = 24, DueAt = dueAt, CreatedAt = Now });
        delivered.Automations.Add(new PostTargetAutomation
            { Id = Guid.NewGuid(), Action = AutomationAction.Repost, DelayHours = 1, Status = AutomationStatus.Done, CreatedAt = Now });
        var queued = new PostTarget { Id = Guid.NewGuid(), Platform = "DiscordWH", Status = TargetStatus.Queued, CreatedAt = Now };
        queued.Automations.Add(new PostTargetAutomation
            { Id = Guid.NewGuid(), Action = AutomationAction.Repost, DelayHours = 3, CreatedAt = Now });
        post.Targets.Add(delivered);
        post.Targets.Add(queued);
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var row = Assert.Single(await New(db).ListAsync("u1", activeOnly: false, limit: 50));

        Assert.Equal(2, row.PendingAutomationCount);
        Assert.NotNull(row.PendingAutomations);
        Assert.Collection(row.PendingAutomations,
            a =>
            {
                Assert.Equal(AutomationAction.Delete, a.Action);
                Assert.Equal("BlueSky", a.Platform);
                Assert.Equal(dueAt, a.DueAt);
                Assert.False(a.Estimated);
            },
            a =>
            {
                Assert.Equal(AutomationAction.Repost, a.Action);
                Assert.Equal("DiscordWH", a.Platform);
                Assert.Equal(postAt.AddHours(3), a.DueAt);
                Assert.True(a.Estimated);
            });
    }
}

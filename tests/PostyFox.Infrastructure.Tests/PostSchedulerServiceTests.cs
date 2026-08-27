using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using PostyFox.Infrastructure.Tests.Support;

namespace PostyFox.Infrastructure.Tests;

/// <summary>
/// Exercises the real EF Core provider (SQLite) rather than the in-memory provider, because
/// <see cref="PostSchedulerService"/> relies on <c>ExecuteUpdateAsync</c> for its atomic claim, which
/// the in-memory provider doesn't support.
/// </summary>
public class PostSchedulerServiceTests
{
    private static PostSchedulerService New(SqliteDb db, FakeBus bus, FixedClock clock, PipelineOptions? options = null) =>
        new(db.Context, bus, clock, Microsoft.Extensions.Options.Options.Create(options ?? new PipelineOptions()),
            NullLogger<PostSchedulerService>.Instance);

    private static (Post post, PostTarget target) Seed(SqliteDb db, DateTimeOffset? postAt, TargetStatus status = TargetStatus.Queued)
    {
        var post = new Post { Id = Guid.NewGuid(), UserId = "u1", PostAt = postAt, RootStatus = PostRootStatus.Queued };
        var target = new PostTarget { Id = Guid.NewGuid(), PostId = post.Id, Status = status };
        db.Context.Posts.Add(post);
        db.Context.PostTargets.Add(target);
        db.Context.SaveChanges();
        return (post, target);
    }

    [Fact]
    public async Task Enqueues_a_due_scheduled_target()
    {
        using var db = new SqliteDb();
        var now = DateTimeOffset.UnixEpoch.AddDays(1);
        var (post, target) = Seed(db, now.AddMinutes(-1));
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(now));

        var claimed = await svc.EnqueueDueAsync();

        Assert.Equal(1, claimed);
        var published = Assert.Single(bus.Of<GenerateTargetCommand>());
        Assert.Equal(target.Id, published.TargetId);
        Assert.Equal(post.Id, published.PostId);

        var reloaded = await db.Context.PostTargets.AsNoTracking().FirstAsync(t => t.Id == target.Id);
        Assert.NotNull(reloaded.GenerationEnqueuedAt);
    }

    [Fact]
    public async Task Does_not_enqueue_a_target_scheduled_for_the_future()
    {
        using var db = new SqliteDb();
        var now = DateTimeOffset.UnixEpoch;
        Seed(db, now.AddHours(1));
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(now));

        var claimed = await svc.EnqueueDueAsync();

        Assert.Equal(0, claimed);
        Assert.Empty(bus.Messages);
    }

    [Fact]
    public async Task Ignores_targets_with_no_schedule()
    {
        using var db = new SqliteDb();
        var now = DateTimeOffset.UnixEpoch;
        Seed(db, postAt: null);
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(now));

        var claimed = await svc.EnqueueDueAsync();

        Assert.Equal(0, claimed);
        Assert.Empty(bus.Messages);
    }

    [Fact]
    public async Task Does_not_reclaim_an_already_enqueued_target()
    {
        using var db = new SqliteDb();
        var now = DateTimeOffset.UnixEpoch.AddDays(1);
        var (_, target) = Seed(db, now.AddMinutes(-1));
        target.GenerationEnqueuedAt = now.AddSeconds(-5);
        await db.Context.SaveChangesAsync();
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(now));

        var claimed = await svc.EnqueueDueAsync();

        Assert.Equal(0, claimed);
        Assert.Empty(bus.Messages);
    }

    [Fact]
    public async Task Ignores_cancelled_targets()
    {
        using var db = new SqliteDb();
        var now = DateTimeOffset.UnixEpoch.AddDays(1);
        Seed(db, now.AddMinutes(-1), TargetStatus.Cancelled);
        var bus = new FakeBus();
        var svc = New(db, bus, new FixedClock(now));

        var claimed = await svc.EnqueueDueAsync();

        Assert.Equal(0, claimed);
        Assert.Empty(bus.Messages);
    }
}

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
/// <see cref="PostTargetAutomationService.EnqueueDueAsync"/> relies on <c>ExecuteUpdateAsync</c> for
/// its atomic claim, which the in-memory provider doesn't support (see <see cref="PostSchedulerServiceTests"/>).
/// </summary>
public class PostTargetAutomationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    private static PostTargetAutomationService New(SqliteDb db, FakeBus bus, FixedClock clock, int batchSize = 200) =>
        new(db.Context, bus, clock, Microsoft.Extensions.Options.Options.Create(new PipelineOptions { AutomationBatchSize = batchSize }),
            NullLogger<PostTargetAutomationService>.Instance);

    private static (Post post, PostTarget target, PostTargetAutomation automation) Seed(
        SqliteDb db, string userId, DateTimeOffset? dueAt, AutomationStatus status = AutomationStatus.Pending,
        AutomationAction action = AutomationAction.Delete)
    {
        var post = new Post { Id = Guid.NewGuid(), UserId = userId, RootStatus = PostRootStatus.Delivered };
        var target = new PostTarget
        {
            Id = Guid.NewGuid(), PostId = post.Id, Platform = "BlueSky", Status = TargetStatus.Delivered, ExternalId = "ext-1"
        };
        var automation = new PostTargetAutomation
        {
            Id = Guid.NewGuid(), PostTargetId = target.Id, Action = action, DelayHours = 6, DueAt = dueAt, Status = status
        };
        db.Context.Posts.Add(post);
        db.Context.PostTargets.Add(target);
        db.Context.PostTargetAutomations.Add(automation);
        db.Context.SaveChanges();
        return (post, target, automation);
    }

    // ----- EnqueueDueAsync ------------------------------------------------------

    [Fact]
    public async Task EnqueueDueAsync_claims_and_publishes_a_due_rule()
    {
        using var db = new SqliteDb();
        var (post, target, automation) = Seed(db, "u1", Now.AddHours(-1));
        var bus = new FakeBus();

        var claimed = await New(db, bus, new FixedClock(Now)).EnqueueDueAsync();

        Assert.Equal(1, claimed);
        var cmd = Assert.Single(bus.Of<ExecuteAutomationCommand>());
        Assert.Equal(post.Id, cmd.PostId);
        Assert.Equal(target.Id, cmd.TargetId);
        Assert.Equal(automation.Id, cmd.AutomationId);

        var reloaded = await db.Context.PostTargetAutomations.AsNoTracking().FirstAsync(a => a.Id == automation.Id);
        Assert.NotNull(reloaded.EnqueuedAt);
    }

    [Fact]
    public async Task EnqueueDueAsync_ignores_a_rule_not_yet_due()
    {
        using var db = new SqliteDb();
        Seed(db, "u1", Now.AddHours(1));
        var bus = new FakeBus();

        var claimed = await New(db, bus, new FixedClock(Now)).EnqueueDueAsync();

        Assert.Equal(0, claimed);
        Assert.Empty(bus.Messages);
    }

    [Fact]
    public async Task EnqueueDueAsync_ignores_a_rule_with_no_due_at_yet()
    {
        using var db = new SqliteDb();
        Seed(db, "u1", dueAt: null);
        var bus = new FakeBus();

        Assert.Equal(0, await New(db, bus, new FixedClock(Now)).EnqueueDueAsync());
        Assert.Empty(bus.Messages);
    }

    [Fact]
    public async Task EnqueueDueAsync_ignores_an_already_claimed_rule()
    {
        using var db = new SqliteDb();
        var (_, _, automation) = Seed(db, "u1", Now.AddHours(-1));
        automation.EnqueuedAt = Now.AddMinutes(-1);
        await db.Context.SaveChangesAsync();
        var bus = new FakeBus();

        Assert.Equal(0, await New(db, bus, new FixedClock(Now)).EnqueueDueAsync());
        Assert.Empty(bus.Messages);
    }

    [Fact]
    public async Task EnqueueDueAsync_ignores_a_non_pending_rule()
    {
        using var db = new SqliteDb();
        Seed(db, "u1", Now.AddHours(-1), status: AutomationStatus.Done);
        var bus = new FakeBus();

        Assert.Equal(0, await New(db, bus, new FixedClock(Now)).EnqueueDueAsync());
    }

    [Fact]
    public async Task EnqueueDueAsync_respects_the_batch_size()
    {
        using var db = new SqliteDb();
        for (var i = 0; i < 3; i++) Seed(db, "u1", Now.AddHours(-1));
        var bus = new FakeBus();

        var claimed = await New(db, bus, new FixedClock(Now), batchSize: 2).EnqueueDueAsync();

        Assert.Equal(2, claimed);
    }

    // ----- CancelAsync ------------------------------------------------------

    [Fact]
    public async Task CancelAsync_cancels_a_pending_rule_the_user_owns()
    {
        using var db = new SqliteDb();
        var (_, _, automation) = Seed(db, "u1", Now.AddHours(1));

        var outcome = await New(db, new FakeBus(), new FixedClock(Now)).CancelAsync("u1", automation.Id);

        Assert.Equal(CancelAutomationOutcome.Cancelled, outcome);
        var reloaded = await db.Context.PostTargetAutomations.AsNoTracking().FirstAsync(a => a.Id == automation.Id);
        Assert.Equal(AutomationStatus.Cancelled, reloaded.Status);
    }

    [Fact]
    public async Task CancelAsync_returns_not_found_for_another_users_rule()
    {
        using var db = new SqliteDb();
        var (_, _, automation) = Seed(db, "u1", Now.AddHours(1));

        var outcome = await New(db, new FakeBus(), new FixedClock(Now)).CancelAsync("someone-else", automation.Id);

        Assert.Equal(CancelAutomationOutcome.NotFound, outcome);
        var reloaded = await db.Context.PostTargetAutomations.AsNoTracking().FirstAsync(a => a.Id == automation.Id);
        Assert.Equal(AutomationStatus.Pending, reloaded.Status);
    }

    [Fact]
    public async Task CancelAsync_returns_already_done_for_a_rule_that_already_ran()
    {
        using var db = new SqliteDb();
        var (_, _, automation) = Seed(db, "u1", Now.AddHours(-1), status: AutomationStatus.Done);

        var outcome = await New(db, new FakeBus(), new FixedClock(Now)).CancelAsync("u1", automation.Id);

        Assert.Equal(CancelAutomationOutcome.AlreadyDone, outcome);
    }

    [Fact]
    public async Task CancelAsync_returns_not_found_for_an_unknown_id()
    {
        using var db = new SqliteDb();
        var outcome = await New(db, new FakeBus(), new FixedClock(Now)).CancelAsync("u1", Guid.NewGuid());
        Assert.Equal(CancelAutomationOutcome.NotFound, outcome);
    }
}

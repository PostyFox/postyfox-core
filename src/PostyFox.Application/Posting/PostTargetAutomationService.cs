using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

/// <summary>Outcome of a manual automation-cancel request.</summary>
public enum CancelAutomationOutcome
{
    /// <summary>No such automation for this user (unknown id, or it belongs to someone else's post).</summary>
    NotFound,
    /// <summary>The rule already ran (or was already cancelled): nothing left to cancel.</summary>
    AlreadyDone,
    Cancelled
}

/// <summary>
/// Schedules and cancels post-delivery automation rules (issue #323): reposting or deleting a
/// delivered target after an author-chosen delay. <see cref="EnqueueDueAsync"/> is polled by
/// <c>PostAutomationSweeper</c> exactly as <see cref="PostSchedulerService.EnqueueDueAsync"/> is
/// polled for scheduled posts; actually running a rule is <c>AutomationExecutionHandler</c>'s job.
/// </summary>
public sealed class PostTargetAutomationService(
    IAppDbContext db,
    IMessageBus bus,
    IClock clock,
    IOptions<PipelineOptions> options,
    ILogger<PostTargetAutomationService> logger)
{
    private readonly PipelineOptions _options = options.Value;

    /// <summary>
    /// Claims and enqueues one batch of due automation rules. Returns the number claimed (callers
    /// should keep calling while this equals <see cref="PipelineOptions.AutomationBatchSize"/>, so a
    /// large backlog drains in one pass rather than waiting for the next poll tick).
    /// </summary>
    public async Task<int> EnqueueDueAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        // Same client-side due-check as PostSchedulerService, for the same reason: EF's SQLite
        // provider can't translate DateTimeOffset comparisons, so the candidate set (already small:
        // only pending, delivered, not-yet-claimed rules) is pulled into memory first.
        var candidates = await db.PostTargetAutomations
            .Where(a => a.Status == AutomationStatus.Pending && a.DueAt != null && a.EnqueuedAt == null)
            .Select(a => new { a.Id, DueAt = a.DueAt!.Value })
            .ToListAsync(ct);

        var dueIds = candidates
            .Where(a => a.DueAt <= now)
            .OrderBy(a => a.DueAt)
            .Take(_options.AutomationBatchSize)
            .Select(a => a.Id)
            .ToList();

        var claimed = 0;
        foreach (var id in dueIds)
        {
            // Atomic conditional claim, same trick as PostSchedulerService: only publishes if this
            // pass wins the race to set EnqueuedAt.
            var rows = await db.PostTargetAutomations
                .Where(a => a.Id == id && a.EnqueuedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.EnqueuedAt, now), ct);
            if (rows == 0) continue;

            var automation = await db.PostTargetAutomations.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, ct);
            if (automation is null) continue;

            var target = await db.PostTargets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == automation.PostTargetId, ct);
            if (target is null) continue;

            await bus.PublishAsync(new ExecuteAutomationCommand
            {
                PostId = target.PostId,
                TargetId = target.Id,
                AutomationId = automation.Id
            }, ct: ct);
            claimed++;
        }

        if (claimed > 0)
            logger.LogInformation("Post automation enqueued {Count} due rule(s).", claimed);

        return claimed;
    }

    /// <summary>
    /// Cancels a not-yet-executed automation rule belonging to the user. A rule that has already run
    /// (or was already cancelled, e.g. because its target was cancelled first) comes back as
    /// <see cref="CancelAutomationOutcome.AlreadyDone"/> rather than an error.
    /// </summary>
    public async Task<CancelAutomationOutcome> CancelAsync(string userId, Guid automationId, CancellationToken ct = default)
    {
        var automation = await db.PostTargetAutomations
            .Include(a => a.PostTarget!.Post)
            .FirstOrDefaultAsync(a => a.Id == automationId && a.PostTarget!.Post!.UserId == userId, ct);
        if (automation is null) return CancelAutomationOutcome.NotFound;
        if (automation.Status != AutomationStatus.Pending) return CancelAutomationOutcome.AlreadyDone;

        automation.Status = AutomationStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        return CancelAutomationOutcome.Cancelled;
    }
}

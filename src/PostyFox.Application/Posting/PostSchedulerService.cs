using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

/// <summary>
/// Publishes <see cref="GenerateTargetCommand"/> for targets whose post's <see cref="Domain.Entities.Post.PostAt"/>
/// has come due. Scheduling delay lives entirely here (backed by the already-persisted <c>PostAt</c>
/// column) rather than on the message bus: <see cref="PostIntakeService"/> only publishes immediately
/// for un-scheduled posts, and leaves scheduled ones for this poller to pick up when due. This avoids
/// RabbitMQ's per-message-TTL "delay queue" head-of-line-blocking problem for scheduling, where an
/// arbitrary/wide-ranging delay published out of due-order would sit behind an earlier, longer one.
/// </summary>
public sealed class PostSchedulerService(
    IAppDbContext db,
    IMessageBus bus,
    IClock clock,
    IOptions<PipelineOptions> options,
    ILogger<PostSchedulerService> logger)
{
    private readonly PipelineOptions _options = options.Value;

    /// <summary>
    /// Claims and enqueues one batch of due targets. Returns the number claimed (callers should keep
    /// calling while this equals <see cref="PipelineOptions.SchedulerBatchSize"/>, so a large backlog
    /// drains in one pass rather than waiting for the next poll tick).
    /// </summary>
    public async Task<int> EnqueueDueAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        // Candidates: still waiting on generation and not already claimed by a previous (or
        // concurrent, on another replica) pass — both translate fine on every provider. The PostAt
        // due-check is intentionally done client-side below: EF Core's SQLite provider can't
        // translate DateTimeOffset comparisons (Npgsql, used in production, handles it natively), and
        // this way the same code path is exercisable against either. The candidate set here is
        // already small (only ever-queued, not-yet-claimed targets), so pulling it into memory first
        // is cheap.
        var candidates = await db.PostTargets
            .Where(t => t.Status == TargetStatus.Queued && t.GenerationEnqueuedAt == null && t.Post!.PostAt != null)
            .Select(t => new { t.Id, PostAt = t.Post!.PostAt!.Value })
            .ToListAsync(ct);

        var candidateIds = candidates
            .Where(t => t.PostAt <= now)
            .OrderBy(t => t.PostAt)
            .Take(_options.SchedulerBatchSize)
            .Select(t => t.Id)
            .ToList();

        var claimed = 0;
        foreach (var targetId in candidateIds)
        {
            // Atomic conditional claim: only publishes if this pass is the one that actually wins the
            // race to set GenerationEnqueuedAt (guards against two scheduler instances/replicas, or two
            // overlapping ticks, both selecting the same row before either commits).
            var rows = await db.PostTargets
                .Where(t => t.Id == targetId && t.GenerationEnqueuedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.GenerationEnqueuedAt, now), ct);
            if (rows == 0) continue;

            var target = await db.PostTargets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == targetId, ct);
            if (target is null) continue;

            await bus.PublishAsync(new GenerateTargetCommand { PostId = target.PostId, TargetId = target.Id }, ct: ct);
            claimed++;
        }

        if (claimed > 0)
            logger.LogInformation("Post scheduler enqueued {Count} due target(s).", claimed);

        return claimed;
    }
}

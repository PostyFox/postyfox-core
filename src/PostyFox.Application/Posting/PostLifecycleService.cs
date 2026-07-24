using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

/// <summary>Outcome of a cancel request.</summary>
public enum CancelOutcome
{
    /// <summary>No such post for this user.</summary>
    NotFound,
    /// <summary>Post exists but has nothing left to cancel (already delivered/failed/cancelled).</summary>
    NothingToCancel,
    /// <summary>One or more targets were moved to Cancelled.</summary>
    Cancelled
}

/// <summary>
/// User-driven lifecycle actions on a post: cancelling the parts that haven't gone out yet, and
/// hard-deleting a post entirely. Both are owner-scoped (a userId that doesn't own the post sees
/// the same result as a missing post).
/// </summary>
public sealed class PostLifecycleService(IAppDbContext db, PostPayloadCleaner payloadCleaner, IClock clock)
{
    /// <summary>Target states that haven't been handed to the platform yet, so are safe to cancel.</summary>
    private static readonly TargetStatus[] Cancellable =
        [TargetStatus.Queued, TargetStatus.Generating, TargetStatus.Ready];

    /// <summary>
    /// Cancels every not-yet-delivered target (Queued/Generating/Ready). Already-delivered or
    /// in-flight (Delivering) targets are left alone, so a partially-sent post keeps what went out.
    /// The delayed queue message for a cancelled target no-ops when it fires (handlers skip Cancelled).
    /// </summary>
    public async Task<CancelOutcome> CancelAsync(string userId, Guid postId, CancellationToken ct = default)
    {
        var post = await db.Posts.Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId, ct);
        if (post is null) return CancelOutcome.NotFound;

        var toCancel = post.Targets.Where(t => Cancellable.Contains(t.Status)).ToList();
        if (toCancel.Count == 0) return CancelOutcome.NothingToCancel;

        var now = clock.UtcNow;
        foreach (var target in toCancel)
        {
            target.Status = TargetStatus.Cancelled;
            target.UpdatedAt = now;
        }
        post.RootStatus = RootStatusCalculator.Compute(post.Targets);
        post.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return CancelOutcome.Cancelled;
    }

    /// <summary>
    /// Hard-deletes a post (row + cascade targets + stored payload/media). Works for terminal history
    /// entries and for stale/orphaned rows still showing as queued: removing the row means any delayed
    /// worker message for it finds no target and no-ops. Returns false if the post isn't the user's.
    /// </summary>
    public async Task<bool> DeleteAsync(string userId, Guid postId, CancellationToken ct = default)
    {
        var post = await db.Posts.Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId, ct);
        if (post is null) return false;

        var mediaManifestJson = post.MediaManifestJson;

        // Row first (cascade drops the targets); then best-effort the object store — an orphaned blob
        // is harmless, an orphaned row is not.
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
        await payloadCleaner.DeleteAsync(postId, mediaManifestJson, ct);
        return true;
    }
}

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
        var post = await db.Posts.Include(p => p.Targets).ThenInclude(t => t.Automations)
            .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId, ct);
        if (post is null) return CancelOutcome.NotFound;

        var toCancel = post.Targets.Where(t => Cancellable.Contains(t.Status)).ToList();
        if (toCancel.Count == 0) return CancelOutcome.NothingToCancel;

        var now = clock.UtcNow;
        foreach (var target in toCancel)
        {
            target.Status = TargetStatus.Cancelled;
            target.UpdatedAt = now;
            // A target that never delivered has nothing to repost/delete: its automations (issue
            // #323) would otherwise sit pending forever with no DueAt ever set.
            foreach (var automation in target.Automations.Where(a => a.Status == AutomationStatus.Pending))
                automation.Status = AutomationStatus.Cancelled;
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

        // Row first (cascade drops the targets); then best-effort the object store: an orphaned blob
        // is harmless, an orphaned row is not.
        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
        await payloadCleaner.DeleteAsync(postId, mediaManifestJson, ct);
        return true;
    }

    /// <summary>
    /// Hard-deletes every one of the user's terminal (history) posts in one go: everything that isn't
    /// still in flight (<see cref="PostStatusService.ActiveStatuses"/>) and isn't a draft. Drafts and
    /// active posts are left untouched — this is "clear my history", not "delete everything". Returns
    /// the number of posts removed.
    /// </summary>
    public async Task<int> DeleteAllHistoryAsync(string userId, CancellationToken ct = default)
    {
        var posts = await db.Posts
            .Include(p => p.Targets)
            .Where(p => p.UserId == userId
                && p.RootStatus != PostRootStatus.Draft
                && !PostStatusService.ActiveStatuses.Contains(p.RootStatus))
            .ToListAsync(ct);
        if (posts.Count == 0) return 0;

        // Rows first (cascade drops the targets); then best-effort the object store per post: an
        // orphaned blob is harmless, an orphaned row is not.
        db.Posts.RemoveRange(posts);
        await db.SaveChangesAsync(ct);

        foreach (var post in posts)
            await payloadCleaner.DeleteAsync(post.Id, post.MediaManifestJson, ct);

        return posts.Count;
    }
}

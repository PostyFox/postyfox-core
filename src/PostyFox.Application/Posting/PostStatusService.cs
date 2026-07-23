using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Application.Options;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

public sealed class PostStatusService(IAppDbContext db, IClock clock, IOptions<RetentionOptions> retention)
{
    private readonly RetentionOptions _retention = retention.Value;

    /// <summary>Root statuses that mean the post is still in flight (worth live-polling).</summary>
    public static readonly PostRootStatus[] ActiveStatuses =
        [PostRootStatus.Queued, PostRootStatus.Generating, PostRootStatus.Delivering];

    public async Task<PostStatusDto?> GetAsync(string userId, Guid postId, CancellationToken ct = default)
    {
        var post = await db.Posts
            .Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId, ct);
        if (post is null) return null;

        var targets = post.Targets
            .OrderBy(t => t.Platform)
            .Select(t => new PostTargetStatusDto(t.Id, t.Platform, t.Status, t.ExternalId, t.ExternalUrl, t.Error, t.Attempts))
            .ToList();

        return new PostStatusDto(post.Id, post.RootStatus, targets);
    }

    /// <summary>
    /// Lists a user's posts, newest first. Always bounded by the retention window so the view never
    /// promises rows the sweeper has already purged. <paramref name="activeOnly"/> narrows to
    /// in-flight posts (the "what's processing right now" view).
    /// </summary>
    public async Task<IReadOnlyList<PostSummaryDto>> ListAsync(
        string userId, bool activeOnly, int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var cutoff = clock.UtcNow.AddDays(-_retention.PostRetentionDays);

        var query = db.Posts
            .AsNoTracking()
            .Where(p => p.UserId == userId);

        if (activeOnly)
            query = query.Where(p => ActiveStatuses.Contains(p.RootStatus));

        // Window (cutoff), order and limit client-side: SQLite cannot compare/ORDER BY
        // DateTimeOffset (see ApiKeyService), so the query stays provider-agnostic. The set is
        // bounded by the retention window — older posts are purged by PostRetentionSweeper.
        var rows = (await query
            .Select(p => new
            {
                p.Id,
                p.RootStatus,
                p.Title,
                p.CreatedAt,
                p.UpdatedAt,
                p.PostAt,
                Targets = p.Targets.Select(t => new { t.Platform, t.Status }).ToList()
            })
            .ToListAsync(ct))
            .Where(p => p.CreatedAt >= cutoff)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit);

        return rows.Select(p => new PostSummaryDto(
            p.Id,
            p.RootStatus,
            p.Title,
            p.Targets.Select(t => t.Platform).Distinct().OrderBy(x => x).ToList(),
            p.Targets.Count,
            p.Targets.Count(t => t.Status == TargetStatus.Delivered),
            p.Targets.Count(t => t.Status == TargetStatus.Failed),
            p.CreatedAt,
            p.UpdatedAt,
            p.PostAt)).ToList();
    }
}

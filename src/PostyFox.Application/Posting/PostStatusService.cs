using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Options;
using PostyFox.Domain.Entities;
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
    /// Returns a post's authored content (everything the compose form needs to recreate it), or null
    /// if the post isn't the user's. Read straight from the row — the object-store copies are just a
    /// mirror of these columns.
    /// </summary>
    public async Task<PostContentDto?> GetContentAsync(string userId, Guid postId, CancellationToken ct = default)
    {
        var post = await db.Posts
            .AsNoTracking()
            .Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId, ct);
        if (post is null) return null;

        // A draft has no PostTarget rows yet — its target selection lives in DraftTargetsJson/
        // DraftTargetOptionsJson instead (see PostIntakeService) — until it's published.
        if (post.RootStatus == PostRootStatus.Draft)
            return new PostContentDto(
                string.IsNullOrEmpty(post.Title) ? null : post.Title,
                string.IsNullOrEmpty(post.Description) ? null : post.Description,
                string.IsNullOrEmpty(post.HtmlDescription) ? null : post.HtmlDescription,
                Json.Deserialize<List<string>>(post.TagsJson) ?? [],
                Json.Deserialize<List<MediaRef>>(post.MediaManifestJson) ?? [],
                post.TemplateId,
                Json.Deserialize<Dictionary<string, string>>(post.VariablesJson) ?? new(),
                Json.Deserialize<List<Guid>>(post.DraftTargetsJson ?? "[]") ?? [],
                post.PostAt,
                post.Rating,
                Json.Deserialize<Dictionary<Guid, IReadOnlyDictionary<string, string>>>(post.DraftTargetOptionsJson ?? "{}") ?? new());

        // "Post again" must re-tick the exact same destination the post was originally sent to, not
        // just its connector — for a multi-target platform (Telegram) that means resolving each
        // target's chat id back to the ConnectorDestination the compose form originally selected.
        // Falls back to the connector itself if that destination is no longer exposed.
        var connectorIdsWithTarget = post.Targets
            .Where(t => t.ConnectorId.HasValue && t.TargetId != null)
            .Select(t => t.ConnectorId!.Value)
            .Distinct()
            .ToList();
        var destinationLookup = connectorIdsWithTarget.Count == 0
            ? new Dictionary<(Guid ConnectorId, string ExternalId), Guid>()
            : (await db.ConnectorDestinations
                .Where(d => connectorIdsWithTarget.Contains(d.ConnectorId))
                .Select(d => new { d.Id, d.ConnectorId, d.ExternalId })
                .ToListAsync(ct))
                .ToDictionary(d => (d.ConnectorId, d.ExternalId), d => d.Id);

        Guid SelectionId(PostTarget t) =>
            t.TargetId != null && destinationLookup.TryGetValue((t.ConnectorId!.Value, t.TargetId), out var destinationId)
                ? destinationId
                : t.ConnectorId!.Value;

        return new PostContentDto(
            string.IsNullOrEmpty(post.Title) ? null : post.Title,
            string.IsNullOrEmpty(post.Description) ? null : post.Description,
            string.IsNullOrEmpty(post.HtmlDescription) ? null : post.HtmlDescription,
            Json.Deserialize<List<string>>(post.TagsJson) ?? [],
            Json.Deserialize<List<MediaRef>>(post.MediaManifestJson) ?? [],
            post.TemplateId,
            Json.Deserialize<Dictionary<string, string>>(post.VariablesJson) ?? new(),
            post.Targets.Where(t => t.ConnectorId.HasValue).Select(SelectionId).Distinct().ToList(),
            post.PostAt,
            post.Rating,
            post.Targets
                .Where(t => t.ConnectorId.HasValue)
                .Select(t => (SelectionId: SelectionId(t),
                    Options: Json.Deserialize<Dictionary<string, string>>(t.OptionsJson)))
                .Where(x => x.Options is { Count: > 0 })
                .GroupBy(x => x.SelectionId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyDictionary<string, string>)g.First().Options!));
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

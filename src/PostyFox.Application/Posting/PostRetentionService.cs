using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Options;

namespace PostyFox.Application.Posting;

/// <summary>
/// Hard-deletes posts older than the configured retention window, together with their stored
/// payloads (title/description/html) and any referenced media. Targets are removed via the
/// cascade on <c>Post → PostTarget</c>. Runs in batches so a single pass stays bounded.
/// </summary>
public sealed class PostRetentionService(
    IAppDbContext db,
    PostPayloadCleaner payloadCleaner,
    IClock clock,
    IOptions<RetentionOptions> retention,
    ILogger<PostRetentionService> logger)
{
    private readonly RetentionOptions _retention = retention.Value;

    /// <summary>Purges expired posts. Returns the number of posts deleted.</summary>
    public async Task<int> PurgeAsync(CancellationToken ct = default)
    {
        var cutoff = clock.UtcNow.AddDays(-_retention.PostRetentionDays);

        var expired = await db.Posts
            .Include(p => p.Targets)
            .Where(p => p.CreatedAt < cutoff)
            .OrderBy(p => p.CreatedAt)
            .Take(_retention.SweepBatchSize)
            .ToListAsync(ct);

        if (expired.Count == 0) return 0;

        // Remove the DB rows first (cascade drops the targets), then best-effort the object store —
        // an orphaned blob is harmless, an orphaned row is not.
        db.Posts.RemoveRange(expired);
        await db.SaveChangesAsync(ct);

        foreach (var post in expired)
            await payloadCleaner.DeleteAsync(post.Id, post.MediaManifestJson, ct);

        logger.LogInformation(
            "Retention sweep deleted {Count} posts created before {Cutoff:o}", expired.Count, cutoff);
        return expired.Count;
    }
}

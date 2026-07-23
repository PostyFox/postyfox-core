using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Options;

namespace PostyFox.Application.Posting;

/// <summary>
/// Hard-deletes posts older than the configured retention window, together with their stored
/// payloads (title/description/html) and any referenced media. Targets are removed via the
/// cascade on <c>Post → PostTarget</c>. Runs in batches so a single pass stays bounded.
/// </summary>
public sealed class PostRetentionService(
    IAppDbContext db,
    IObjectStore objectStore,
    IClock clock,
    IOptions<PipelineOptions> pipeline,
    IOptions<RetentionOptions> retention,
    ILogger<PostRetentionService> logger)
{
    private readonly PipelineOptions _pipeline = pipeline.Value;
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
            await DeletePayloadAsync(post.Id, post.MediaManifestJson, ct);

        logger.LogInformation(
            "Retention sweep deleted {Count} posts created before {Cutoff:o}", expired.Count, cutoff);
        return expired.Count;
    }

    private async Task DeletePayloadAsync(Guid postId, string mediaManifestJson, CancellationToken ct)
    {
        var container = _pipeline.PostContainer;
        foreach (var suffix in new[] { "title", "description", "description-html" })
            await TryDeleteAsync(container, $"{postId}/{suffix}", ct);

        // Media is uploaded per-compose under a unique key and referenced only by this post, so it is
        // safe to remove alongside the post.
        List<MediaRef>? media = null;
        try { media = Json.Deserialize<List<MediaRef>>(mediaManifestJson); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not parse media manifest for post {PostId}", postId); }

        foreach (var m in media ?? [])
            await TryDeleteAsync(m.Container, m.Key, ct);
    }

    private async Task TryDeleteAsync(string container, string key, CancellationToken ct)
    {
        try { await objectStore.DeleteAsync(container, key, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed deleting object {Container}/{Key} during retention sweep", container, key); }
    }
}

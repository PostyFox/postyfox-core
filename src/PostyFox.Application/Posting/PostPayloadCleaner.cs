using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Options;

namespace PostyFox.Application.Posting;

/// <summary>
/// Best-effort removal of the object-store artefacts owned by a post: its authored payload
/// (title/description/html) and any media referenced only by that post. Shared by the retention
/// sweeper and the single-post delete path so both clean up the same way. An orphaned blob is
/// harmless (the DB row is already gone), so every deletion is swallowed and logged, never thrown.
/// </summary>
public sealed class PostPayloadCleaner(
    IObjectStore objectStore,
    IOptions<PipelineOptions> pipeline,
    ILogger<PostPayloadCleaner> logger)
{
    private readonly PipelineOptions _pipeline = pipeline.Value;

    public async Task DeleteAsync(Guid postId, string mediaManifestJson, CancellationToken ct = default)
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
        catch (Exception ex) { logger.LogWarning(ex, "Failed deleting object {Container}/{Key}", container, key); }
    }
}

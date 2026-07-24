using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;

namespace PostyFox.Application.Posting;

/// <summary>
/// Copies a media blob to a fresh, user-owned key so the copy is independent of the original. Used
/// when recreating a post: the new post must own its media outright, otherwise deleting (or the
/// retention sweep expiring) the original would pull the blob out from under the copy.
/// </summary>
public sealed class MediaCopier(IObjectStore store)
{
    public async Task<MediaRef> CopyAsync(string userId, MediaRef source, CancellationToken ct = default)
    {
        // Mirror the upload key scheme ({userId}/{guid}/{filename}) so the copy is owned exactly like a
        // fresh upload and is cleaned up with the post that references it.
        var fileName = source.Key.Split('/').LastOrDefault() ?? "file";
        var newKey = $"{userId}/{Guid.NewGuid():N}/{fileName}";

        await using var stream = await store.GetAsync(source.Container, source.Key, ct);
        await store.PutAsync(source.Container, newKey, stream, source.ContentType, ct);

        return source with { Key = newKey };
    }
}

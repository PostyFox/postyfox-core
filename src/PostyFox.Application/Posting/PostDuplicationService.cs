using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;

namespace PostyFox.Application.Posting;

/// <summary>
/// Prepares the content for recreating ("post again") an existing post: the same authored fields,
/// but with its media copied to fresh user-owned blobs so the recreated post is fully self-contained.
/// </summary>
public sealed class PostDuplicationService(PostStatusService status, MediaCopier mediaCopier)
{
    /// <summary>
    /// Returns the post's content with independent copies of its media, or null if it isn't the
    /// user's. Only the media is duplicated here; the caller (compose form) creates the new post.
    /// </summary>
    public async Task<PostContentDto?> DuplicateAsync(string userId, Guid postId, CancellationToken ct = default)
    {
        var content = await status.GetContentAsync(userId, postId, ct);
        if (content is null) return null;
        if (content.Media.Count == 0) return content;

        var copies = new List<MediaRef>(content.Media.Count);
        foreach (var media in content.Media)
            copies.Add(await mediaCopier.CopyAsync(userId, media, ct));

        return content with { Media = copies };
    }
}

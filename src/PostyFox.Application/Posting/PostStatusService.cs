using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;

namespace PostyFox.Application.Posting;

public sealed class PostStatusService(IAppDbContext db)
{
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
}

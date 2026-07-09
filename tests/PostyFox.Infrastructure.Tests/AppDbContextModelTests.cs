using Microsoft.EntityFrameworkCore;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class AppDbContextModelTests
{
    [Fact]
    public async Task Post_with_targets_persists_and_cascades()
    {
        using var db = new SqliteDb();
        var post = new Post { Id = Guid.NewGuid(), UserId = "u1", Title = "t", RootStatus = PostRootStatus.Queued };
        post.Targets.Add(new PostTarget { Id = Guid.NewGuid(), PostId = post.Id, Platform = "DiscordWH", Status = TargetStatus.Queued });
        db.Context.Posts.Add(post);
        await db.Context.SaveChangesAsync();

        db.Context.ChangeTracker.Clear();
        var loaded = await db.Context.Posts.Include(p => p.Targets).FirstAsync(p => p.Id == post.Id);
        Assert.Single(loaded.Targets);
        Assert.Equal(PostRootStatus.Queued, loaded.RootStatus);

        db.Context.Posts.Remove(loaded);
        await db.Context.SaveChangesAsync();
        Assert.Empty(db.Context.PostTargets); // cascade delete
    }

    [Fact]
    public async Task Enum_status_is_stored_as_string()
    {
        using var db = new SqliteDb();
        var post = new Post { Id = Guid.NewGuid(), UserId = "u1", RootStatus = PostRootStatus.PartiallyFailed };
        db.Context.Posts.Add(post);
        await db.Context.SaveChangesAsync();

        var raw = await db.Context.Database
            .SqlQueryRaw<string>("SELECT RootStatus AS Value FROM posts")
            .SingleAsync();
        Assert.Equal("PartiallyFailed", raw);
    }
}

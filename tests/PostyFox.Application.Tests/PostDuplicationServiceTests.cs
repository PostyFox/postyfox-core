using System.Text;
using Microsoft.Extensions.Options;
using PostyFox.Application.Connectors;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Application.Tests;

public class PostDuplicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

    private static PostDuplicationService New(TestDbContext db, FakeObjectStore store)
    {
        var status = new PostStatusService(db, new FixedClock(Now),
            Microsoft.Extensions.Options.Options.Create(new RetentionOptions { PostRetentionDays = 30 }));
        return new PostDuplicationService(status, new MediaCopier(store));
    }

    private static async Task<Post> SeedAsync(TestDbContext db, string userId, params MediaRef[] media)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(), UserId = userId, Title = "Title", Description = "Body",
            TagsJson = Json.Serialize(new[] { "a", "b" }),
            MediaManifestJson = Json.Serialize(media),
            RootStatus = PostRootStatus.Delivered, CreatedAt = Now, UpdatedAt = Now,
            Targets = { new PostTarget { Id = Guid.NewGuid(), ConnectorId = Guid.NewGuid(), Platform = "DiscordWH", Status = TargetStatus.Delivered, CreatedAt = Now } }
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return post;
    }

    [Fact]
    public async Task Duplicate_copies_media_to_fresh_keys_with_same_bytes()
    {
        using var db = TestDbContext.Create();
        var store = new FakeObjectStore();
        var srcKey = "u1/original/a.png";
        store.Blobs[$"media/{srcKey}"] = Encoding.UTF8.GetBytes("PNGDATA");
        var post = await SeedAsync(db, "u1", new MediaRef("media", srcKey, "image/png", "alt text"));

        var content = await New(db, store).DuplicateAsync("u1", post.Id);

        var copied = Assert.Single(content!.Media);
        Assert.NotEqual(srcKey, copied.Key);                 // new key
        Assert.StartsWith("u1/", copied.Key);                // owned by the user
        Assert.Equal("media", copied.Container);
        Assert.Equal("image/png", copied.ContentType);
        Assert.Equal("alt text", copied.Alt);                // metadata preserved
        Assert.True(store.Blobs.ContainsKey($"media/{copied.Key}"));
        Assert.Equal(store.Blobs[$"media/{srcKey}"], store.Blobs[$"media/{copied.Key}"]); // same bytes
        Assert.True(store.Blobs.ContainsKey($"media/{srcKey}")); // original left intact
    }

    [Fact]
    public async Task Duplicate_carries_authored_fields()
    {
        using var db = TestDbContext.Create();
        var post = await SeedAsync(db, "u1");

        var content = await New(db, new FakeObjectStore()).DuplicateAsync("u1", post.Id);

        Assert.Equal("Title", content!.Title);
        Assert.Equal("Body", content.Description);
        Assert.Equal(["a", "b"], content.Tags);
        Assert.Single(content.ConnectorIds);
    }

    [Fact]
    public async Task Duplicate_other_users_post_is_null()
    {
        using var db = TestDbContext.Create();
        var post = await SeedAsync(db, "u1");

        Assert.Null(await New(db, new FakeObjectStore()).DuplicateAsync("someone-else", post.Id));
    }
}

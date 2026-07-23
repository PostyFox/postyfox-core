using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Application.Tests;

public class PostRetentionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

    private static PostRetentionService New(TestDbContext db, FakeObjectStore store, int retentionDays = 30, int batch = 500) =>
        new(db, store, new FixedClock(Now),
            Microsoft.Extensions.Options.Options.Create(new PipelineOptions { PostContainer = "post" }),
            Microsoft.Extensions.Options.Options.Create(new RetentionOptions { PostRetentionDays = retentionDays, SweepBatchSize = batch }),
            NullLogger<PostRetentionService>.Instance);

    private static async Task<Post> SeedPostAsync(TestDbContext db, FakeObjectStore store, DateTimeOffset createdAt, string userId = "u1")
    {
        var media = new[] { new { container = "media", key = $"{userId}/{Guid.NewGuid():N}/a.png", contentType = "image/png", alt = (string?)null } };
        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "t",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            MediaManifestJson = Json.Serialize(media),
            Targets = { new PostTarget { Id = Guid.NewGuid(), Platform = "DiscordWH", Status = TargetStatus.Delivered, CreatedAt = createdAt } }
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        store.Text[$"post/{post.Id}/title"] = "t";
        store.Text[$"post/{post.Id}/description"] = "d";
        store.Text[$"post/{post.Id}/description-html"] = "<p>d</p>";
        store.Text[$"{media[0].container}/{media[0].key}"] = "bytes";
        return post;
    }

    [Fact]
    public async Task Purge_deletes_posts_older_than_window_and_keeps_recent()
    {
        using var db = TestDbContext.Create();
        var store = new FakeObjectStore();
        var old = await SeedPostAsync(db, store, Now.AddDays(-31));
        var fresh = await SeedPostAsync(db, store, Now.AddDays(-2));

        var deleted = await New(db, store).PurgeAsync();

        Assert.Equal(1, deleted);
        var remaining = Assert.Single(db.Posts);
        Assert.Equal(fresh.Id, remaining.Id);
        // Cascade removed the old post's target; the fresh one's remains.
        Assert.Single(db.PostTargets);
    }

    [Fact]
    public async Task Purge_removes_stored_payload_and_media_for_deleted_posts()
    {
        using var db = TestDbContext.Create();
        var store = new FakeObjectStore();
        var old = await SeedPostAsync(db, store, Now.AddDays(-40));

        await New(db, store).PurgeAsync();

        Assert.False(store.Text.ContainsKey($"post/{old.Id}/title"));
        Assert.False(store.Text.ContainsKey($"post/{old.Id}/description"));
        Assert.False(store.Text.ContainsKey($"post/{old.Id}/description-html"));
        Assert.DoesNotContain(store.Text.Keys, k => k.StartsWith("media/"));
    }

    [Fact]
    public async Task Purge_respects_batch_size()
    {
        using var db = TestDbContext.Create();
        var store = new FakeObjectStore();
        for (var i = 0; i < 5; i++) await SeedPostAsync(db, store, Now.AddDays(-50 - i));

        var deleted = await New(db, store, batch: 2).PurgeAsync();

        Assert.Equal(2, deleted);
        Assert.Equal(3, db.Posts.Count());
    }

    [Fact]
    public async Task Purge_with_nothing_expired_is_noop()
    {
        using var db = TestDbContext.Create();
        var store = new FakeObjectStore();
        await SeedPostAsync(db, store, Now.AddDays(-1));

        var deleted = await New(db, store).PurgeAsync();

        Assert.Equal(0, deleted);
        Assert.Single(db.Posts);
    }
}

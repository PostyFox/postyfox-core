using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using Xunit;

namespace PostyFox.Application.Tests;

public class TagPresetServiceTests
{
    private static TagPresetService NewService(TestDbContext db) => new(db, new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public async Task Upsert_creates_then_updates()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);

        var created = await svc.UpsertAsync("u1", new TagPresetUpsertRequest(null, "Weekly art", ["art", "fursona"]));
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(["art", "fursona"], created.Tags);

        var updated = await svc.UpsertAsync("u1", new TagPresetUpsertRequest(created.Id, "Weekly", ["art"]));
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Weekly", updated.Name);
        Assert.Equal(["art"], updated.Tags);
        Assert.Single(db.TagPresets);
    }

    [Fact]
    public async Task Get_and_delete_are_owner_scoped()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        var created = await svc.UpsertAsync("owner", new TagPresetUpsertRequest(null, "P", ["a", "b"]));

        Assert.Null(await svc.GetAsync("intruder", created.Id));
        Assert.False(await svc.DeleteAsync("intruder", created.Id));
        Assert.True(await svc.DeleteAsync("owner", created.Id));
        Assert.Empty(db.TagPresets);
    }

    [Fact]
    public async Task List_is_owner_scoped_and_ordered_by_name()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        await svc.UpsertAsync("u1", new TagPresetUpsertRequest(null, "Zebra", ["z"]));
        await svc.UpsertAsync("u1", new TagPresetUpsertRequest(null, "Alpha", ["a"]));
        await svc.UpsertAsync("u2", new TagPresetUpsertRequest(null, "Other", ["o"]));

        var list = await svc.ListAsync("u1");
        Assert.Equal(["Alpha", "Zebra"], list.Select(t => t.Name));
    }
}

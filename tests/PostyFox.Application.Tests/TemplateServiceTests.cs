using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using Xunit;

namespace PostyFox.Application.Tests;

public class TemplateServiceTests
{
    private static TemplateService NewService(TestDbContext db) => new(db, new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public async Task Upsert_creates_then_updates()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);

        var created = await svc.UpsertAsync("u1", new TemplateUpsertRequest(null, "Title", "Body"));
        Assert.NotEqual(Guid.Empty, created.Id);

        var updated = await svc.UpsertAsync("u1", new TemplateUpsertRequest(created.Id, "New", "New body"));
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("New", updated.Title);
        Assert.Single(db.Templates);
    }

    [Fact]
    public async Task Get_and_delete_are_owner_scoped()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        var created = await svc.UpsertAsync("owner", new TemplateUpsertRequest(null, "T", "B"));

        Assert.Null(await svc.GetAsync("intruder", created.Id));
        Assert.False(await svc.DeleteAsync("intruder", created.Id));
        Assert.True(await svc.DeleteAsync("owner", created.Id));
        Assert.Empty(db.Templates);
    }
}

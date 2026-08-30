using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using Xunit;

namespace PostyFox.Application.Tests;

public class TextTemplateServiceTests
{
    private static TextTemplateService NewService(TestDbContext db) => new(db, new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public async Task Upsert_creates_then_updates_including_connector_overrides()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        var connectorId = Guid.NewGuid();

        var created = await svc.UpsertAsync("u1", new TextTemplateUpsertRequest(
            null, "mention", "friend", new Dictionary<Guid, string> { [connectorId] = "@alice" }));
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("friend", created.DefaultValue);
        Assert.Equal("@alice", created.ConnectorValues[connectorId]);

        var updated = await svc.UpsertAsync("u1", new TextTemplateUpsertRequest(
            created.Id, "mention", "pal", new Dictionary<Guid, string>()));
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("pal", updated.DefaultValue);
        Assert.Empty(updated.ConnectorValues);
        Assert.Single(db.TextTemplates);
    }

    [Fact]
    public async Task Upsert_rejects_a_blank_name()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        await Assert.ThrowsAsync<ConnectorValidationException>(() =>
            svc.UpsertAsync("u1", new TextTemplateUpsertRequest(null, "  ", "", new Dictionary<Guid, string>())));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has.dot")]
    [InlineData("has/slash")]
    public async Task Upsert_rejects_names_that_would_not_round_trip_through_the_tt_token(string name)
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        await Assert.ThrowsAsync<ConnectorValidationException>(() =>
            svc.UpsertAsync("u1", new TextTemplateUpsertRequest(null, name, "", new Dictionary<Guid, string>())));
    }

    [Fact]
    public async Task Upsert_rejects_a_case_insensitive_duplicate_name_for_the_same_user()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        await svc.UpsertAsync("u1", new TextTemplateUpsertRequest(null, "Mention", "", new Dictionary<Guid, string>()));

        await Assert.ThrowsAsync<ConnectorValidationException>(() =>
            svc.UpsertAsync("u1", new TextTemplateUpsertRequest(null, "mention", "", new Dictionary<Guid, string>())));
    }

    [Fact]
    public async Task Upsert_allows_the_same_name_for_different_users()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        await svc.UpsertAsync("u1", new TextTemplateUpsertRequest(null, "mention", "a", new Dictionary<Guid, string>()));
        var other = await svc.UpsertAsync("u2", new TextTemplateUpsertRequest(null, "mention", "b", new Dictionary<Guid, string>()));
        Assert.Equal("b", other.DefaultValue);
    }

    [Fact]
    public async Task Upsert_keeping_its_own_name_is_not_a_duplicate_against_itself()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        var created = await svc.UpsertAsync("u1", new TextTemplateUpsertRequest(null, "mention", "a", new Dictionary<Guid, string>()));

        var updated = await svc.UpsertAsync("u1", new TextTemplateUpsertRequest(created.Id, "mention", "b", new Dictionary<Guid, string>()));
        Assert.Equal("b", updated.DefaultValue);
    }

    [Fact]
    public async Task Get_and_delete_are_owner_scoped()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        var created = await svc.UpsertAsync("owner", new TextTemplateUpsertRequest(null, "mention", "", new Dictionary<Guid, string>()));

        Assert.Null(await svc.GetAsync("intruder", created.Id));
        Assert.False(await svc.DeleteAsync("intruder", created.Id));
        Assert.True(await svc.DeleteAsync("owner", created.Id));
        Assert.Empty(db.TextTemplates);
    }

    [Fact]
    public async Task List_is_owner_scoped_and_ordered_by_name()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        await svc.UpsertAsync("u1", new TextTemplateUpsertRequest(null, "zebra", "", new Dictionary<Guid, string>()));
        await svc.UpsertAsync("u1", new TextTemplateUpsertRequest(null, "alpha", "", new Dictionary<Guid, string>()));
        await svc.UpsertAsync("u2", new TextTemplateUpsertRequest(null, "other", "", new Dictionary<Guid, string>()));

        var list = await svc.ListAsync("u1");
        Assert.Equal(["alpha", "zebra"], list.Select(t => t.Name));
    }
}

using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class PairedUserAgentServiceTests
{
    private const string Secret = """{"CookieHeader":"a=1","UserAgent":"Browser/1.0"}""";

    private static PairedUserAgentService Service(TestDbContext db) =>
        new(db, new FakeRegistry(new FakeCookiePairingConnector("FurAffinity", "a"), new FakeConnector("BlueSky")));

    private static TestDbContext Db(bool usePaired = true)
    {
        var db = TestDbContext.Create();
        foreach (var platform in new[] { "FurAffinity", "BlueSky" })
            db.ServiceDefinitions.Add(new ServiceDefinition
            {
                Id = platform, Name = platform, Platform = platform, Enabled = true, UsePairedUserAgent = usePaired
            });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task List_returns_only_cookie_paired_platforms()
    {
        using var db = Db();

        var list = await Service(db).ListAsync();

        var only = Assert.Single(list);
        Assert.Equal("FurAffinity", only.Platform);
        Assert.True(only.UsePairedUserAgent);
    }

    [Fact]
    public async Task Set_persists_the_choice()
    {
        using var db = Db();

        var result = await Service(db).SetAsync("FurAffinity", false);

        Assert.False(result!.UsePairedUserAgent);
        Assert.False(db.ServiceDefinitions.Single(s => s.Id == "FurAffinity").UsePairedUserAgent);
    }

    [Fact]
    public async Task Set_rejects_unknown_and_non_cookie_platforms()
    {
        using var db = Db();
        var service = Service(db);

        Assert.Null(await service.SetAsync("BlueSky", false));
        Assert.Null(await service.SetAsync("Nope", false));
    }

    [Fact]
    public async Task Apply_leaves_the_secret_alone_when_the_paired_agent_is_allowed()
    {
        using var db = Db();

        Assert.Equal(Secret, await PairedUserAgentService.ApplyAsync(db, "FurAffinity", Secret));
    }

    [Fact]
    public async Task Apply_strips_the_user_agent_when_the_default_is_selected()
    {
        using var db = Db(usePaired: false);

        var result = await PairedUserAgentService.ApplyAsync(db, "FurAffinity", Secret);

        Assert.DoesNotContain("UserAgent", result);
        Assert.Contains("CookieHeader", result);
    }

    [Fact]
    public async Task Apply_tolerates_null_and_non_object_secrets()
    {
        using var db = Db(usePaired: false);

        Assert.Null(await PairedUserAgentService.ApplyAsync(db, "FurAffinity", null));
        Assert.Equal("UserAgent[", await PairedUserAgentService.ApplyAsync(db, "FurAffinity", "UserAgent["));
    }
}

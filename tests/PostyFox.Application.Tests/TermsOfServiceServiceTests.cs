using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using Xunit;

namespace PostyFox.Application.Tests;

public class TermsOfServiceServiceTests
{
    private static TermsOfServiceService Create(TestDbContext db) => new(db, new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public async Task No_terms_means_nothing_to_accept()
    {
        using var db = TestDbContext.Create();
        var svc = Create(db);

        Assert.Null(await svc.GetCurrentVersionAsync());
        var status = await svc.GetStatusAsync("user-1", "user-1");
        Assert.Null(status.Current);
        Assert.True(status.Accepted);
        Assert.True(status.OwnerAccepted);
    }

    [Fact]
    public async Task Publishing_requires_acceptance_and_accepting_records_it()
    {
        using var db = TestDbContext.Create();
        var svc = Create(db);

        var terms = await svc.PublishAsync("admin", "# Terms");
        Assert.NotNull(terms);
        Assert.Equal(terms.Version, await svc.GetCurrentVersionAsync());
        Assert.False((await svc.GetStatusAsync("user-1", "user-1")).Accepted);

        Assert.True(await svc.AcceptAsync("user-1", terms.Version));
        Assert.True(await svc.AcceptAsync("user-1", terms.Version));

        var status = await svc.GetStatusAsync("user-1", "user-1");
        Assert.True(status.Accepted);
        Assert.Equal("# Terms", status.Current!.Content);
        var acceptance = Assert.Single(db.TermsAcceptances);
        Assert.Equal(DateTimeOffset.UnixEpoch, acceptance.AcceptedAt);
    }

    [Fact]
    public async Task A_new_version_requires_acceptance_again_and_stale_versions_are_refused()
    {
        using var db = TestDbContext.Create();
        var svc = Create(db);
        var v1 = await svc.PublishAsync("admin", "v1");
        await svc.AcceptAsync("user-1", v1!.Version);

        var v2 = await svc.PublishAsync("admin", "v2");

        Assert.False(await svc.HasAcceptedAsync("user-1", v2!.Version));
        Assert.False(await svc.AcceptAsync("user-1", v1.Version));
        Assert.True(await svc.AcceptAsync("user-1", v2.Version));
    }

    [Fact]
    public async Task Publishing_blank_content_turns_the_terms_off()
    {
        using var db = TestDbContext.Create();
        var svc = Create(db);
        await svc.PublishAsync("admin", "v1");
        Assert.Equal("v1", (await svc.GetCurrentAsync())!.Content);

        Assert.Null(await svc.PublishAsync("admin", "  "));
        Assert.Null(await svc.GetCurrentVersionAsync());
        Assert.Null(await svc.GetCurrentAsync());
        Assert.Null((await svc.GetStatusAsync("user-1", "user-1")).Current);
    }

    [Fact]
    public async Task Owner_acceptance_is_reported_separately()
    {
        using var db = TestDbContext.Create();
        var svc = Create(db);
        var terms = await svc.PublishAsync("admin", "v1");
        await svc.AcceptAsync("member", terms!.Version);

        var status = await svc.GetStatusAsync("member", "owner");

        Assert.True(status.Accepted);
        Assert.False(status.OwnerAccepted);
    }
}

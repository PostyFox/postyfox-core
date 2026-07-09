using PostyFox.Application.Security;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using Xunit;

namespace PostyFox.Application.Tests;

public class ApiKeyServiceTests
{
    private static ApiKeyService NewService(TestDbContext db) =>
        new(db, new ApiKeyHasher(), new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public async Task Create_returns_40_char_key_and_persists_hash()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);

        var created = await svc.CreateAsync("user-1", "ci");

        Assert.Equal(40, created.ApiKey.Length);
        Assert.Equal(created.ApiKey[..8], created.Prefix);
        var stored = Assert.Single(db.ApiKeys);
        Assert.NotEqual(created.ApiKey, stored.KeyHash); // never stored in clear
    }

    [Fact]
    public async Task Validate_returns_owner_for_valid_key_and_null_otherwise()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        var created = await svc.CreateAsync("user-1", null);

        Assert.Equal("user-1", await svc.ValidateAsync(created.ApiKey));
        Assert.Null(await svc.ValidateAsync("wrong" + created.ApiKey[5..]));
    }

    [Fact]
    public async Task Revoked_key_no_longer_validates()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        var created = await svc.CreateAsync("user-1", null);

        Assert.True(await svc.RevokeAsync("user-1", created.Id));
        Assert.Null(await svc.ValidateAsync(created.ApiKey));
        Assert.Single(await svc.ListAsync("user-1")); // still listed, but revoked
    }

    [Fact]
    public async Task List_is_scoped_to_user()
    {
        using var db = TestDbContext.Create();
        var svc = NewService(db);
        await svc.CreateAsync("user-1", null);
        await svc.CreateAsync("user-2", null);

        Assert.Single(await svc.ListAsync("user-1"));
    }
}

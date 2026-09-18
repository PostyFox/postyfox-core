using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using Xunit;

namespace PostyFox.Application.Tests;

public class UserSettingsServiceTests
{
    [Fact]
    public async Task Defaults_to_off_for_a_user_with_no_row()
    {
        using var db = TestDbContext.Create();
        var svc = new UserSettingsService(db, new FixedClock(DateTimeOffset.UnixEpoch));

        Assert.False((await svc.GetAsync("user-1")).IncludeAdvertisingLine);
    }

    [Fact]
    public async Task Update_creates_the_user_row_and_persists_the_setting()
    {
        using var db = TestDbContext.Create();
        var svc = new UserSettingsService(db, new FixedClock(DateTimeOffset.UnixEpoch));

        await svc.UpdateAsync("user-1", new UserSettingsUpdateRequest(true));
        Assert.True((await svc.GetAsync("user-1")).IncludeAdvertisingLine);

        await svc.UpdateAsync("user-1", new UserSettingsUpdateRequest(false));
        Assert.False((await svc.GetAsync("user-1")).IncludeAdvertisingLine);
        Assert.Single(db.Users);
    }
}

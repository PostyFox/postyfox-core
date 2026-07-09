using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class UserConnectorServiceTests
{
    private static (UserConnectorService svc, FakeSecretStore secrets) New(TestDbContext db)
    {
        var secrets = new FakeSecretStore();
        return (new UserConnectorService(db, secrets, new FixedClock(DateTimeOffset.UnixEpoch)), secrets);
    }

    private static void SeedDefinition(TestDbContext db) =>
        db.ServiceDefinitions.Add(new ServiceDefinition { Id = "DiscordWH", Name = "Discord", Platform = "DiscordWH", Enabled = true });

    [Fact]
    public async Task Upsert_unknown_definition_returns_null()
    {
        using var db = TestDbContext.Create();
        var (svc, _) = New(db);
        var result = await svc.UpsertAsync("u1", new UserConnectorUpsertRequest(null, "Nope", "d", "{}", null, true));
        Assert.Null(result);
    }

    [Fact]
    public async Task Upsert_persists_config_and_stores_secret()
    {
        using var db = TestDbContext.Create();
        SeedDefinition(db);
        await db.SaveChangesAsync();
        var (svc, secrets) = New(db);

        var dto = await svc.UpsertAsync("u1",
            new UserConnectorUpsertRequest(null, "DiscordWH", "My Discord", "{\"Webhook\":\"http://x\"}", "{\"token\":\"abc\"}", true));

        Assert.NotNull(dto);
        Assert.Equal("DiscordWH", dto!.Platform);
        Assert.Equal("{\"token\":\"abc\"}", secrets.Store[UserConnectorService.SecretName(dto.Id, "u1")]);
    }

    [Fact]
    public async Task Delete_removes_row_and_secret()
    {
        using var db = TestDbContext.Create();
        SeedDefinition(db);
        await db.SaveChangesAsync();
        var (svc, secrets) = New(db);
        var dto = await svc.UpsertAsync("u1", new UserConnectorUpsertRequest(null, "DiscordWH", "d", "{}", "{\"s\":1}", true));

        Assert.True(await svc.DeleteAsync("u1", dto!.Id));
        Assert.Empty(db.UserConnectors);
        Assert.Empty(secrets.Store);
    }
}

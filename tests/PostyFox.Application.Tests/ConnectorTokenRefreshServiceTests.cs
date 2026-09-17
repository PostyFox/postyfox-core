using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PostyFox.Application.Connectors;
using PostyFox.Application.Options;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class ConnectorTokenRefreshServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

    private static ConnectorTokenRefreshService New(
        TestDbContext db, FakeSecretStore secrets, FakeRegistry registry, int refreshWithinDays = 10) =>
        new(db, secrets, registry, new FixedClock(Now),
            Microsoft.Extensions.Options.Options.Create(new ConnectorRefreshOptions { RefreshWithinDays = refreshWithinDays }),
            NullLogger<ConnectorTokenRefreshService>.Instance);

    private static async Task<Guid> SeedConnectorAsync(TestDbContext db, string platform, string userId = "u1")
    {
        if (!db.ServiceDefinitions.Any(s => s.Id == platform))
            db.ServiceDefinitions.Add(new ServiceDefinition { Id = platform, Name = platform, Platform = platform, Enabled = true });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = id, UserId = userId, ServiceDefinitionId = platform, DisplayName = platform, Enabled = true
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Refreshes_a_connector_whose_token_expires_soon()
    {
        using var db = TestDbContext.Create();
        var id = await SeedConnectorAsync(db, "Instagram");
        var secrets = new FakeSecretStore();
        secrets.Store[UserConnectorService.SecretName(id, "u1")] =
            """{"AccessToken":"old","ExpiresAt":"2026-07-25T00:00:00Z"}"""; // 2 days out
        var connector = new FakeRefreshableConnector("Instagram");
        var registry = new FakeRegistry(connector);

        var refreshed = await New(db, secrets, registry).RefreshDueAsync();

        Assert.Equal(1, refreshed);
        Assert.Equal(1, connector.RefreshCount);
        Assert.Contains("\"AccessToken\":\"new\"", secrets.Store[UserConnectorService.SecretName(id, "u1")]);
    }

    [Fact]
    public async Task Skips_a_connector_whose_token_is_not_near_expiry()
    {
        using var db = TestDbContext.Create();
        var id = await SeedConnectorAsync(db, "Instagram");
        var secrets = new FakeSecretStore();
        secrets.Store[UserConnectorService.SecretName(id, "u1")] =
            """{"AccessToken":"old","ExpiresAt":"2026-09-01T00:00:00Z"}"""; // 40 days out
        var connector = new FakeRefreshableConnector("Instagram");
        var registry = new FakeRegistry(connector);

        var refreshed = await New(db, secrets, registry).RefreshDueAsync();

        Assert.Equal(0, refreshed);
        Assert.Equal(0, connector.RefreshCount);
    }

    [Fact]
    public async Task Ignores_connectors_for_platforms_that_are_not_refreshable()
    {
        using var db = TestDbContext.Create();
        var id = await SeedConnectorAsync(db, "Tumblr");
        var secrets = new FakeSecretStore();
        secrets.Store[UserConnectorService.SecretName(id, "u1")] = """{"OAuthToken":"t","OAuthTokenSecret":"s"}""";
        var registry = new FakeRegistry(new FakeConnector("Tumblr"));

        var refreshed = await New(db, secrets, registry).RefreshDueAsync();

        Assert.Equal(0, refreshed);
    }

    [Fact]
    public async Task A_declined_refresh_leaves_the_existing_secret_in_place_and_does_not_throw()
    {
        using var db = TestDbContext.Create();
        var id = await SeedConnectorAsync(db, "Instagram");
        var secrets = new FakeSecretStore();
        var originalSecret = """{"AccessToken":"old","ExpiresAt":"2026-07-25T00:00:00Z"}""";
        secrets.Store[UserConnectorService.SecretName(id, "u1")] = originalSecret;
        var connector = new FakeRefreshableConnector("Instagram", refresh: _ => null);
        var registry = new FakeRegistry(connector);

        var refreshed = await New(db, secrets, registry).RefreshDueAsync();

        Assert.Equal(0, refreshed);
        Assert.Equal(originalSecret, secrets.Store[UserConnectorService.SecretName(id, "u1")]);
    }

    [Fact]
    public async Task One_connectors_failure_does_not_stop_the_others_from_refreshing()
    {
        using var db = TestDbContext.Create();
        var failingId = await SeedConnectorAsync(db, "Instagram", "u1");
        var okId = await SeedConnectorAsync(db, "Instagram", "u2");
        var secrets = new FakeSecretStore();
        secrets.Store[UserConnectorService.SecretName(failingId, "u1")] =
            """{"AccessToken":"old","ExpiresAt":"2026-07-25T00:00:00Z"}""";
        secrets.Store[UserConnectorService.SecretName(okId, "u2")] =
            """{"AccessToken":"old","ExpiresAt":"2026-07-25T00:00:00Z"}""";
        var connector = new FakeRefreshableConnector("Instagram", refresh: ctx =>
            ctx.UserId == "u1" ? throw new InvalidOperationException("boom") : """{"AccessToken":"new","ExpiresAt":"2999-01-01T00:00:00Z"}""");
        var registry = new FakeRegistry(connector);

        var refreshed = await New(db, secrets, registry).RefreshDueAsync();

        Assert.Equal(1, refreshed);
    }
}

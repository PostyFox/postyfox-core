using System.Text.Json;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class ConnectorCookiePairingServiceTests
{
    private static async Task<Guid> SeedAsync(TestDbContext db, string platform = "FurAffinity")
    {
        db.ServiceDefinitions.Add(new ServiceDefinition
        {
            Id = platform,
            Name = platform,
            Platform = platform,
            Enabled = true
        });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = id,
            UserId = "u1",
            ServiceDefinitionId = platform,
            DisplayName = platform,
            ConfigJson = "{}",
            Enabled = true
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Pairing_is_hashed_one_use_and_stores_only_required_cookies()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedAsync(db);
        var secrets = new FakeSecretStore();
        var clock = new FixedClock(DateTimeOffset.UnixEpoch);
        var service = new ConnectorCookiePairingService(db, secrets, clock);

        var start = await service.StartAsync("u1", connectorId);

        Assert.NotNull(start);
        var pending = Assert.Single(db.ConnectorCookiePairings);
        Assert.DoesNotContain(start!.PairingToken, pending.TokenHash);

        var outcome = await service.CompleteAsync(start.PairingToken, new Dictionary<string, string>
        {
            ["a"] = "session-a",
            ["b"] = "session-b",
            ["tracking"] = "must-not-be-stored"
        });

        Assert.Equal(ConnectorCookiePairingOutcome.Completed, outcome);
        Assert.Empty(db.ConnectorCookiePairings);
        var stored = secrets.Store[UserConnectorService.SecretName(connectorId, "u1")];
        using var json = JsonDocument.Parse(stored);
        Assert.Equal("a=session-a; b=session-b", json.RootElement.GetProperty("CookieHeader").GetString());
        Assert.DoesNotContain("tracking", stored);
        Assert.Equal(
            ConnectorCookiePairingOutcome.InvalidOrExpired,
            await service.CompleteAsync(start.PairingToken, new Dictionary<string, string>
            {
                ["a"] = "session-a",
                ["b"] = "session-b"
            }));
    }

    [Fact]
    public async Task Invalid_cookie_payload_does_not_consume_pairing()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedAsync(db);
        var service = new ConnectorCookiePairingService(
            db,
            new FakeSecretStore(),
            new FixedClock(DateTimeOffset.UnixEpoch));
        var start = await service.StartAsync("u1", connectorId);

        var outcome = await service.CompleteAsync(start!.PairingToken, new Dictionary<string, string>
        {
            ["a"] = "session-a"
        });

        Assert.Equal(ConnectorCookiePairingOutcome.InvalidCookies, outcome);
        Assert.Single(db.ConnectorCookiePairings);
    }

    [Fact]
    public async Task Expired_pairing_is_rejected_and_removed()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedAsync(db);
        var clock = new FixedClock(DateTimeOffset.UnixEpoch);
        var service = new ConnectorCookiePairingService(db, new FakeSecretStore(), clock);
        var start = await service.StartAsync("u1", connectorId);
        clock.UtcNow = clock.UtcNow.AddMinutes(6);

        var outcome = await service.CompleteAsync(start!.PairingToken, new Dictionary<string, string>
        {
            ["a"] = "session-a",
            ["b"] = "session-b"
        });

        Assert.Equal(ConnectorCookiePairingOutcome.InvalidOrExpired, outcome);
        Assert.Empty(db.ConnectorCookiePairings);
    }

    [Fact]
    public async Task Pairing_can_only_start_for_owned_furaffinity_connector()
    {
        using var db = TestDbContext.Create();
        var faId = await SeedAsync(db);
        var otherId = await SeedAsync(db, "BlueSky");
        var service = new ConnectorCookiePairingService(
            db,
            new FakeSecretStore(),
            new FixedClock(DateTimeOffset.UnixEpoch));

        Assert.Null(await service.StartAsync("another-user", faId));
        Assert.Null(await service.StartAsync("u1", otherId));
    }

    [Fact]
    public async Task Starting_again_invalidates_and_replaces_the_previous_pairing()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedAsync(db);
        var service = new ConnectorCookiePairingService(
            db,
            new FakeSecretStore(),
            new FixedClock(DateTimeOffset.UnixEpoch));

        var first = await service.StartAsync("u1", connectorId);
        var second = await service.StartAsync("u1", connectorId);

        Assert.NotEqual(first!.PairingToken, second!.PairingToken);
        Assert.Single(db.ConnectorCookiePairings);
        Assert.Equal(
            ConnectorCookiePairingOutcome.InvalidOrExpired,
            await service.CompleteAsync(first.PairingToken, new Dictionary<string, string>
            {
                ["a"] = "session-a",
                ["b"] = "session-b"
            }));
    }
}

using System.Text.Json;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class ConnectorCookiePairingServiceTests
{
    private static readonly Dictionary<string, string> ValidCookies = new()
    {
        ["a"] = "session-a",
        ["b"] = "session-b"
    };

    private static FakeRegistry Registry() => new(
        new FakeCookiePairingConnector("FurAffinity", "a", "b"),
        new FakeConnector("BlueSky"));

    private static ConnectorCookiePairingService Service(
        TestDbContext db,
        FakeSecretStore? secrets = null,
        FixedClock? clock = null) =>
        new(db, secrets ?? new FakeSecretStore(), clock ?? new FixedClock(DateTimeOffset.UnixEpoch), Registry());

    private static void SeedDefinition(TestDbContext db, string platform) =>
        db.ServiceDefinitions.Add(new ServiceDefinition
        {
            Id = platform,
            Name = platform,
            Platform = platform,
            Enabled = true
        });

    private static async Task<Guid> SeedAsync(
        TestDbContext db,
        string platform = "FurAffinity",
        string userId = "u1",
        string? displayName = null)
    {
        if (!db.ServiceDefinitions.Local.Any(s => s.Id == platform)
            && !db.ServiceDefinitions.Any(s => s.Id == platform))
            SeedDefinition(db, platform);

        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = id,
            UserId = userId,
            ServiceDefinitionId = platform,
            DisplayName = displayName ?? platform,
            ConfigJson = "{}",
            Enabled = true
        });
        await db.SaveChangesAsync();
        return id;
    }

    // ----- direct pairing (the browser extension's one-click path) --------------------------------

    [Fact]
    public async Task Pairing_by_platform_uses_the_users_only_connector()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedAsync(db);
        var secrets = new FakeSecretStore();
        var service = Service(db, secrets);

        var result = await service.PairAsync("u1", "FurAffinity", null, new Dictionary<string, string>
        {
            ["a"] = "session-a",
            ["b"] = "session-b",
            ["tracking"] = "must-not-be-stored"
        });

        Assert.Equal(ConnectorCookiePairOutcome.Connected, result.Outcome);
        Assert.Equal(connectorId, result.ConnectorId);
        var stored = secrets.Store[UserConnectorService.SecretName(connectorId, "u1")];
        using var json = JsonDocument.Parse(stored);
        Assert.Equal("a=session-a; b=session-b", json.RootElement.GetProperty("CookieHeader").GetString());
        Assert.DoesNotContain("tracking", stored);
    }

    [Fact]
    public async Task Pairing_by_platform_creates_a_connector_when_the_user_has_none()
    {
        using var db = TestDbContext.Create();
        SeedDefinition(db, "FurAffinity");
        await db.SaveChangesAsync();
        var secrets = new FakeSecretStore();
        var service = Service(db, secrets);

        var result = await service.PairAsync("u1", "FurAffinity", null, ValidCookies);

        Assert.Equal(ConnectorCookiePairOutcome.Connected, result.Outcome);
        var created = Assert.Single(db.UserConnectors);
        Assert.Equal("u1", created.UserId);
        Assert.Equal("FurAffinity", created.ServiceDefinitionId);
        Assert.Equal(created.Id, result.ConnectorId);
        Assert.Contains(UserConnectorService.SecretName(created.Id, "u1"), secrets.Store.Keys);
    }

    [Fact]
    public async Task Pairing_by_platform_will_not_guess_between_several_connectors()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, displayName: "Main");
        var second = await SeedAsync(db, displayName: "Alt");
        var service = Service(db);

        Assert.Equal(
            ConnectorCookiePairOutcome.AmbiguousConnector,
            (await service.PairAsync("u1", "FurAffinity", null, ValidCookies)).Outcome);
        // …but naming one resolves it.
        Assert.Equal(
            ConnectorCookiePairOutcome.Connected,
            (await service.PairAsync("u1", null, second, ValidCookies)).Outcome);
    }

    [Fact]
    public async Task Pairing_rejects_unowned_connectors_and_non_cookie_platforms()
    {
        using var db = TestDbContext.Create();
        var faId = await SeedAsync(db);
        var blueSkyId = await SeedAsync(db, "BlueSky");
        var service = Service(db);

        Assert.Equal(
            ConnectorCookiePairOutcome.UnsupportedPlatform,
            (await service.PairAsync("another-user", null, faId, ValidCookies)).Outcome);
        Assert.Equal(
            ConnectorCookiePairOutcome.UnsupportedPlatform,
            (await service.PairAsync("u1", null, blueSkyId, ValidCookies)).Outcome);
        Assert.Equal(
            ConnectorCookiePairOutcome.UnsupportedPlatform,
            (await service.PairAsync("u1", "BlueSky", null, ValidCookies)).Outcome);
    }

    [Fact]
    public async Task Incomplete_cookies_are_rejected_before_a_connector_is_created()
    {
        using var db = TestDbContext.Create();
        SeedDefinition(db, "FurAffinity");
        await db.SaveChangesAsync();
        var service = Service(db);

        var result = await service.PairAsync("u1", "FurAffinity", null, new Dictionary<string, string>
        {
            ["a"] = "session-a"
        });

        Assert.Equal(ConnectorCookiePairOutcome.InvalidCookies, result.Outcome);
        Assert.Empty(db.UserConnectors);
    }

    // ----- discovery ------------------------------------------------------------------------------

    [Fact]
    public async Task Targets_cover_cookie_platforms_only_and_include_unconfigured_ones()
    {
        using var db = TestDbContext.Create();
        SeedDefinition(db, "FurAffinity");
        await SeedAsync(db, "BlueSky");
        var service = Service(db);

        var targets = await service.ListTargetsAsync("u1");

        var target = Assert.Single(targets);
        Assert.Equal("FurAffinity", target.Platform);
        Assert.Null(target.ConnectorId);
        Assert.Equal(["a", "b"], target.CookieNames);
        Assert.Equal("https://furaffinity.test/login", target.LoginUrl);
    }

    [Fact]
    public async Task Targets_list_one_entry_per_owned_connector()
    {
        using var db = TestDbContext.Create();
        await SeedAsync(db, displayName: "Main");
        await SeedAsync(db, displayName: "Alt");
        await SeedAsync(db, userId: "someone-else", displayName: "Theirs");
        var service = Service(db);

        var targets = await service.ListTargetsAsync("u1");

        Assert.Equal(["Alt", "Main"], targets.Select(t => t.DisplayName));
        Assert.All(targets, t => Assert.NotNull(t.ConnectorId));
    }

    // ----- token handshake (fallback for clients without a PostyFox session) ----------------------

    [Fact]
    public async Task Pairing_is_hashed_one_use_and_stores_only_required_cookies()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedAsync(db);
        var secrets = new FakeSecretStore();
        var service = Service(db, secrets);

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
            await service.CompleteAsync(start.PairingToken, ValidCookies));
    }

    [Fact]
    public async Task Invalid_cookie_payload_does_not_consume_pairing()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedAsync(db);
        var service = Service(db);
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
        var service = Service(db, clock: clock);
        var start = await service.StartAsync("u1", connectorId);
        clock.UtcNow = clock.UtcNow.AddMinutes(6);

        var outcome = await service.CompleteAsync(start!.PairingToken, ValidCookies);

        Assert.Equal(ConnectorCookiePairingOutcome.InvalidOrExpired, outcome);
        Assert.Empty(db.ConnectorCookiePairings);
    }

    [Fact]
    public async Task Pairing_can_only_start_for_an_owned_cookie_authenticated_connector()
    {
        using var db = TestDbContext.Create();
        var faId = await SeedAsync(db);
        var otherId = await SeedAsync(db, "BlueSky");
        var service = Service(db);

        Assert.Null(await service.StartAsync("another-user", faId));
        Assert.Null(await service.StartAsync("u1", otherId));
    }

    [Fact]
    public async Task Starting_again_invalidates_and_replaces_the_previous_pairing()
    {
        using var db = TestDbContext.Create();
        var connectorId = await SeedAsync(db);
        var service = Service(db);

        var first = await service.StartAsync("u1", connectorId);
        var second = await service.StartAsync("u1", connectorId);

        Assert.NotEqual(first!.PairingToken, second!.PairingToken);
        Assert.Single(db.ConnectorCookiePairings);
        Assert.Equal(
            ConnectorCookiePairingOutcome.InvalidOrExpired,
            await service.CompleteAsync(first.PairingToken, ValidCookies));
    }
}

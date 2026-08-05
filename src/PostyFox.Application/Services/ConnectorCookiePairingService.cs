using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

public sealed record ConnectorCookiePairingStart(string PairingToken, DateTimeOffset ExpiresAt);

public enum ConnectorCookiePairingOutcome
{
    Completed,
    InvalidOrExpired,
    InvalidCookies
}

public enum ConnectorCookiePairOutcome
{
    Connected,
    /// <summary>The platform is unknown, disabled, or does not authenticate with website cookies.</summary>
    UnsupportedPlatform,
    /// <summary>The user has several connectors for the platform, so the client must name one.</summary>
    AmbiguousConnector,
    InvalidCookies
}

public sealed record ConnectorCookiePairResult(
    ConnectorCookiePairOutcome Outcome,
    Guid? ConnectorId = null,
    string? DisplayName = null);

/// <summary>
/// Connects the connectors that authenticate with a website session cookie rather than an API token.
/// <para>
/// Two routes in, both ending at the same stored secret. <see cref="PairAsync"/> is the direct one: a
/// browser client that carries the user's PostyFox session posts the cookies itself, so no handshake
/// is needed. <see cref="StartAsync"/>/<see cref="CompleteAsync"/> keep the token handshake for
/// clients that cannot present that session (a different browser or profile, or a Safari build where
/// the session cookie does not reach the extension). The pairing token is a bearer secret and is
/// stored only as a hash.
/// </para>
/// </summary>
public sealed partial class ConnectorCookiePairingService(
    IAppDbContext db,
    ISecretsProvider secrets,
    IClock clock,
    IConnectorRegistry connectors)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The cookie-authenticated sites this deployment supports, with no user context: which cookies to
    /// collect and where to log in. Lets a browser client stay useful (and drive the token handshake)
    /// before the user has a PostyFox session. Carries only platform metadata — nothing user-specific.
    /// </summary>
    public async Task<IReadOnlyList<CookiePairingTargetDto>> ListSitesAsync(CancellationToken ct = default)
    {
        var defs = await db.ServiceDefinitions.Where(s => s.Enabled).OrderBy(s => s.Name).ToListAsync(ct);
        return defs
            .Select(def => (def, spec: SpecFor(def.Platform)))
            .Where(x => x.spec is not null)
            .Select(x => new CookiePairingTargetDto(
                null, x.def.Id, x.def.Platform, x.def.Name,
                x.spec!.SiteUrl, x.spec.LoginUrl, x.spec.CookieNames))
            .ToList();
    }

    /// <summary>
    /// Every site the user could hand a session to, with the cookie details a browser client needs.
    /// A platform the user has no connector for still appears, with a null connector id — pairing it
    /// creates the connector. This is the extension's one discovery call: it resolves the connector,
    /// the cookie names, and the site's login URL in a single authenticated round trip.
    /// </summary>
    public async Task<IReadOnlyList<CookiePairingTargetDto>> ListTargetsAsync(
        string userId,
        CancellationToken ct = default)
    {
        var defs = await db.ServiceDefinitions.Where(s => s.Enabled).OrderBy(s => s.Name).ToListAsync(ct);
        var owned = await db.UserConnectors.Where(c => c.UserId == userId).ToListAsync(ct);

        var targets = new List<CookiePairingTargetDto>();
        foreach (var def in defs)
        {
            if (SpecFor(def.Platform) is not { } spec) continue;
            var mine = owned.Where(c => c.ServiceDefinitionId == def.Id).OrderBy(c => c.DisplayName).ToList();
            if (mine.Count == 0)
            {
                targets.Add(new CookiePairingTargetDto(
                    null, def.Id, def.Platform, def.Name, spec.SiteUrl, spec.LoginUrl, spec.CookieNames));
                continue;
            }

            targets.AddRange(mine.Select(c => new CookiePairingTargetDto(
                c.Id, def.Id, def.Platform, c.DisplayName, spec.SiteUrl, spec.LoginUrl, spec.CookieNames)));
        }

        return targets;
    }

    /// <summary>
    /// Stores a website session against the user's connector for the platform. Called by a browser
    /// client that already holds the user's PostyFox session, so the caller's identity is the
    /// authorization — there is no token to mint or redeem.
    /// </summary>
    /// <param name="connectorId">
    /// The connector to update. When null the platform's sole connector is used, or one is created if
    /// the user has none; several candidates yield <see cref="ConnectorCookiePairOutcome.AmbiguousConnector"/>.
    /// </param>
    public async Task<ConnectorCookiePairResult> PairAsync(
        string userId,
        string? platform,
        Guid? connectorId,
        IReadOnlyDictionary<string, string>? cookies,
        CancellationToken ct = default)
    {
        UserConnector? connector = null;
        if (connectorId is { } id)
        {
            connector = await db.UserConnectors
                .Include(c => c.ServiceDefinition)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
            if (connector?.ServiceDefinition is null)
                return new ConnectorCookiePairResult(ConnectorCookiePairOutcome.UnsupportedPlatform);
            platform = connector.ServiceDefinition.Platform;
        }

        if (string.IsNullOrWhiteSpace(platform) || SpecFor(platform) is not { } spec)
            return new ConnectorCookiePairResult(ConnectorCookiePairOutcome.UnsupportedPlatform);
        // Validate before creating anything, so a client that posts junk leaves no connector behind.
        if (!TryCookieHeader(spec, cookies, out var cookieHeader))
            return new ConnectorCookiePairResult(ConnectorCookiePairOutcome.InvalidCookies);

        if (connector is null)
        {
            var def = await db.ServiceDefinitions
                .FirstOrDefaultAsync(s => s.Enabled && s.Platform == platform, ct);
            if (def is null)
                return new ConnectorCookiePairResult(ConnectorCookiePairOutcome.UnsupportedPlatform);

            var mine = await db.UserConnectors
                .Where(c => c.UserId == userId && c.ServiceDefinitionId == def.Id)
                .OrderBy(c => c.DisplayName)
                .ToListAsync(ct);
            if (mine.Count > 1)
                return new ConnectorCookiePairResult(ConnectorCookiePairOutcome.AmbiguousConnector);

            connector = mine.SingleOrDefault();
            if (connector is null)
            {
                // First run: the browser client knows the site but there is nothing to attach the
                // session to yet. Creating the obvious connector here is what keeps this one click —
                // the alternative is bouncing the user into PostyFox to add it by hand first. Every
                // field on the platform's config schema is optional, so defaults are valid.
                connector = new UserConnector
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ServiceDefinitionId = def.Id,
                    DisplayName = def.Name,
                    ConfigJson = "{}",
                    Enabled = true,
                    CreatedAt = clock.UtcNow,
                    UpdatedAt = clock.UtcNow
                };
                db.UserConnectors.Add(connector);
                await db.SaveChangesAsync(ct);
            }
        }

        await StoreSessionAsync(connector.Id, userId, cookieHeader, ct);
        return new ConnectorCookiePairResult(
            ConnectorCookiePairOutcome.Connected, connector.Id, connector.DisplayName);
    }

    public async Task<ConnectorCookiePairingStart?> StartAsync(
        string userId,
        Guid connectorId,
        CancellationToken ct = default)
    {
        var connector = await db.UserConnectors
            .Include(c => c.ServiceDefinition)
            .FirstOrDefaultAsync(c => c.Id == connectorId && c.UserId == userId, ct);
        if (connector?.ServiceDefinition is null || SpecFor(connector.ServiceDefinition.Platform) is null)
            return null;

        // One active handoff per connector. Starting again invalidates the previous code and bounds
        // stale rows even when an expired code is never presented to the completion endpoint.
        var existing = await db.ConnectorCookiePairings
            .Where(p => p.ConnectorId == connectorId)
            .ToListAsync(ct);
        db.ConnectorCookiePairings.RemoveRange(existing);

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = clock.UtcNow;
        db.ConnectorCookiePairings.Add(new ConnectorCookiePairing
        {
            TokenHash = Hash(token),
            ConnectorId = connectorId,
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime)
        });
        await db.SaveChangesAsync(ct);
        return new ConnectorCookiePairingStart(token, now.Add(Lifetime));
    }

    public async Task<ConnectorCookiePairingOutcome> CompleteAsync(
        string token,
        IReadOnlyDictionary<string, string>? cookies,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return ConnectorCookiePairingOutcome.InvalidOrExpired;

        var pairing = await db.ConnectorCookiePairings
            .Include(p => p.Connector!)
            .ThenInclude(c => c.ServiceDefinition)
            .FirstOrDefaultAsync(p => p.TokenHash == Hash(token), ct);
        var spec = pairing?.Connector?.ServiceDefinition is { } def ? SpecFor(def.Platform) : null;
        if (pairing is null || spec is null || pairing.ExpiresAt < clock.UtcNow
            || pairing.Connector?.UserId != pairing.UserId)
        {
            if (pairing is not null)
            {
                db.ConnectorCookiePairings.Remove(pairing);
                await db.SaveChangesAsync(ct);
            }
            return ConnectorCookiePairingOutcome.InvalidOrExpired;
        }

        if (!TryCookieHeader(spec, cookies, out var cookieHeader))
            return ConnectorCookiePairingOutcome.InvalidCookies;

        // Consume before storing the session. A concurrent second consumer will hit EF's optimistic
        // concurrency check rather than obtaining another valid use of the same bearer token.
        db.ConnectorCookiePairings.Remove(pairing);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConnectorCookiePairingOutcome.InvalidOrExpired;
        }

        await StoreSessionAsync(pairing.ConnectorId, pairing.UserId, cookieHeader, ct);
        return ConnectorCookiePairingOutcome.Completed;
    }

    private CookiePairingSpec? SpecFor(string platform) =>
        connectors.TryGet(platform, out var connector) ? connector.Describe().CookiePairing : null;

    private Task StoreSessionAsync(Guid connectorId, string userId, string cookieHeader, CancellationToken ct) =>
        secrets.SetSecretAsync(
            UserConnectorService.SecretName(connectorId, userId),
            JsonSerializer.Serialize(
                new Dictionary<string, string> { ["CookieHeader"] = cookieHeader },
                Json.Options),
            ct);

    /// <summary>
    /// Builds the connector's <c>Cookie</c> header from exactly the names the platform declares.
    /// Anything else the client sent (analytics, preferences, …) is dropped rather than persisted.
    /// </summary>
    private static bool TryCookieHeader(
        CookiePairingSpec spec,
        IReadOnlyDictionary<string, string>? cookies,
        out string cookieHeader)
    {
        cookieHeader = string.Empty;
        if (spec.CookieNames.Count == 0) return false;

        var parts = new List<string>(spec.CookieNames.Count);
        foreach (var name in spec.CookieNames)
        {
            if (!TryCookie(cookies, name, out var value)) return false;
            parts.Add($"{name}={value}");
        }
        cookieHeader = string.Join("; ", parts);
        return true;
    }

    private static bool TryCookie(
        IReadOnlyDictionary<string, string>? cookies,
        string name,
        out string value)
    {
        value = string.Empty;
        if (cookies is null || !cookies.TryGetValue(name, out var candidate)
            || string.IsNullOrWhiteSpace(candidate) || candidate.Length > 512
            || !CookieValueRegex().IsMatch(candidate))
            return false;
        value = candidate;
        return true;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex(@"^[\x21-\x3A\x3C-\x7E]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CookieValueRegex();
}

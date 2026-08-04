using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

public sealed record ConnectorCookiePairingStart(string PairingToken, DateTimeOffset ExpiresAt);

public enum ConnectorCookiePairingOutcome
{
    Completed,
    InvalidOrExpired,
    InvalidCookies
}

/// <summary>
/// Creates and consumes one-use browser-extension handoffs for connectors that authenticate with
/// website session cookies. The pairing token is a bearer secret and is stored only as a hash.
/// </summary>
public sealed partial class ConnectorCookiePairingService(
    IAppDbContext db,
    ISecretsProvider secrets,
    IClock clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task<ConnectorCookiePairingStart?> StartAsync(
        string userId,
        Guid connectorId,
        CancellationToken ct = default)
    {
        var connector = await db.UserConnectors
            .Include(c => c.ServiceDefinition)
            .FirstOrDefaultAsync(c => c.Id == connectorId && c.UserId == userId, ct);
        if (connector?.ServiceDefinition?.Platform != "FurAffinity") return null;

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
        if (pairing is null || pairing.ExpiresAt < clock.UtcNow
            || pairing.Connector?.UserId != pairing.UserId
            || pairing.Connector.ServiceDefinition?.Platform != "FurAffinity")
        {
            if (pairing is not null)
            {
                db.ConnectorCookiePairings.Remove(pairing);
                await db.SaveChangesAsync(ct);
            }
            return ConnectorCookiePairingOutcome.InvalidOrExpired;
        }

        if (!TryCookie(cookies, "a", out var a) || !TryCookie(cookies, "b", out var b))
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

        var secretJson = JsonSerializer.Serialize(
            new Dictionary<string, string> { ["CookieHeader"] = $"a={a}; b={b}" },
            Json.Options);
        await secrets.SetSecretAsync(
            UserConnectorService.SecretName(pairing.ConnectorId, pairing.UserId),
            secretJson,
            ct);
        return ConnectorCookiePairingOutcome.Completed;
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

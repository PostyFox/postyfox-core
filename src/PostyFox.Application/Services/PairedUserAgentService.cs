using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;

namespace PostyFox.Application.Services;

public sealed record PairedUserAgentSetting(string Platform, string Name, bool UsePairedUserAgent);

/// <summary>
/// Admin control over which cookie-paired platforms replay the pairing browser's User-Agent and
/// which always send PostyFox's default. Users have no say: the flag lives on the service definition.
/// </summary>
public sealed class PairedUserAgentService(IAppDbContext db, IConnectorRegistry connectors)
{
    private const string UserAgentKey = "UserAgent";

    public async Task<IReadOnlyList<PairedUserAgentSetting>> ListAsync(CancellationToken ct = default)
    {
        var defs = await db.ServiceDefinitions.Where(s => s.Enabled).OrderBy(s => s.Name).ToListAsync(ct);
        return defs
            .Where(d => IsCookiePaired(d.Platform))
            .Select(d => new PairedUserAgentSetting(d.Platform, d.Name, d.UsePairedUserAgent))
            .ToList();
    }

    /// <summary>Returns null when the platform is unknown or does not pair with cookies.</summary>
    public async Task<PairedUserAgentSetting?> SetAsync(string platform, bool value, CancellationToken ct = default)
    {
        if (!IsCookiePaired(platform)) return null;
        var def = await db.ServiceDefinitions.FirstOrDefaultAsync(s => s.Platform == platform, ct);
        if (def is null) return null;
        def.UsePairedUserAgent = value;
        await db.SaveChangesAsync(ct);
        return new PairedUserAgentSetting(def.Platform, def.Name, def.UsePairedUserAgent);
    }

    /// <summary>Whether a session for the platform may carry the pairing browser's User-Agent.</summary>
    public static async Task<bool> AllowsPairedAsync(IAppDbContext db, string platform, CancellationToken ct = default) =>
        !await db.ServiceDefinitions.AnyAsync(s => s.Platform == platform && !s.UsePairedUserAgent, ct);

    /// <summary>
    /// Drops the stored User-Agent from a connector secret when the admin has set the platform to the
    /// default. Applied at delivery, so a change also covers sessions paired before it.
    /// </summary>
    public static async Task<string?> ApplyAsync(
        IAppDbContext db, string platform, string? secretJson, CancellationToken ct = default)
    {
        if (secretJson is null || !secretJson.Contains(UserAgentKey, StringComparison.Ordinal)) return secretJson;
        if (await AllowsPairedAsync(db, platform, ct)) return secretJson;
        try
        {
            if (JsonNode.Parse(secretJson) is not JsonObject obj || !obj.Remove(UserAgentKey)) return secretJson;
            return obj.ToJsonString();
        }
        catch (System.Text.Json.JsonException)
        {
            return secretJson;
        }
    }

    private bool IsCookiePaired(string platform) =>
        connectors.TryGet(platform, out var c) && c.Describe().CookiePairing is not null;
}

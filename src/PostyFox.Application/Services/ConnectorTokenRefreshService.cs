using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Options;

namespace PostyFox.Application.Services;

/// <summary>
/// Refreshes connector access tokens ahead of expiry for every enabled connector whose platform
/// resolves to an <see cref="IRefreshableConnector"/> (currently just Instagram's long-lived
/// token). Driven on a schedule by <c>ConnectorTokenRefreshSweeper</c> in PostyFox.Infrastructure —
/// entirely automatic, no user action required.
/// </summary>
public sealed class ConnectorTokenRefreshService(
    IAppDbContext db,
    ISecretsProvider secrets,
    IConnectorRegistry registry,
    IClock clock,
    IOptions<ConnectorRefreshOptions> options,
    ILogger<ConnectorTokenRefreshService> logger)
{
    private readonly ConnectorRefreshOptions _options = options.Value;

    /// <summary>
    /// The <see cref="IRefreshableConnector"/> secret-JSON convention: every refreshable connector's
    /// stored secret carries this field (alongside whatever else the platform needs) so this service
    /// can decide, without knowing the platform, whether a token is due for renewal.
    /// </summary>
    private sealed record ExpiringSecret(DateTimeOffset? ExpiresAt);

    /// <summary>Refreshes every due connector's token. Returns the number successfully refreshed.</summary>
    public async Task<int> RefreshDueAsync(CancellationToken ct = default)
    {
        var candidates = await db.UserConnectors
            .Include(c => c.ServiceDefinition)
            .Where(c => c.Enabled)
            .ToListAsync(ct);

        var refreshed = 0;
        foreach (var uc in candidates)
        {
            if (uc.ServiceDefinition is null) continue;
            if (!registry.TryGet(uc.ServiceDefinition.Platform, out var connector)
                || connector is not IRefreshableConnector refreshable)
                continue;

            var secretKey = UserConnectorService.SecretName(uc.Id, uc.UserId);
            try
            {
                var secretJson = await secrets.GetSecretAsync(secretKey, ct);
                if (secretJson is null) continue;

                var expiresAt = Json.Deserialize<ExpiringSecret>(secretJson)?.ExpiresAt;
                if (expiresAt is null) continue; // nothing to act on — the platform reports no expiry
                if (expiresAt.Value - clock.UtcNow > TimeSpan.FromDays(_options.RefreshWithinDays))
                    continue; // not due yet

                var context = new ConnectorContext(uc.Id, uc.UserId, uc.ConfigJson, secretJson, null);
                var newSecretJson = await refreshable.RefreshTokenAsync(context, ct);
                if (newSecretJson is null)
                {
                    logger.LogWarning(
                        "Token refresh for connector {ConnectorId} ({Platform}) was declined by the platform; it will need reconnecting before {ExpiresAt:o}.",
                        uc.Id, uc.ServiceDefinition.Platform, expiresAt);
                    continue;
                }

                await secrets.SetSecretAsync(secretKey, newSecretJson, ct);
                refreshed++;
            }
            catch (Exception ex)
            {
                // One connector's failure (transient network error, malformed secret, …) must not stop
                // the sweep from reaching the rest.
                logger.LogError(ex, "Token refresh failed for connector {ConnectorId} ({Platform}).",
                    uc.Id, uc.ServiceDefinition.Platform);
            }
        }
        return refreshed;
    }
}

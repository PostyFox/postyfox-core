using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;

namespace PostyFox.Application.Services;

/// <summary>
/// Runs connector operations (auth check, target listing, Telegram login) for a user's
/// configured connector — resolving its config + secret and dispatching to the connector impl.
/// </summary>
public sealed class ConnectorOperationsService(
    IAppDbContext db,
    ISecretStore secrets,
    IConnectorRegistry registry,
    ITelegramGateway telegram)
{
    public async Task<AuthState?> IsAuthenticatedAsync(string userId, Guid connectorId, CancellationToken ct = default)
    {
        var built = await BuildAsync(userId, connectorId, ct);
        if (built is null) return null;
        var (platform, context) = built.Value;
        return registry.TryGet(platform, out var connector)
            ? await connector.IsAuthenticatedAsync(context, ct)
            : new AuthState(false, $"No connector for platform '{platform}'");
    }

    public async Task<IReadOnlyList<ConnectorTarget>?> ListTargetsAsync(string userId, Guid connectorId, CancellationToken ct = default)
    {
        var built = await BuildAsync(userId, connectorId, ct);
        if (built is null) return null;
        var (platform, context) = built.Value;
        return registry.TryGet(platform, out var connector)
            ? await connector.ListTargetsAsync(context, ct)
            : [];
    }

    /// <summary>Advances the Telegram interactive login for the connector's configured phone number.</summary>
    public async Task<TelegramLoginStep?> TelegramLoginAsync(string userId, Guid connectorId, string? value, CancellationToken ct = default)
    {
        var uc = await db.UserConnectors.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == connectorId, ct);
        if (uc is null) return null;
        var phone = Field(uc.ConfigJson, "PhoneNumber");
        if (string.IsNullOrWhiteSpace(phone)) return null;
        return await telegram.LoginAsync(userId, phone!, value, ct);
    }

    /// <summary>
    /// Begins an OAuth "connect" flow for the connector, returning the provider URL to redirect the
    /// user to. The request-token secret is stashed transiently, keyed by the request token, so the
    /// callback can complete the exchange. Returns null if the connector doesn't support OAuth.
    /// </summary>
    public async Task<string?> StartOAuthAsync(string userId, Guid connectorId, string callbackUrl, CancellationToken ct = default)
    {
        var uc = await db.UserConnectors.Include(c => c.ServiceDefinition)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == connectorId, ct);
        if (uc?.ServiceDefinition is null) return null;
        if (!registry.TryGet(uc.ServiceDefinition.Platform, out var connector)
            || connector is not IOAuthConnector oauth
            || !connector.Describe().SupportsOAuth)
            return null;

        var start = await oauth.StartAuthorizationAsync(callbackUrl, ct);
        if (start is null) return null;

        var pending = JsonSerializer.Serialize(new PendingOAuth(userId, connectorId, start.RequestTokenSecret));
        await secrets.SetSecretAsync(PendingKey(start.RequestToken), pending, ct);
        return start.AuthorizeUrl;
    }

    /// <summary>
    /// Completes an OAuth flow from the provider's callback: looks up the pending request by token,
    /// verifies it belongs to the current user, exchanges for the access token, and persists it as
    /// the connector's secret. Returns false if the request is unknown/expired or doesn't match.
    /// </summary>
    public async Task<bool> CompleteOAuthAsync(string userId, string requestToken, string verifier, CancellationToken ct = default)
    {
        var pendingJson = await secrets.GetSecretAsync(PendingKey(requestToken), ct);
        if (pendingJson is null) return false;
        var pending = JsonSerializer.Deserialize<PendingOAuth>(pendingJson);
        if (pending is null || pending.UserId != userId) return false;

        var uc = await db.UserConnectors.Include(c => c.ServiceDefinition)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == pending.ConnectorId, ct);
        if (uc?.ServiceDefinition is null) return false;
        if (!registry.TryGet(uc.ServiceDefinition.Platform, out var connector) || connector is not IOAuthConnector oauth)
            return false;

        var secretJson = await oauth.CompleteAuthorizationAsync(requestToken, pending.RequestTokenSecret, verifier, ct);
        if (secretJson is null) return false;

        await secrets.SetSecretAsync(UserConnectorService.SecretName(pending.ConnectorId, userId), secretJson, ct);
        await secrets.DeleteSecretAsync(PendingKey(requestToken), ct);
        return true;
    }

    private static string PendingKey(string requestToken) => $"oauth-pending-{requestToken}";

    private sealed record PendingOAuth(string UserId, Guid ConnectorId, string RequestTokenSecret);

    private async Task<(string platform, ConnectorContext context)?> BuildAsync(string userId, Guid connectorId, CancellationToken ct)
    {
        var uc = await db.UserConnectors
            .Include(c => c.ServiceDefinition)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == connectorId, ct);
        if (uc?.ServiceDefinition is null) return null;
        var secret = await secrets.GetSecretAsync(UserConnectorService.SecretName(connectorId, userId), ct);
        return (uc.ServiceDefinition.Platform, new ConnectorContext(connectorId, userId, uc.ConfigJson, secret, null));
    }

    private static string? Field(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(name, out var v)
                ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}

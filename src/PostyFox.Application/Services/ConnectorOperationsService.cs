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

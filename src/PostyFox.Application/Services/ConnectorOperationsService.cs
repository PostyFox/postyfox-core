using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;

namespace PostyFox.Application.Services;

/// <summary>
/// Runs connector operations (auth check, target listing, Telegram login) for a user's
/// configured connector: resolving its config + secret and dispatching to the connector impl.
/// </summary>
public sealed class ConnectorOperationsService(
    IAppDbContext db,
    ISecretsProvider secrets,
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

    /// <summary>
    /// Reports a connector's limits (character + attachment caps, image/video size caps). Uses live
    /// per-instance values when the connector supports it (Fediverse), otherwise falls back to the
    /// static descriptor values including any <see cref="MediaSpec"/> declared on the connector.
    /// Returns null only when the connector doesn't exist.
    /// </summary>
    public async Task<ConnectorLimits?> GetLimitsAsync(string userId, Guid connectorId, CancellationToken ct = default)
    {
        var built = await BuildAsync(userId, connectorId, ct);
        if (built is null) return null;
        var (platform, context) = built.Value;
        if (!registry.TryGet(platform, out var connector)) return null;

        if (connector is ILimitsConnector limitsConnector
            && await limitsConnector.GetLimitsAsync(context, ct) is { } live)
            return live;

        // In-process connectors (Discord, Telegram) don't implement ILimitsConnector but declare a
        // MediaSpec on their descriptor. Expose its byte AND dimension caps so the frontend can
        // surface resize warnings before the user submits a post — a small file can still be
        // oversized on width/height alone (see MediaProcessing's image/video normalizers).
        var descriptor = connector.Describe();
        return new ConnectorLimits(
            descriptor.MaxContentLength,
            descriptor.MediaSpec?.MaxAttachments,
            descriptor.MediaSpec?.Image.AllowedMimeTypes,
            descriptor.MediaSpec?.Image.MaxBytes,
            descriptor.MediaSpec?.Video.MaxBytes,
            descriptor.MediaSpec?.Image.MaxWidth,
            descriptor.MediaSpec?.Image.MaxHeight,
            descriptor.MediaSpec?.Video.MaxWidth,
            descriptor.MediaSpec?.Video.MaxHeight);
    }

    /// <summary>
    /// For a given file (size, MIME type, and — for images — pixel dimensions), reports per-connector
    /// whether the file exceeds the platform's size OR dimension limit and will therefore be resized
    /// before delivery, plus the platform's attachment-count cap. A small, high-resolution image can
    /// be well under a platform's byte cap yet still exceed its max width/height (see
    /// MediaProcessing's image normalizer), so both checks matter; a caller with no dimensions (e.g. a
    /// non-image file, or one it couldn't decode) simply skips the dimension check. Takes only the
    /// file's size/type/dimensions, not its bytes, so the frontend can call this the moment a file is
    /// selected — before uploading it — to surface resize / "too many attachments" warnings ahead of
    /// the (potentially slow) upload itself.
    /// </summary>
    public async Task<IReadOnlyList<MediaCheckResultItem>> CheckMediaAsync(
        string userId, IReadOnlyList<Guid> connectorIds, long fileSize, string mimeType,
        int? width = null, int? height = null, CancellationToken ct = default)
    {
        var result = new List<MediaCheckResultItem>(connectorIds.Count);
        foreach (var connectorId in connectorIds)
        {
            var uc = await db.UserConnectors.Include(c => c.ServiceDefinition)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == connectorId, ct);
            if (uc?.ServiceDefinition is null) continue;

            var limits = await GetLimitsAsync(userId, connectorId, ct);
            if (limits is null) continue;

            var isImage = mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            var isVideo = mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

            var exceedsSize =
                (isImage && limits.ImageSizeLimit is { } imgLimit && fileSize > imgLimit) ||
                (isVideo && limits.VideoSizeLimit is { } vidLimit && fileSize > vidLimit);
            var exceedsDimensions =
                (isImage && width is { } iw && limits.ImageMaxWidth is { } imw && iw > imw) ||
                (isImage && height is { } ih && limits.ImageMaxHeight is { } imh && ih > imh) ||
                (isVideo && width is { } vw && limits.VideoMaxWidth is { } vmw && vw > vmw) ||
                (isVideo && height is { } vh && limits.VideoMaxHeight is { } vmh && vh > vmh);

            result.Add(new MediaCheckResultItem(
                connectorId,
                uc.ServiceDefinition.Platform,
                uc.DisplayName,
                exceedsSize || exceedsDimensions,
                limits.ImageSizeLimit,
                limits.VideoSizeLimit,
                limits.MaxMediaAttachments,
                limits.ImageMaxWidth,
                limits.ImageMaxHeight,
                limits.VideoMaxWidth,
                limits.VideoMaxHeight));
        }
        return result;
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

        var start = await oauth.StartAuthorizationAsync(callbackUrl, uc.ConfigJson, ct);
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
        await secrets.TryDeleteSecretAsync(PendingKey(requestToken), ct);
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

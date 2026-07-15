namespace PostyFox.Application.Connectors;

/// <summary>Describes a connector's capabilities and identity.</summary>
public sealed record ConnectorDescriptor(
    string Platform,
    string DisplayName,
    bool SupportsTitle,
    bool SupportsMedia,
    bool SupportsThreads,
    int? MaxContentLength,
    /// <summary>True when the connector exposes an interactive OAuth "connect" flow (see <see cref="IOAuthConnector"/>).</summary>
    bool SupportsOAuth = false);

/// <summary>Result of beginning an OAuth authorization for a connector.</summary>
public sealed record OAuthStart(string AuthorizeUrl, string RequestToken, string RequestTokenSecret);

/// <summary>
/// Optional capability for connectors that support an interactive OAuth flow (e.g. Tumblr's
/// OAuth 1.0a). The token exchange itself lives in the connector implementation; core orchestrates
/// the browser redirect, correlates the callback, and persists the resulting secret.
/// </summary>
public interface IOAuthConnector
{
    /// <summary>Begins authorization; returns the provider URL to send the user to + the request token pair.</summary>
    Task<OAuthStart?> StartAuthorizationAsync(string callbackUrl, CancellationToken ct = default);

    /// <summary>Completes authorization after the user returns; returns the secret JSON to persist.</summary>
    Task<string?> CompleteAuthorizationAsync(string requestToken, string requestTokenSecret, string verifier, CancellationToken ct = default);
}

/// <summary>A destination within a connected account (a Telegram chat, Tumblr blog, etc.).</summary>
public sealed record ConnectorTarget(string Id, string Name);

/// <summary>Authentication state for a user's connector.</summary>
public sealed record AuthState(bool IsAuthenticated, string? Detail = null);

/// <summary>Content rendered for a specific platform, ready to deliver.</summary>
public sealed record RenderedPost(
    string? Title,
    string Body,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MediaRef> Media);

/// <summary>
/// Reference to a stored media object (carried on the post / in the manifest and passed to
/// connectors). Connectors fetch the bytes from the object store themselves; media is never
/// shipped inline. <see cref="Alt"/> is optional accessibility text used where platforms support it.
/// </summary>
public sealed record MediaRef(string Container, string Key, string ContentType, string? Alt = null);

/// <summary>Media bytes resolved from the object store by a connector at delivery time.</summary>
public sealed record MediaContent(string FileName, string ContentType, byte[] Data, string? Alt = null);

/// <summary>Outcome of a delivery attempt.</summary>
public sealed record DeliveryResult(bool Success, string? ExternalId, string? ExternalUrl, string? Error)
{
    public static DeliveryResult Ok(string? externalId, string? externalUrl = null) => new(true, externalId, externalUrl, null);
    public static DeliveryResult Fail(string error) => new(false, null, null, error);
}

/// <summary>
/// Runtime context handed to a connector: the resolved non-secret config and the resolved
/// secret config for the user's connector instance.
/// </summary>
public sealed record ConnectorContext(
    Guid ConnectorId,
    string UserId,
    string ConfigJson,
    string? SecretJson,
    string? TargetId);

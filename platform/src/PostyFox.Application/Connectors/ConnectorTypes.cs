namespace PostyFox.Application.Connectors;

/// <summary>Describes a connector's capabilities and identity.</summary>
public sealed record ConnectorDescriptor(
    string Platform,
    string DisplayName,
    bool SupportsTitle,
    bool SupportsMedia,
    bool SupportsThreads,
    int? MaxContentLength);

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

/// <summary>Reference to a stored media object.</summary>
public sealed record MediaRef(string Container, string Key, string ContentType);

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

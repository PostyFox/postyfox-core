using PostyFox.Domain.Enums;

namespace PostyFox.Application.Connectors;

/// <summary>
/// Everything a PostyFox Connect browser client needs to hand a website session to a connector:
/// where the cookies live, where to send the user to log in, and which cookie names are required.
/// Declaring this server-side (rather than a bare capability flag) keeps site-specific knowledge out
/// of the extension, so a newly supported site needs no extension release.
/// </summary>
public sealed record CookiePairingSpec(
    string SiteUrl,
    string LoginUrl,
    IReadOnlyList<string> CookieNames);

/// <summary>Describes a connector's capabilities and identity.</summary>
public sealed record ConnectorDescriptor(
    string Platform,
    string DisplayName,
    bool SupportsTitle,
    bool SupportsMedia,
    bool SupportsThreads,
    int? MaxContentLength,
    /// <summary>True when the connector exposes an interactive OAuth "connect" flow (see <see cref="IOAuthConnector"/>).</summary>
    bool SupportsOAuth = false,
    /// <summary>
    /// The platform's media constraints. Connectors that normalize media in-process (Discord,
    /// Telegram) declare one so images/video are resized to fit before upload; connectors delegated
    /// to the Node service normalize there and leave this null.
    /// </summary>
    MediaSpec? MediaSpec = null,
    /// <summary>
    /// Set when authentication is handed off from PostyFox Connect browser clients; carries the site
    /// and cookie names the client needs. Null for connectors that authenticate some other way.
    /// </summary>
    CookiePairingSpec? CookiePairing = null,
    /// <summary>True when authored content ratings can be represented by the platform.</summary>
    bool SupportsRating = false,
    /// <summary>True when delivery must include an explicit author-supplied content rating.</summary>
    bool RequiresRating = false,
    /// <summary>
    /// Field-descriptor JSON (same format as <see cref="Domain.Entities.ServiceDefinition.ConfigSchema"/>)
    /// for choices the platform takes <em>per submission</em> rather than per account — FurAffinity's
    /// category, species, gender and gallery folders. The compose form renders these per selected
    /// target; the values are stored on the <see cref="Domain.Entities.PostTarget"/> and applied over
    /// the connector's config at delivery. Null when the platform has no per-submission choices.
    /// </summary>
    string? PostOptionsSchema = null,
    /// <summary>
    /// True when a single connector login can fan out to several independently selectable delivery
    /// destinations (e.g. one Telegram MTProto login reaching many chats/channels) — see
    /// <see cref="Domain.Entities.ConnectorDestination"/>. The compose UI then lets the author pick
    /// individual exposed destinations rather than the connector itself. False (the default) keeps
    /// the legacy 1:1 connector-to-destination behaviour every other platform uses.
    /// </summary>
    bool SupportsMultipleTargets = false,
    /// <summary>
    /// True when the platform has a native tags field/mechanism the connector sends tags through
    /// directly (Tumblr, FurAffinity). False means the platform has no such field — tags can only
    /// reach it by being woven into the body text, either at an author-placed <c>{tags}</c> template
    /// token or, failing that, appended to the end. The default (true) matches most platforms having
    /// been built with an assumed tags field; every connector explicitly declares this.
    /// </summary>
    bool SupportsTags = true,
    /// <summary>
    /// True when a delivery must include at least one tag (FurAffinity). Forces "include tags" on for
    /// every post to this platform — the compose UI cannot turn it off, and intake rejects a post
    /// with no tags for a target that requires them.
    /// </summary>
    bool RequiresTags = false,
    /// <summary>
    /// True when the platform can hide a post's body behind a click-to-reveal warning (Mastodon-style
    /// "content warning" / "CW", surfaced as <c>spoiler_text</c> by every Fediverse driver this app
    /// uses). Purely informational — drives the "Content warning" capability badge in the connector
    /// list; the actual per-submission text lives in <see cref="PostOptionsSchema"/> like FurAffinity's
    /// category/species/gender, so it is authored per post rather than assumed from the title.
    /// </summary>
    bool SupportsContentWarning = false)
{
    /// <summary>True when authentication is handed off from PostyFox Connect browser clients.</summary>
    public bool SupportsCookiePairing => CookiePairing is not null;
}

/// <summary>Result of beginning an OAuth authorization for a connector.</summary>
public sealed record OAuthStart(string AuthorizeUrl, string RequestToken, string RequestTokenSecret);

/// <summary>
/// Optional capability for connectors that support an interactive OAuth flow (e.g. Tumblr's
/// OAuth 1.0a). The token exchange itself lives in the connector implementation; core orchestrates
/// the browser redirect, correlates the callback, and persists the resulting secret.
/// </summary>
public interface IOAuthConnector
{
    /// <summary>
    /// Begins authorization; returns the provider URL to send the user to + the request token pair.
    /// <paramref name="configJson"/> carries the connector's non-secret config for providers whose
    /// authorization is instance-scoped (e.g. Fediverse instance URL); OAuth1 providers ignore it.
    /// </summary>
    Task<OAuthStart?> StartAuthorizationAsync(string callbackUrl, string? configJson, CancellationToken ct = default);

    /// <summary>Completes authorization after the user returns; returns the secret JSON to persist.</summary>
    Task<string?> CompleteAuthorizationAsync(string requestToken, string requestTokenSecret, string verifier, CancellationToken ct = default);
}

/// <summary>A destination within a connected account (a Telegram chat, Tumblr blog, etc.).</summary>
public sealed record ConnectorTarget(string Id, string Name);

/// <summary>
/// Live, per-connector-instance limits. Fediverse instances each configure their own caps, so these
/// are read from the instance rather than assumed per platform. Null means "not reported".
/// <see cref="ImageSizeLimit"/> / <see cref="VideoSizeLimit"/> are in bytes.
/// </summary>
public sealed record ConnectorLimits(
    int? MaxContentLength,
    int? MaxMediaAttachments,
    IReadOnlyList<string>? SupportedMimeTypes = null,
    long? ImageSizeLimit = null,
    long? VideoSizeLimit = null);

/// <summary>
/// Optional capability for connectors that can report live per-instance limits (e.g. Fediverse
/// character/attachment caps from the instance config). Connectors without it fall back to the
/// static <see cref="ConnectorDescriptor.MaxContentLength"/>.
/// </summary>
public interface ILimitsConnector
{
    Task<ConnectorLimits?> GetLimitsAsync(ConnectorContext context, CancellationToken ct = default);
}

/// <summary>Authentication state for a user's connector.</summary>
public sealed record AuthState(bool IsAuthenticated, string? Detail = null);

/// <summary>Content rendered for a specific platform, ready to deliver.</summary>
public sealed record RenderedPost(
    string? Title,
    string Body,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MediaRef> Media,
    ContentRating? Rating = null,
    /// <summary>
    /// How many tags were dropped from an inline <c>#hashtag</c> interpolation (see
    /// <see cref="ConnectorDescriptor.SupportsTags"/> = false) to keep the rendered body within the
    /// platform's <see cref="ConnectorDescriptor.MaxContentLength"/>. Always 0 for platforms with a
    /// native tags field, or when no trimming was needed.
    /// </summary>
    int TagsOmitted = 0);

/// <summary>
/// Reference to a stored media object (carried on the post / in the manifest and passed to
/// connectors). Connectors fetch the bytes from the object store themselves; media is never
/// shipped inline. <see cref="Alt"/> is optional accessibility text used where platforms support it.
/// <see cref="IsDefault"/> marks the author's chosen "primary" image when a post carries several —
/// platforms limited to a single attachment (FurAffinity) submit this one instead of rejecting a
/// multi-image post; platforms that accept multiple images ignore the flag and send everything.
/// </summary>
public sealed record MediaRef(string Container, string Key, string ContentType, string? Alt = null, bool IsDefault = false);

/// <summary>Media bytes resolved from the object store by a connector at delivery time.</summary>
public sealed record MediaContent(string FileName, string ContentType, byte[] Data, string? Alt = null);

public static class MediaContainers
{
    public const string Media = "media";
}

public static class MediaRefValidation
{
    /// <summary>True when a <see cref="MediaRef"/> points at an object this user actually owns.</summary>
    public static bool IsOwnedBy(this MediaRef media, string userId) =>
        media.Container == MediaContainers.Media && media.Key.StartsWith($"{userId}/", StringComparison.Ordinal);
}

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

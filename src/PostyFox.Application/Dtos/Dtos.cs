using PostyFox.Application.Connectors;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Dtos;

public sealed record ApiKeyDto(Guid Id, string Prefix, string? Name, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

/// <summary>Returned once at creation; the plaintext key is never retrievable again.</summary>
public sealed record ApiKeyCreatedDto(Guid Id, string ApiKey, string Prefix);

public sealed record ServiceDefinitionDto(
    string Id,
    string Name,
    bool Enabled,
    string ConfigSchema,
    string? SecureConfigSchema,
    string Platform,
    bool SupportsTitle,
    bool SupportsMedia,
    bool SupportsThreads,
    int? MaxContentLength,
    bool SupportsOAuth,
    bool SupportsCookiePairing,
    bool SupportsRating,
    bool RequiresRating,
    /// <summary>
    /// Field descriptors for choices this platform takes per submission rather than per account (see
    /// <see cref="Connectors.ConnectorDescriptor.PostOptionsSchema"/>). Same format as
    /// <see cref="ConfigSchema"/>; null when the platform has none. Rendered by the compose form once
    /// per selected target, and submitted as <see cref="CreatePostRequest.TargetOptions"/>.
    /// </summary>
    string? PostOptionsSchema);

public sealed record UserConnectorDto(Guid Id, string ServiceDefinitionId, string Platform, string DisplayName, string ConfigJson, bool Enabled);

/// <summary>
/// A site the signed-in user can hand a browser session to. One entry per cookie-pairing platform:
/// <see cref="ConnectorId"/> names the connector to update, or is null when the user has none yet
/// (pairing then creates one). The cookie details let a browser client read the right cookies and
/// point the user at the right login page without hard-coding either.
/// </summary>
public sealed record CookiePairingTargetDto(
    Guid? ConnectorId,
    string ServiceDefinitionId,
    string Platform,
    string DisplayName,
    string SiteUrl,
    string LoginUrl,
    IReadOnlyList<string> CookieNames);

public sealed record UserConnectorUpsertRequest(
    Guid? Id,
    string ServiceDefinitionId,
    string DisplayName,
    string ConfigJson,
    string? SecureConfigJson,
    bool Enabled);

public sealed record TemplateDto(Guid Id, string Title, string MarkdownBody);
public sealed record TemplateUpsertRequest(Guid? Id, string Title, string MarkdownBody);

public sealed record CreatePostRequest(
    IReadOnlyList<Guid> Targets,
    string? Title,
    string? Description,
    string? HtmlDescription,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<MediaRef>? Media,
    Guid? TemplateId,
    IReadOnlyDictionary<string, string>? Variables,
    DateTimeOffset? PostAt,
    ContentRating? Rating = null,
    /// <summary>
    /// Per-submission platform choices, keyed by target connector id — FurAffinity's category,
    /// species, gender and folders. Validated against that platform's
    /// <see cref="ServiceDefinitionDto.PostOptionsSchema"/>; anything it does not declare is dropped.
    /// Entries for connectors outside <see cref="Targets"/> are ignored.
    /// </summary>
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, string>>? TargetOptions = null);

public sealed record CreatePostResponse(Guid PostId, PostRootStatus RootStatus);

public sealed record PostTargetStatusDto(Guid TargetId, string Platform, TargetStatus Status, string? ExternalId, string? ExternalUrl, string? Error, int Attempts);
public sealed record PostStatusDto(Guid PostId, PostRootStatus RootStatus, IReadOnlyList<PostTargetStatusDto> Targets);

/// <summary>The user-authored content of a post, shaped to re-seed the compose form ("post again").</summary>
public sealed record PostContentDto(
    string? Title,
    string? Description,
    string? HtmlDescription,
    IReadOnlyList<string> Tags,
    IReadOnlyList<MediaRef> Media,
    Guid? TemplateId,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<Guid> ConnectorIds,
    DateTimeOffset? PostAt,
    ContentRating? Rating,
    /// <summary>
    /// The per-submission platform choices this post was created with, keyed by connector id, so
    /// "post again" re-seeds them rather than silently reverting to platform defaults.
    /// </summary>
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, string>> TargetOptions);

/// <summary>Lightweight row for the post list / activity view (no per-target detail).</summary>
public sealed record PostSummaryDto(
    Guid PostId,
    PostRootStatus RootStatus,
    string Title,
    IReadOnlyList<string> Platforms,
    int TargetCount,
    int DeliveredCount,
    int FailedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PostAt);

/// <summary>Request body for the media-check endpoint.</summary>
public sealed record MediaCheckRequest(
    IReadOnlyList<Guid> ConnectorIds,
    long FileSize,
    string MimeType);

/// <summary>
/// Per-connector result of a media pre-flight check. <see cref="WillResize"/> is true when the
/// file exceeds the connector's size limit and the backend will resize/transcode it before delivery.
/// <see cref="ImageSizeLimit"/> and <see cref="VideoSizeLimit"/> are in bytes; null means the
/// connector reports no cap for that media type.
/// </summary>
public sealed record MediaCheckResultItem(
    Guid ConnectorId,
    string Platform,
    string DisplayName,
    bool WillResize,
    long? ImageSizeLimit,
    long? VideoSizeLimit);

public sealed record TriggerRegistrationRequest(
    string SourceType,
    string ExternalAccount,
    Guid? TemplateId,
    Guid TargetConnectorId,
    int NotifyFrequencyHrs);

public sealed record TriggerDto(
    Guid Id,
    string SourceType,
    string ExternalAccount,
    Guid? TemplateId,
    Guid? TargetConnectorId,
    int NotifyFrequencyHrs,
    DateTimeOffset? LastFiredAt);

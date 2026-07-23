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
    bool SupportsOAuth);

public sealed record UserConnectorDto(Guid Id, string ServiceDefinitionId, string Platform, string DisplayName, string ConfigJson, bool Enabled);

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
    DateTimeOffset? PostAt);

public sealed record CreatePostResponse(Guid PostId, PostRootStatus RootStatus);

public sealed record PostTargetStatusDto(Guid TargetId, string Platform, TargetStatus Status, string? ExternalId, string? ExternalUrl, string? Error, int Attempts);
public sealed record PostStatusDto(Guid PostId, PostRootStatus RootStatus, IReadOnlyList<PostTargetStatusDto> Targets);

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

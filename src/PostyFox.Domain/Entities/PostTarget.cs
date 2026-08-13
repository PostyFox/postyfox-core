using PostyFox.Domain.Enums;

namespace PostyFox.Domain.Entities;

/// <summary>A single platform delivery of a <see cref="Post"/>.</summary>
public class PostTarget
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid? ConnectorId { get; set; }
    public string Platform { get; set; } = string.Empty;
    /// <summary>
    /// The specific destination within the connector's account to deliver to (e.g. a Telegram chat
    /// id), when the target was selected via a <see cref="ConnectorDestination"/> rather than the
    /// connector's single default destination. Null means "use the connector's own config" (the
    /// legacy 1:1 connector-to-destination behaviour every other platform still uses).
    /// </summary>
    public string? TargetId { get; set; }
    /// <summary>Human-readable label for <see cref="TargetId"/>, captured at selection time for display.</summary>
    public string? TargetName { get; set; }
    /// <summary>
    /// The author's per-submission choices for this platform (FurAffinity's category, species, gender,
    /// gallery folders), as declared by its connector descriptor's <c>PostOptionsSchema</c>. Applied
    /// over the connector's own config at delivery; <c>{}</c> means "use the platform's defaults".
    /// </summary>
    public string OptionsJson { get; set; } = "{}";
    public string? RenderedContentJson { get; set; }
    public TargetStatus Status { get; set; } = TargetStatus.Queued;
    public string? ExternalId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Post? Post { get; set; }
}

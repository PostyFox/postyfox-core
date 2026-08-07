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

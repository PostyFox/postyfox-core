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
    /// <summary>
    /// Whether tags should be included for this target: optional for every platform (even ones with
    /// no native tags field, which get them woven into the body instead; see
    /// <see cref="Connectors.ConnectorDescriptor.SupportsTags"/>), and forced true for platforms that
    /// declare <see cref="Connectors.ConnectorDescriptor.RequiresTags"/>. Otherwise defaults to the
    /// owning connector's configured <see cref="UserConnector.DefaultIncludeTags"/> unless the author
    /// overrides the choice for this post.
    /// </summary>
    public bool IncludeTags { get; set; } = true;
    /// <summary>
    /// The audience/content rating for this target, on platforms that can represent one (see
    /// <see cref="Connectors.ConnectorDescriptor.SupportsRating"/>). Unlike <see cref="IncludeTags"/>
    /// this carries exactly what the author chose at compose time (including a connector's own
    /// <see cref="UserConnector.DefaultRating"/> pre-fill, if that's what they left in place): intake
    /// stores it as sent and applies no fallback of its own. Null on a platform that doesn't support
    /// ratings, or when the author never set one.
    /// </summary>
    public ContentRating? Rating { get; set; }
    public string? RenderedContentJson { get; set; }
    public TargetStatus Status { get; set; } = TargetStatus.Queued;
    public string? ExternalId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Set the moment <see cref="PostSchedulerService"/> claims this target and publishes its
    /// <c>GenerateTargetCommand</c> (once <see cref="Post.PostAt"/> is due). Null while the target is
    /// still waiting on its schedule (or was never scheduled: immediate posts publish straight away
    /// at intake without going through the scheduler at all). Acts as a claim marker so concurrent
    /// scheduler passes/replicas can't both publish the same due target.
    /// </summary>
    public DateTimeOffset? GenerationEnqueuedAt { get; set; }

    public Post? Post { get; set; }

    /// <summary>Post-delivery automation rules requested for this target (issue #323). See <see cref="PostTargetAutomation"/>.</summary>
    public List<PostTargetAutomation> Automations { get; set; } = new();
}

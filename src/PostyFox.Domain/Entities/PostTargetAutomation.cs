using PostyFox.Domain.Enums;

namespace PostyFox.Domain.Entities;

/// <summary>
/// A "do X to this target after Y hours" rule (issue #323): repost/reblog it, or delete it from the
/// platform, once it's been delivered that long. Created alongside its <see cref="PostTarget"/> at
/// intake, but only becomes due once that target actually delivers — see <see cref="DueAt"/>.
/// </summary>
public class PostTargetAutomation
{
    public Guid Id { get; set; }
    public Guid PostTargetId { get; set; }
    public AutomationAction Action { get; set; }
    public double DelayHours { get; set; }

    /// <summary>
    /// When this becomes due: the target's delivery time plus <see cref="DelayHours"/>, set by
    /// <c>DeliverTargetHandler</c> the moment the target actually delivers. Null until then: an
    /// automation on a still-in-flight (or not-yet-due scheduled) post has no fixed due time yet, so
    /// there is nothing for the sweeper to compare against.
    /// </summary>
    public DateTimeOffset? DueAt { get; set; }

    /// <summary>
    /// Set the moment the automation sweeper claims this rule and publishes its
    /// <c>ExecuteAutomationCommand</c>. Acts as a claim marker so concurrent sweeper passes/replicas
    /// can't both execute the same rule — mirrors <see cref="PostTarget.GenerationEnqueuedAt"/>.
    /// </summary>
    public DateTimeOffset? EnqueuedAt { get; set; }

    public AutomationStatus Status { get; set; } = AutomationStatus.Pending;
    public string? Error { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public PostTarget? PostTarget { get; set; }
}

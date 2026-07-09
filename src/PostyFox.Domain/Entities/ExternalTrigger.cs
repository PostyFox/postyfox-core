namespace PostyFox.Domain.Entities;

/// <summary>
/// A user's registration of interest in an external event (e.g. a Twitch channel going
/// live) that should fire a templated post to a target when it occurs.
/// </summary>
public class ExternalTrigger
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;       // e.g. "Twitch"
    public string ExternalAccount { get; set; } = string.Empty;  // e.g. Twitch user id
    public Guid? TemplateId { get; set; }
    public Guid? TargetConnectorId { get; set; }
    public int NotifyFrequencyHrs { get; set; }
    public DateTimeOffset? LastFiredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

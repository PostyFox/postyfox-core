namespace PostyFox.Domain.Entities;

/// <summary>Records processed external webhook message ids to drop duplicates.</summary>
public class WebhookDedupe
{
    public string Source { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public DateTimeOffset SeenAt { get; set; }
}

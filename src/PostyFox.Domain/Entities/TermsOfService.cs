namespace PostyFox.Domain.Entities;

/// <summary>
/// One published version of the instance's terms of service (issue #417). Every admin publish adds a
/// new row, so the highest <see cref="Version"/> is current and users must accept it again. A current
/// version with empty <see cref="Content"/> means no terms are in force.
/// </summary>
public class TermsOfService
{
    public int Version { get; set; }

    /// <summary>Markdown shown to users before they accept.</summary>
    public string Content { get; set; } = string.Empty;
    public string PublishedByUserId { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
}

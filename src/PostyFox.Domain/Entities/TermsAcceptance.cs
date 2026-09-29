namespace PostyFox.Domain.Entities;

/// <summary>Records that a user accepted a specific <see cref="TermsOfService"/> version, and when.</summary>
public class TermsAcceptance
{
    public string UserId { get; set; } = string.Empty;
    public int TermsVersion { get; set; }
    public DateTimeOffset AcceptedAt { get; set; }
}

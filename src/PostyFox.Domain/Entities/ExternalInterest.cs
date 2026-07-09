namespace PostyFox.Domain.Entities;

/// <summary>
/// Inbound fan-out index: maps an external source account to the users interested in it,
/// so an incoming webhook can be dispatched to all relevant triggers.
/// Composite key: (SourceType, ExternalAccount, UserId).
/// </summary>
public class ExternalInterest
{
    public string SourceType { get; set; } = string.Empty;
    public string ExternalAccount { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

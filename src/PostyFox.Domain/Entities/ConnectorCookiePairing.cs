namespace PostyFox.Domain.Entities;

/// <summary>
/// Short-lived, one-use handoff from a user's browser extension to a scraper-backed connector.
/// Only the SHA-256 hash of the bearer pairing token is persisted.
/// </summary>
public class ConnectorCookiePairing
{
    public string TokenHash { get; set; } = string.Empty;
    public Guid ConnectorId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public UserConnector? Connector { get; set; }
}

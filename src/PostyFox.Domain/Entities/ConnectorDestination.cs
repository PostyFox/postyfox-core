namespace PostyFox.Domain.Entities;

/// <summary>
/// A specific destination within a connected account that the user has chosen to expose for
/// posting — e.g. one Telegram chat/channel reachable from a single MTProto login. Lets a
/// connector that authenticates once (<see cref="UserConnector"/>) fan out to several delivery
/// destinations without the user creating a duplicate connector/login per destination.
/// <see cref="ExternalId"/> is the platform's own identifier for the destination (a Telegram chat
/// id); <see cref="Name"/> is a human-readable label captured at the time it was exposed (kept in
/// sync when the user re-selects, so a renamed channel doesn't show a stale name).
/// </summary>
public class ConnectorDestination
{
    public Guid Id { get; set; }
    public Guid ConnectorId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public UserConnector? Connector { get; set; }
}

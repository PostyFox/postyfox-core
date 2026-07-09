namespace PostyFox.Domain.Entities;

/// <summary>
/// A machine-to-machine API key. The secret itself is never stored; only a salted
/// hash (<see cref="KeyHash"/>). <see cref="Prefix"/> is a short non-secret fragment
/// used for lookup and display.
/// </summary>
public class ApiKey
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public bool IsActive => RevokedAt is null;
}

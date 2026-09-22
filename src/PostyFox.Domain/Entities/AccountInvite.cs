using PostyFox.Domain.Enums;

namespace PostyFox.Domain.Entities;

/// <summary>
/// An invitation for another OIDC-authenticated person to manage the inviting user's account
/// (issue #409). Delivered by email to <see cref="InviteeEmail"/>; accepting it requires signing
/// in via OIDC with a matching email address, which creates an <see cref="AccountMember"/> row.
/// </summary>
public class AccountInvite
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Snapshot of the owner's own OIDC email at invite time, shown to the invitee before they accept.</summary>
    public string OwnerEmail { get; set; } = string.Empty;
    public string InviteeEmail { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash of the bearer token embedded in the invite link; never stored in the clear.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>First 8 chars of the token, in the clear, for the candidate lookup (same approach as ApiKey).</summary>
    public string TokenPrefix { get; set; } = string.Empty;

    public InviteStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? AcceptedByUserId { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>True once <see cref="ExpiresAt"/> has passed, computed rather than swept by a background job.</summary>
    public bool IsExpired(DateTimeOffset now) => Status == InviteStatus.Pending && now >= ExpiresAt;
}

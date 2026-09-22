namespace PostyFox.Domain.Entities;

/// <summary>
/// Grants <see cref="MemberUserId"/> delegated access to act as <see cref="OwnerUserId"/>'s account
/// (issue #409), created when that user accepts an <see cref="AccountInvite"/>. Composite key
/// (OwnerUserId, MemberUserId): a member may hold access to several owners' accounts, and an owner
/// may grant access to several members. Access is full delegation, not a permission subset — see
/// <see cref="PostyFox.Web.Auth"/>'s act-as claims transformation, which is what actually resolves
/// "who am I acting as" for every request.
/// </summary>
public class AccountMember
{
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Snapshot of the owner's OIDC email at accept time, for the member's "switch account" list.</summary>
    public string OwnerEmail { get; set; } = string.Empty;
    public string MemberUserId { get; set; } = string.Empty;

    /// <summary>Snapshot of the member's OIDC email at accept time, for the owner's "who has access" list.</summary>
    public string MemberEmail { get; set; } = string.Empty;
    public Guid InviteId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

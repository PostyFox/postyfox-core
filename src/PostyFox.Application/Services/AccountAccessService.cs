using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Services;

/// <summary>
/// Account delegation (issue #409): lets a user invite someone else, by email, to manage their
/// account, cross-checked against the OIDC email the invitee actually signs in with. Acceptance
/// creates an <see cref="AccountMember"/> row; who a request is actually treated as (the invited
/// member acting as the owner) is resolved separately, per-request, by the act-as claims
/// transformation in <c>PostyFox.Web.Auth</c> — this service only manages the invite/membership
/// records themselves.
/// </summary>
public sealed class AccountAccessService(IAppDbContext db, IApiKeyHasher hasher, IClock clock, IEmailSender emailSender, ILogger<AccountAccessService> logger)
{
    private const string TokenChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
    private const int TokenLength = 40;
    private const int TokenPrefixLength = 8;
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    public async Task<AccountInviteDto> InviteAsync(string ownerUserId, string ownerEmail, string inviteeEmail, string acceptUrlBase, CancellationToken ct = default)
    {
        ownerEmail = Normalize(ownerEmail);
        inviteeEmail = Normalize(inviteeEmail);

        if (string.IsNullOrEmpty(ownerEmail))
            throw new ConnectorValidationException("Your account has no verified email address, so invites can't be cross-checked. Sign in with an OIDC identity that provides one.");
        if (string.IsNullOrEmpty(inviteeEmail) || !inviteeEmail.Contains('@'))
            throw new ConnectorValidationException("Enter a valid email address to invite.");
        if (inviteeEmail == ownerEmail)
            throw new ConnectorValidationException("You can't invite yourself.");

        var now = clock.UtcNow;
        var token = GenerateToken();
        var invite = new AccountInvite
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            OwnerEmail = ownerEmail,
            InviteeEmail = inviteeEmail,
            TokenPrefix = token[..TokenPrefixLength],
            TokenHash = hasher.Hash(token),
            Status = InviteStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now + InviteLifetime
        };
        db.AccountInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        var link = $"{acceptUrlBase.TrimEnd('/')}/access?token={Uri.EscapeDataString(token)}";
        try
        {
            await emailSender.SendAsync(
                inviteeEmail,
                $"{ownerEmail} invited you to help manage their PostyFox account",
                $"""
                {ownerEmail} has invited you to help manage their PostyFox account (posting, connectors, and more).

                Accept the invite (expires {invite.ExpiresAt:yyyy-MM-dd}): {link}

                If you weren't expecting this, you can ignore this email.
                """,
                ct);
        }
        catch (Exception ex)
        {
            // The invite record is still valid and shows up under "pending" for the invitee once they
            // sign in, so a delivery failure shouldn't fail the request — just the automated email.
            logger.LogWarning(ex, "Failed to send invite email for invite {InviteId}", invite.Id);
        }

        return ToDto(invite, now);
    }

    public async Task<IReadOnlyList<AccountInviteDto>> ListSentAsync(string ownerUserId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var invites = await db.AccountInvites.Where(i => i.OwnerUserId == ownerUserId).ToListAsync(ct);
        return invites.OrderByDescending(i => i.CreatedAt).Select(i => ToDto(i, now)).ToList();
    }

    public async Task<IReadOnlyList<AccountInviteDto>> ListPendingForEmailAsync(string email, CancellationToken ct = default)
    {
        email = Normalize(email);
        var now = clock.UtcNow;
        var invites = await db.AccountInvites
            .Where(i => i.InviteeEmail == email && i.Status == InviteStatus.Pending)
            .ToListAsync(ct);
        return invites
            .Where(i => !i.IsExpired(now))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => ToDto(i, now))
            .ToList();
    }

    public async Task<bool> RevokeAsync(string ownerUserId, Guid inviteId, CancellationToken ct = default)
    {
        var invite = await db.AccountInvites.FirstOrDefaultAsync(i => i.OwnerUserId == ownerUserId && i.Id == inviteId, ct);
        if (invite is null || invite.Status != InviteStatus.Pending) return false;
        invite.Status = InviteStatus.Revoked;
        invite.RevokedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Accepts an invite by its emailed token (the link's landing page). The accepting user's own
    /// OIDC email must match the invite's addressee, case-insensitively — the cross-check the ticket
    /// asks for.
    /// </summary>
    public async Task<AccountAcceptResult> AcceptAsync(string token, string acceptingUserId, string acceptingUserEmail, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length < TokenPrefixLength) return AccountAcceptResult.NotFound;

        var prefix = token[..TokenPrefixLength];
        var candidates = await db.AccountInvites
            .Where(i => i.TokenPrefix == prefix && i.Status == InviteStatus.Pending)
            .ToListAsync(ct);
        var invite = candidates.FirstOrDefault(i => hasher.Verify(token, i.TokenHash));
        if (invite is null) return AccountAcceptResult.NotFound;

        return await AcceptCoreAsync(invite, acceptingUserId, acceptingUserEmail, ct);
    }

    /// <summary>
    /// Accepts an invite by id, for the "invitations waiting for you" list (<see cref="ListPendingForEmailAsync"/>),
    /// so accepting doesn't require having the original email to hand. Just as safe as the token path:
    /// the same email cross-check applies, and the id alone grants nothing without it.
    /// </summary>
    public async Task<AccountAcceptResult> AcceptByIdAsync(Guid inviteId, string acceptingUserId, string acceptingUserEmail, CancellationToken ct = default)
    {
        var invite = await db.AccountInvites.FirstOrDefaultAsync(i => i.Id == inviteId && i.Status == InviteStatus.Pending, ct);
        if (invite is null) return AccountAcceptResult.NotFound;

        return await AcceptCoreAsync(invite, acceptingUserId, acceptingUserEmail, ct);
    }

    private async Task<AccountAcceptResult> AcceptCoreAsync(AccountInvite invite, string acceptingUserId, string acceptingUserEmail, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (invite.IsExpired(now)) return AccountAcceptResult.Expired;

        acceptingUserEmail = Normalize(acceptingUserEmail);
        if (string.IsNullOrEmpty(acceptingUserEmail) || acceptingUserEmail != invite.InviteeEmail)
            return AccountAcceptResult.EmailMismatch;

        if (acceptingUserId == invite.OwnerUserId)
            return AccountAcceptResult.NotFound; // can't accept your own invite

        invite.Status = InviteStatus.Accepted;
        invite.AcceptedByUserId = acceptingUserId;
        invite.AcceptedAt = now;

        var existing = await db.AccountMembers
            .FirstOrDefaultAsync(m => m.OwnerUserId == invite.OwnerUserId && m.MemberUserId == acceptingUserId, ct);
        if (existing is null)
        {
            db.AccountMembers.Add(new AccountMember
            {
                OwnerUserId = invite.OwnerUserId,
                OwnerEmail = invite.OwnerEmail,
                MemberUserId = acceptingUserId,
                MemberEmail = acceptingUserEmail,
                InviteId = invite.Id,
                CreatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);
        return AccountAcceptResult.Accepted;
    }

    /// <summary>Accounts the given user can act as: themselves, plus any owner who's granted them access.</summary>
    public async Task<IReadOnlyList<AccountAccessDto>> ListAccessibleAccountsAsync(string userId, string? userEmail, CancellationToken ct = default)
    {
        var memberships = await db.AccountMembers.Where(m => m.MemberUserId == userId).ToListAsync(ct);
        var result = new List<AccountAccessDto> { new(userId, userEmail, true) };
        result.AddRange(memberships.OrderBy(m => m.OwnerEmail).Select(m => new AccountAccessDto(m.OwnerUserId, m.OwnerEmail, false)));
        return result;
    }

    /// <summary>Members with delegated access to the given owner's account, for the "who has access" list.</summary>
    public async Task<IReadOnlyList<AccountMemberDto>> ListMembersAsync(string ownerUserId, CancellationToken ct = default)
    {
        var members = await db.AccountMembers.Where(m => m.OwnerUserId == ownerUserId).ToListAsync(ct);
        return members.OrderBy(m => m.MemberEmail).Select(m => new AccountMemberDto(m.MemberUserId, m.MemberEmail, m.CreatedAt)).ToList();
    }

    public async Task<bool> RemoveMemberAsync(string ownerUserId, string memberUserId, CancellationToken ct = default)
    {
        var member = await db.AccountMembers.FirstOrDefaultAsync(m => m.OwnerUserId == ownerUserId && m.MemberUserId == memberUserId, ct);
        if (member is null) return false;
        db.AccountMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string Normalize(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    private static AccountInviteDto ToDto(AccountInvite i, DateTimeOffset now) =>
        new(i.Id, i.OwnerEmail, i.InviteeEmail, i.Status, i.CreatedAt, i.ExpiresAt, i.AcceptedAt, i.IsExpired(now));

    private static string GenerateToken()
    {
        var chars = new char[TokenLength];
        for (var i = 0; i < TokenLength; i++)
            chars[i] = TokenChars[RandomNumberGenerator.GetInt32(TokenChars.Length)];
        return new string(chars);
    }
}

public enum AccountAcceptResult
{
    Accepted,
    NotFound,
    Expired,
    EmailMismatch
}

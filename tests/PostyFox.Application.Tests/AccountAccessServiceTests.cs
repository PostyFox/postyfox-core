using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Security;
using PostyFox.Application.Services;
using PostyFox.Application.Tests.Support;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Application.Tests;

public class AccountAccessServiceTests
{
    private static (AccountAccessService svc, FakeEmailSender email, FixedClock clock) NewService(TestDbContext db)
    {
        var clock = new FixedClock(DateTimeOffset.UnixEpoch);
        var email = new FakeEmailSender();
        var svc = new AccountAccessService(db, new ApiKeyHasher(), clock, email, NullLogger<AccountAccessService>.Instance);
        return (svc, email, clock);
    }

    /// <summary>
    /// The real token only ever exists in the emailed link, never in the DB (only its hash does), so
    /// tests recover it the same way a real invitee would: read it out of the invite email.
    /// </summary>
    private static string TokenFromEmail(FakeEmailSender email) =>
        Regex.Match(email.Sent.Last().Body, "token=([^\\s]+)").Groups[1].Value;

    [Fact]
    public async Task Invite_persists_and_emails_the_invitee()
    {
        using var db = TestDbContext.Create();
        var (svc, email, _) = NewService(db);

        var invite = await svc.InviteAsync("owner-1", "Owner@Example.com", "Friend@Example.com", "https://app.postyfox.test");

        Assert.Equal("owner@example.com", invite.OwnerEmail);
        Assert.Equal("friend@example.com", invite.InviteeEmail);
        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.False(invite.IsExpired);
        var sent = Assert.Single(email.Sent);
        Assert.Equal("friend@example.com", sent.To);
        Assert.Contains("/access?token=", sent.Body);
    }

    [Fact]
    public async Task Invite_still_created_when_email_delivery_fails()
    {
        using var db = TestDbContext.Create();
        var (svc, email, _) = NewService(db);
        email.ThrowOnSend = true;

        var invite = await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");

        Assert.Equal(InviteStatus.Pending, invite.Status);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task Invite_rejects_self_invite()
    {
        using var db = TestDbContext.Create();
        var (svc, _, _) = NewService(db);

        await Assert.ThrowsAsync<ConnectorValidationException>(
            () => svc.InviteAsync("owner-1", "owner@example.com", "Owner@Example.com", "https://app.postyfox.test"));
    }

    [Fact]
    public async Task Invite_rejects_owner_with_no_email()
    {
        using var db = TestDbContext.Create();
        var (svc, _, _) = NewService(db);

        await Assert.ThrowsAsync<ConnectorValidationException>(
            () => svc.InviteAsync("owner-1", "", "friend@example.com", "https://app.postyfox.test"));
    }

    [Fact]
    public async Task Accept_creates_membership_when_email_matches()
    {
        using var db = TestDbContext.Create();
        var (svc, email, _) = NewService(db);
        await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");
        var token = TokenFromEmail(email);

        var result = await svc.AcceptAsync(token, "member-1", "Friend@Example.com");

        Assert.Equal(AccountAcceptResult.Accepted, result);
        var member = Assert.Single(db.AccountMembers);
        Assert.Equal("owner-1", member.OwnerUserId);
        Assert.Equal("member-1", member.MemberUserId);

        var sent = await svc.ListSentAsync("owner-1");
        Assert.Equal(InviteStatus.Accepted, sent.Single().Status);
    }

    [Fact]
    public async Task Accept_rejects_mismatched_email()
    {
        using var db = TestDbContext.Create();
        var (svc, email, _) = NewService(db);
        await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");
        var token = TokenFromEmail(email);

        var result = await svc.AcceptAsync(token, "member-1", "someone-else@example.com");

        Assert.Equal(AccountAcceptResult.EmailMismatch, result);
        Assert.Empty(db.AccountMembers);
    }

    [Fact]
    public async Task AcceptById_works_for_the_pending_for_you_list_without_a_token()
    {
        using var db = TestDbContext.Create();
        var (svc, _, _) = NewService(db);
        var invite = await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");

        var wrongEmail = await svc.AcceptByIdAsync(invite.Id, "member-1", "someone-else@example.com");
        Assert.Equal(AccountAcceptResult.EmailMismatch, wrongEmail);

        var result = await svc.AcceptByIdAsync(invite.Id, "member-1", "Friend@Example.com");
        Assert.Equal(AccountAcceptResult.Accepted, result);
        Assert.Single(db.AccountMembers);

        // Already accepted: no longer pending, so a second attempt finds nothing to accept.
        Assert.Equal(AccountAcceptResult.NotFound, await svc.AcceptByIdAsync(invite.Id, "member-2", "friend@example.com"));
    }

    [Fact]
    public async Task Accept_rejects_unknown_token()
    {
        using var db = TestDbContext.Create();
        var (svc, _, _) = NewService(db);

        var result = await svc.AcceptAsync("not-a-real-token-at-all-00000000000000", "member-1", "friend@example.com");

        Assert.Equal(AccountAcceptResult.NotFound, result);
    }

    [Fact]
    public async Task Accept_rejects_expired_invite()
    {
        using var db = TestDbContext.Create();
        var (svc, email, clock) = NewService(db);
        await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");
        var token = TokenFromEmail(email);

        clock.UtcNow = DateTimeOffset.UnixEpoch.AddDays(8);
        var result = await svc.AcceptAsync(token, "member-1", "friend@example.com");

        Assert.Equal(AccountAcceptResult.Expired, result);
    }

    [Fact]
    public async Task Revoke_only_affects_pending_invites_owned_by_caller()
    {
        using var db = TestDbContext.Create();
        var (svc, _, _) = NewService(db);
        var invite = await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");

        Assert.False(await svc.RevokeAsync("someone-else", invite.Id));
        Assert.True(await svc.RevokeAsync("owner-1", invite.Id));
        Assert.False(await svc.RevokeAsync("owner-1", invite.Id)); // already revoked
    }

    [Fact]
    public async Task ListPendingForEmail_only_shows_pending_invites_addressed_to_that_email()
    {
        using var db = TestDbContext.Create();
        var (svc, _, _) = NewService(db);
        await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");
        await svc.InviteAsync("owner-1", "owner@example.com", "someone-else@example.com", "https://app.postyfox.test");

        var pending = await svc.ListPendingForEmailAsync("Friend@Example.com");

        var invite = Assert.Single(pending);
        Assert.Equal("friend@example.com", invite.InviteeEmail);
    }

    [Fact]
    public async Task ListAccessibleAccounts_includes_self_and_memberships()
    {
        using var db = TestDbContext.Create();
        var (svc, email, _) = NewService(db);
        await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");
        await svc.AcceptAsync(TokenFromEmail(email), "member-1", "friend@example.com");

        var accounts = await svc.ListAccessibleAccountsAsync("member-1", "friend@example.com");

        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a is { IsSelf: true, UserId: "member-1" });
        Assert.Contains(accounts, a => a is { IsSelf: false, UserId: "owner-1", Email: "owner@example.com" });
    }

    [Fact]
    public async Task Owner_can_list_and_remove_members()
    {
        using var db = TestDbContext.Create();
        var (svc, email, _) = NewService(db);
        await svc.InviteAsync("owner-1", "owner@example.com", "friend@example.com", "https://app.postyfox.test");
        await svc.AcceptAsync(TokenFromEmail(email), "member-1", "friend@example.com");

        var members = await svc.ListMembersAsync("owner-1");
        Assert.Single(members);

        Assert.False(await svc.RemoveMemberAsync("owner-1", "not-a-member"));
        Assert.True(await svc.RemoveMemberAsync("owner-1", "member-1"));
        Assert.Empty(await svc.ListMembersAsync("owner-1"));
    }
}

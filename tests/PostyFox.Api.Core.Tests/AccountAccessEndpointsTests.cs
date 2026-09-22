using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Web.Auth;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class AccountAccessEndpointsTests
{
    [Fact]
    public async Task Invite_email_and_self_invite_are_rejected()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Auth-Request-Email", "dev-user@example.com");

        var selfInvite = await client.PostAsJsonAsync("/api/profile/invites", new { email = "dev-user@example.com" });
        Assert.Equal(HttpStatusCode.BadRequest, selfInvite.StatusCode);

        var badEmail = await client.PostAsJsonAsync("/api/profile/invites", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
    }

    [Fact]
    public async Task Invite_create_list_and_revoke()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Auth-Request-Email", "dev-user@example.com");

        var create = await client.PostAsJsonAsync("/api/profile/invites", new { email = "friend@example.com" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var invite = await create.Content.ReadFromJsonAsync<AccountInviteDto>();
        Assert.Equal(InviteStatus.Pending, invite!.Status);

        var sent = await client.GetFromJsonAsync<List<AccountInviteDto>>("/api/profile/invites");
        Assert.Contains(sent!, i => i.Id == invite.Id);

        var email = factory.Services.GetRequiredService<IEmailSender>() as FakeEmailSender ?? throw new InvalidOperationException();
        var captured = Assert.Single(email.Sent);
        Assert.Equal("friend@example.com", captured.To);

        var revoke = await client.DeleteAsync($"/api/profile/invites/{invite.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        var revokeAgain = await client.DeleteAsync($"/api/profile/invites/{invite.Id}");
        Assert.Equal(HttpStatusCode.NotFound, revokeAgain.StatusCode);
    }

    /// <summary>
    /// DevMode authenticates every request as the same fixed "dev-user", so a real second identity
    /// isn't reachable over HTTP here. Instead this seeds the membership directly (as accepting an
    /// invite would) and drives switching entirely through the public X-Act-As contract: a header the
    /// SPA sends and the backend validates, never a fact the endpoint under test is told directly.
    /// </summary>
    [Fact]
    public async Task Act_as_header_is_honoured_only_with_a_valid_membership()
    {
        using var factory = new CustomWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Templates.Add(new Template { Id = Guid.NewGuid(), UserId = "owner-1", Title = "Owner's template", MarkdownBody = "hi" });
            db.AccountMembers.Add(new AccountMember
            {
                OwnerUserId = "owner-1",
                OwnerEmail = "owner@example.com",
                MemberUserId = "dev-user",
                MemberEmail = "dev-user@example.com",
                InviteId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();

        // No header: dev-user sees their own (empty) template list.
        var own = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates");
        Assert.Empty(own!);

        // Valid membership: acting as owner-1 surfaces their data.
        client.DefaultRequestHeaders.Add(AuthConstants.ActAsHeader, "owner-1");
        var asOwner = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates");
        Assert.Single(asOwner!);

        // No membership to this id: header is ignored, falls back to acting as self.
        client.DefaultRequestHeaders.Remove(AuthConstants.ActAsHeader);
        client.DefaultRequestHeaders.Add(AuthConstants.ActAsHeader, "someone-with-no-grant");
        var asStranger = await client.GetFromJsonAsync<List<TemplateDto>>("/api/templates");
        Assert.Empty(asStranger!);
    }

    [Fact]
    public async Task Accept_rejects_a_token_whose_email_does_not_match_the_signed_in_user()
    {
        // DevMode authenticates every request as the same fixed "dev-user" (see the class remark on
        // Act_as_header_is_honoured...), so a real cross-user "accept" can't be driven over HTTP here;
        // that path (distinct owner/member ids) is covered directly at the service layer in
        // AccountAccessServiceTests. This checks what IS reachable over HTTP: the token round-trips
        // from the create endpoint's email into the accept endpoint, and a mismatched email is 400.
        using var factory = new CustomWebApplicationFactory();
        using var owner = factory.CreateClient();
        owner.DefaultRequestHeaders.Add("X-Auth-Request-Email", "owner@example.com");

        await owner.PostAsJsonAsync("/api/profile/invites", new { email = "friend@example.com" });
        var email = factory.Services.GetRequiredService<IEmailSender>() as FakeEmailSender ?? throw new InvalidOperationException();
        var token = Regex.Match(email.Sent.Single().Body, "token=([^\\s]+)").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));

        using var wrongEmail = factory.CreateClient();
        wrongEmail.DefaultRequestHeaders.Add("X-Auth-Request-Email", "not-the-invitee@example.com");
        var mismatch = await wrongEmail.PostAsJsonAsync("/api/profile/invites/accept", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
    }

    [Fact]
    public async Task Accounts_and_members_lists_reflect_an_accepted_membership()
    {
        using var factory = new CustomWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AccountMembers.Add(new AccountMember
            {
                OwnerUserId = "owner-1",
                OwnerEmail = "owner@example.com",
                MemberUserId = "dev-user",
                MemberEmail = "dev-user@example.com",
                InviteId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var accounts = await client.GetFromJsonAsync<List<AccountAccessDto>>("/api/profile/accounts");
        Assert.Contains(accounts!, a => !a.IsSelf && a.UserId == "owner-1" && a.Email == "owner@example.com");
        Assert.Contains(accounts!, a => a.IsSelf && a.UserId == "dev-user");

        // Members list is scoped to the owner: dev-user (the member here, not the owner) sees none.
        var members = await client.GetFromJsonAsync<List<AccountMemberDto>>("/api/profile/members");
        Assert.Empty(members!);

        var remove = await client.DeleteAsync("/api/profile/members/dev-user");
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode); // dev-user owns no account here
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class ProfileEndpoints
{
    public sealed record CreateKeyRequest(string? Name);
    public sealed record CreateInviteRequest(string Email);
    public sealed record AcceptInviteRequest(string Token);

    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile/keys")
            .RequireAuthorization()
            .WithTags("profile")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("", async (CreateKeyRequest? body, ClaimsPrincipal user, ApiKeyService svc, CancellationToken ct) =>
        {
            var dto = await svc.CreateAsync(user.UserId()!, body?.Name, ct);
            return Results.Created($"/api/profile/keys/{dto.Id}", dto);
        })
        .WithSummary("Create an API key")
        .WithDescription("Generates a new API key for the current user. The plaintext key is returned once and never again.")
        .Produces<ApiKeyCreatedDto>(StatusCodes.Status201Created);

        group.MapGet("", async (ClaimsPrincipal user, ApiKeyService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(user.UserId()!, ct)))
        .WithSummary("List API keys")
        .WithDescription("Returns the current user's API keys (secret is never returned; only a prefix).")
        .Produces<IReadOnlyList<ApiKeyDto>>();

        var settings = app.MapGroup("/api/profile/settings")
            .RequireAuthorization()
            .WithTags("profile")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        settings.MapGet("", async (ClaimsPrincipal user, UserSettingsService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAsync(user.UserId()!, ct)))
        .WithSummary("Get user settings")
        .Produces<UserSettingsDto>();

        settings.MapPut("", async (UserSettingsUpdateRequest body, ClaimsPrincipal user, UserSettingsService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateAsync(user.UserId()!, body, ct)))
        .WithSummary("Update user settings")
        .WithDescription("IncludeAdvertisingLine appends a \"Sent using PostyFox\" link to every post delivered for this user.")
        .Produces<UserSettingsDto>();

        group.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, ApiKeyService svc, CancellationToken ct) =>
            await svc.RevokeAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Revoke an API key")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        MapAccountAccessEndpoints(app);
    }

    /// <summary>Issue #409: invite-by-email account delegation and account switching.</summary>
    private static void MapAccountAccessEndpoints(IEndpointRouteBuilder app)
    {
        var invites = app.MapGroup("/api/profile/invites")
            .RequireAuthorization()
            .WithTags("profile")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        invites.MapPost("", async (CreateInviteRequest body, ClaimsPrincipal user, HttpRequest req, AccountAccessService svc, CancellationToken ct) =>
        {
            try
            {
                var acceptUrlBase = $"{req.Scheme}://{req.Host}";
                var dto = await svc.InviteAsync(user.UserId()!, user.Email() ?? "", body.Email, acceptUrlBase, ct);
                return Results.Created($"/api/profile/invites/{dto.Id}", dto);
            }
            catch (ConnectorValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithSummary("Invite someone, by email, to manage your account")
        .WithDescription("Emails an invite link. Acceptance is cross-checked against the OIDC email the invitee actually signs in with.")
        .Produces<AccountInviteDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        invites.MapGet("", async (ClaimsPrincipal user, AccountAccessService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListSentAsync(user.UserId()!, ct)))
        .WithSummary("List invites you've sent")
        .Produces<IReadOnlyList<AccountInviteDto>>();

        invites.MapGet("pending", async (ClaimsPrincipal user, AccountAccessService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListPendingForEmailAsync(user.Email() ?? "", ct)))
        .WithSummary("List invites addressed to your email, awaiting your acceptance")
        .Produces<IReadOnlyList<AccountInviteDto>>();

        invites.MapPost("accept", async (AcceptInviteRequest body, ClaimsPrincipal user, AccountAccessService svc, CancellationToken ct) =>
        {
            var result = await svc.AcceptAsync(body.Token, user.UserId()!, user.Email() ?? "", ct);
            return result switch
            {
                AccountAcceptResult.Accepted => Results.NoContent(),
                AccountAcceptResult.Expired => Results.BadRequest(new { error = "This invite has expired." }),
                AccountAcceptResult.EmailMismatch => Results.BadRequest(new
                {
                    error = "This invite was sent to a different email address than the one on your signed-in account."
                }),
                _ => Results.NotFound()
            };
        })
        .WithSummary("Accept an invite")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        invites.MapPost("{id:guid}/accept", async (Guid id, ClaimsPrincipal user, AccountAccessService svc, CancellationToken ct) =>
        {
            var result = await svc.AcceptByIdAsync(id, user.UserId()!, user.Email() ?? "", ct);
            return result switch
            {
                AccountAcceptResult.Accepted => Results.NoContent(),
                AccountAcceptResult.Expired => Results.BadRequest(new { error = "This invite has expired." }),
                AccountAcceptResult.EmailMismatch => Results.BadRequest(new
                {
                    error = "This invite was sent to a different email address than the one on your signed-in account."
                }),
                _ => Results.NotFound()
            };
        })
        .WithSummary("Accept an invite from your \"invitations waiting for you\" list")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        invites.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, AccountAccessService svc, CancellationToken ct) =>
            await svc.RevokeAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Revoke an invite you sent, before it's accepted")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        var accounts = app.MapGroup("/api/profile/accounts")
            .RequireAuthorization()
            .WithTags("profile")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        accounts.MapGet("", async (ClaimsPrincipal user, AccountAccessService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAccessibleAccountsAsync(user.UserId()!, user.Email(), ct)))
        .WithSummary("List accounts you can act as")
        .WithDescription("Yourself, plus any account owner who's accepted-invited you in. Send the chosen id as X-Act-As to act as it.")
        .Produces<IReadOnlyList<AccountAccessDto>>();

        var members = app.MapGroup("/api/profile/members")
            .RequireAuthorization()
            .WithTags("profile")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        members.MapGet("", async (ClaimsPrincipal user, AccountAccessService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListMembersAsync(user.UserId()!, ct)))
        .WithSummary("List who has delegated access to your account")
        .Produces<IReadOnlyList<AccountMemberDto>>();

        members.MapDelete("{memberUserId}", async (string memberUserId, ClaimsPrincipal user, AccountAccessService svc, CancellationToken ct) =>
            await svc.RemoveMemberAsync(user.UserId()!, memberUserId, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Remove someone's delegated access to your account")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}

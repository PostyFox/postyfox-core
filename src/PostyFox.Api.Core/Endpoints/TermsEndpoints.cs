using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Web.Auth;
using PostyFox.Application.Resources;

namespace PostyFox.Api.Core.Endpoints;

/// <summary>Issue #417: reading and accepting the terms of service. Exempt from the terms gate itself.</summary>
public static class TermsEndpoints
{
    public sealed record AcceptTermsRequest(int Version);

    public static void MapTermsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/terms")
            .RequireAuthorization()
            .AllowWithoutTermsAcceptance()
            .WithTags("terms")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("", async (ClaimsPrincipal user, TermsOfServiceService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetStatusAsync(user.CallerUserId()!, user.UserId()!, ct)))
        .WithSummary("Get the current terms of service and whether they have been accepted")
        .WithDescription("Current is null when no terms are in force. OwnerAccepted reports the account being acted as.")
        .Produces<TermsStatusDto>();

        group.MapGet("current", async (TermsOfServiceService svc, CancellationToken ct) =>
            await svc.GetCurrentAsync(ct) is { } terms ? Results.Ok(terms) : Results.NoContent())
        .AllowAnonymous()
        .WithSummary("Get the current terms of service")
        .WithDescription("Public, for the /tos page. 204 when no terms are in force.")
        .Produces<TermsDto>()
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("accept", async (AcceptTermsRequest body, ClaimsPrincipal user, TermsOfServiceService svc, CancellationToken ct) =>
            await svc.AcceptAsync(user.CallerUserId()!, body.Version, ct)
                ? Results.NoContent()
                : Results.Conflict(new { error = Messages.TermsNotCurrent }))
        .WithSummary("Accept the current terms of service")
        .WithDescription("Records acceptance for the signed-in person, never the account being acted as.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status409Conflict);
    }
}

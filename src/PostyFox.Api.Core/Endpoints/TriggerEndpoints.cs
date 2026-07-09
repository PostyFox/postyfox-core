using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Dtos;
using PostyFox.Application.Triggers;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class TriggerEndpoints
{
    public static void MapTriggerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/triggers")
            .RequireAuthorization().WithTags("triggers")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("", async (TriggerRegistrationRequest body, ClaimsPrincipal user, ExternalTriggerService svc, CancellationToken ct) =>
            await svc.RegisterAsync(user.UserId()!, body, ct) is { } dto ? Results.Ok(dto) : Results.BadRequest(new { error = "Unknown or disabled target connector" }))
        .WithSummary("Register an external trigger")
        .WithDescription("Registers interest in an external event source so a matching inbound webhook fires a templated post to the target connector.")
        .Produces<TriggerDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("", async (ClaimsPrincipal user, ExternalTriggerService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(user.UserId()!, ct)))
        .WithSummary("List external triggers")
        .Produces<IReadOnlyList<TriggerDto>>();

        group.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, ExternalTriggerService svc, CancellationToken ct) =>
            await svc.DeleteAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Delete an external trigger")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}

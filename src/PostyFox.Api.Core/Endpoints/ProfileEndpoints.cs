using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class ProfileEndpoints
{
    public sealed record CreateKeyRequest(string? Name);

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

        group.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, ApiKeyService svc, CancellationToken ct) =>
            await svc.RevokeAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Revoke an API key")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}

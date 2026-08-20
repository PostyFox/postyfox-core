using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class TagPresetEndpoints
{
    public static void MapTagPresetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tag-presets")
            .RequireAuthorization().WithTags("tag-presets")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("", async (ClaimsPrincipal user, TagPresetService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(user.UserId()!, ct)))
        .WithSummary("List tag presets")
        .Produces<IReadOnlyList<TagPresetDto>>();

        group.MapGet("{id:guid}", async (Guid id, ClaimsPrincipal user, TagPresetService svc, CancellationToken ct) =>
            await svc.GetAsync(user.UserId()!, id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
        .WithSummary("Get a tag preset")
        .Produces<TagPresetDto>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (TagPresetUpsertRequest body, ClaimsPrincipal user, TagPresetService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpsertAsync(user.UserId()!, body, ct)))
        .WithSummary("Create or update a tag preset")
        .Produces<TagPresetDto>();

        group.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, TagPresetService svc, CancellationToken ct) =>
            await svc.DeleteAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Delete a tag preset")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}

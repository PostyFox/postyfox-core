using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/templates")
            .RequireAuthorization().WithTags("templates")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("", async (ClaimsPrincipal user, TemplateService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(user.UserId()!, ct)))
        .WithSummary("List posting templates")
        .Produces<IReadOnlyList<TemplateDto>>();

        group.MapGet("{id:guid}", async (Guid id, ClaimsPrincipal user, TemplateService svc, CancellationToken ct) =>
            await svc.GetAsync(user.UserId()!, id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
        .WithSummary("Get a posting template")
        .Produces<TemplateDto>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (TemplateUpsertRequest body, ClaimsPrincipal user, TemplateService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpsertAsync(user.UserId()!, body, ct)))
        .WithSummary("Create or update a posting template")
        .Produces<TemplateDto>();

        group.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, TemplateService svc, CancellationToken ct) =>
            await svc.DeleteAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Delete a posting template")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}

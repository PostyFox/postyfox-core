using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class TextTemplateEndpoints
{
    public static void MapTextTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/text-templates")
            .RequireAuthorization().WithTags("text-templates")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("", async (ClaimsPrincipal user, TextTemplateService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(user.UserId()!, ct)))
        .WithSummary("List text templates")
        .Produces<IReadOnlyList<TextTemplateDto>>();

        group.MapGet("{id:guid}", async (Guid id, ClaimsPrincipal user, TextTemplateService svc, CancellationToken ct) =>
            await svc.GetAsync(user.UserId()!, id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
        .WithSummary("Get a text template")
        .Produces<TextTemplateDto>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (TextTemplateUpsertRequest body, ClaimsPrincipal user, TextTemplateService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.UpsertAsync(user.UserId()!, body, ct));
            }
            catch (ConnectorValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithSummary("Create or update a text template")
        .WithDescription("Referenced inline in a post as {{tt:Name}}; resolved per delivery target from its connector overrides, falling back to the default value.")
        .Produces<TextTemplateDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, TextTemplateService svc, CancellationToken ct) =>
            await svc.DeleteAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Delete a text template")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}

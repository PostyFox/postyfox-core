using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Services;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class AdminEndpoints
{
    public sealed record SetOperationalSecretRequest(string? Value);

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/access", () => Results.Ok(new { isAdmin = true }))
        .RequireAuthorization(AuthConstants.AdminPolicy)
        .WithTags("admin")
        .WithSummary("Confirm that the current Keycloak identity has admin access")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        var secrets = app.MapGroup("/api/admin/operational-secrets")
            .RequireAuthorization(AuthConstants.AdminPolicy)
            .WithTags("admin")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        secrets.MapGet("", async (OperationalSecretService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)))
        .WithSummary("List operational secret configuration status")
        .WithDescription("Returns the fixed operational-secret catalog and configured state; secret values are never returned.")
        .Produces<IReadOnlyList<OperationalSecretStatus>>();

        secrets.MapPut("{key}", async (
            string key,
            SetOperationalSecretRequest body,
            OperationalSecretService service,
            CancellationToken ct) =>
        {
            try
            {
                var status = await service.SetAsync(key, body.Value ?? "", ct);
                return status is null
                    ? Results.NotFound()
                    : Results.Ok(status);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithSummary("Set an operational secret")
        .Produces<OperationalSecretStatus>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        secrets.MapDelete("{key}", async (
            string key,
            OperationalSecretService service,
            CancellationToken ct) =>
            await service.DeleteAsync(key, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Delete an operational secret")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}

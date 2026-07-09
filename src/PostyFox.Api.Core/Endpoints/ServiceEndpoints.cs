using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Services;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class ServiceEndpoints
{
    public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
    {
        var catalog = app.MapGroup("/api/services")
            .RequireAuthorization().WithTags("services")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        catalog.MapGet("", async (ServiceCatalogService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(ct)))
        .WithSummary("List available platform definitions")
        .Produces<IReadOnlyList<ServiceDefinitionDto>>();

        catalog.MapGet("{id}", async (string id, ServiceCatalogService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
        .WithSummary("Get a platform definition")
        .Produces<ServiceDefinitionDto>()
        .Produces(StatusCodes.Status404NotFound);

        var connectors = app.MapGroup("/api/connectors")
            .RequireAuthorization().WithTags("connectors")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        connectors.MapGet("", async (ClaimsPrincipal user, UserConnectorService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(user.UserId()!, ct)))
        .WithSummary("List the user's configured connectors")
        .Produces<IReadOnlyList<UserConnectorDto>>();

        connectors.MapGet("{id:guid}", async (Guid id, ClaimsPrincipal user, UserConnectorService svc, CancellationToken ct) =>
            await svc.GetAsync(user.UserId()!, id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
        .WithSummary("Get a configured connector")
        .Produces<UserConnectorDto>()
        .Produces(StatusCodes.Status404NotFound);

        connectors.MapPut("", async (UserConnectorUpsertRequest body, ClaimsPrincipal user, UserConnectorService svc, CancellationToken ct) =>
            await svc.UpsertAsync(user.UserId()!, body, ct) is { } dto ? Results.Ok(dto) : Results.BadRequest(new { error = "Unknown service definition" }))
        .WithSummary("Create or update a connector")
        .WithDescription("Non-secret config is stored in the database; SecureConfigJson is written to the secret store.")
        .Produces<UserConnectorDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        connectors.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, UserConnectorService svc, CancellationToken ct) =>
            await svc.DeleteAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Delete a connector")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        connectors.MapGet("{id:guid}/authenticated", async (Guid id, ClaimsPrincipal user, ConnectorOperationsService svc, CancellationToken ct) =>
            await svc.IsAuthenticatedAsync(user.UserId()!, id, ct) is { } state ? Results.Ok(state) : Results.NotFound())
        .WithSummary("Check whether a connector is authenticated with its platform")
        .Produces<AuthState>()
        .Produces(StatusCodes.Status404NotFound);

        connectors.MapGet("{id:guid}/targets", async (Guid id, ClaimsPrincipal user, ConnectorOperationsService svc, CancellationToken ct) =>
            await svc.ListTargetsAsync(user.UserId()!, id, ct) is { } targets ? Results.Ok(targets) : Results.NotFound())
        .WithSummary("List the delivery targets available on a connector")
        .Produces<IReadOnlyList<ConnectorTarget>>()
        .Produces(StatusCodes.Status404NotFound);

        connectors.MapPost("{id:guid}/telegram/login", async (Guid id, TelegramLoginBody body, ClaimsPrincipal user, ConnectorOperationsService svc, CancellationToken ct) =>
            await svc.TelegramLoginAsync(user.UserId()!, id, body?.Value, ct) is { } step ? Results.Ok(step) : Results.NotFound())
        .WithSummary("Advance the Telegram MTProto login flow")
        .WithDescription("Call repeatedly, supplying the requested value (verification code, then 2FA password) until Status is 'complete'.")
        .Produces<TelegramLoginStep>()
        .Produces(StatusCodes.Status404NotFound);
    }

    public sealed record TelegramLoginBody(string? Value);
}

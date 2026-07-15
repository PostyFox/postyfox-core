using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
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
        {
            try
            {
                return await svc.UpsertAsync(user.UserId()!, body, ct) is { } dto
                    ? Results.Ok(dto)
                    : Results.BadRequest(new { error = "Unknown service definition" });
            }
            catch (ConnectorValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
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

        connectors.MapGet("{id:guid}/limits", async (Guid id, ClaimsPrincipal user, ConnectorOperationsService svc, CancellationToken ct) =>
            await svc.GetLimitsAsync(user.UserId()!, id, ct) is { } limits ? Results.Ok(limits) : Results.NotFound())
        .WithSummary("Report a connector's limits (character/attachment caps; live per-instance where supported)")
        .Produces<ConnectorLimits>()
        .Produces(StatusCodes.Status404NotFound);

        connectors.MapPost("{id:guid}/telegram/login", async (Guid id, TelegramLoginBody body, ClaimsPrincipal user, ConnectorOperationsService svc, CancellationToken ct) =>
            await svc.TelegramLoginAsync(user.UserId()!, id, body?.Value, ct) is { } step ? Results.Ok(step) : Results.NotFound())
        .WithSummary("Advance the Telegram MTProto login flow")
        .WithDescription("Call repeatedly, supplying the requested value (verification code, then 2FA password) until Status is 'complete'.")
        .Produces<TelegramLoginStep>()
        .Produces(StatusCodes.Status404NotFound);

        connectors.MapPost("{id:guid}/oauth/start", async (Guid id, ClaimsPrincipal user, ConnectorOperationsService svc, IConfiguration cfg, HttpRequest req, CancellationToken ct) =>
        {
            var url = await svc.StartOAuthAsync(user.UserId()!, id, OAuthCallbackUrl(cfg, req), ct);
            return url is null
                ? Results.BadRequest(new { error = "OAuth is not available for this connector" })
                : Results.Ok(new { authorizeUrl = url });
        })
        .WithSummary("Begin the OAuth connect flow for a connector")
        .WithDescription("Returns the provider authorize URL to open in the browser; the provider then calls back to /api/connectors/oauth/callback.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // Provider redirect target. The user still carries the oauth2-proxy session, so this is an
        // authenticated request; correlation to the connector is via the stashed request token.
        connectors.MapGet("oauth/callback", async (
            // OAuth1 (Tumblr): oauth_token/oauth_verifier. OAuth2 (Mastodon): state/code.
            // Firefish/Misskey MiAuth: the redirect echoes the session token (token/session) and
            // there is no verifier — the stored session token is exchanged for the access token.
            [FromQuery(Name = "oauth_token")] string? oauthToken,
            [FromQuery(Name = "oauth_verifier")] string? oauthVerifier,
            [FromQuery(Name = "state")] string? state,
            [FromQuery(Name = "code")] string? code,
            [FromQuery(Name = "token")] string? token,
            [FromQuery(Name = "session")] string? session,
            ClaimsPrincipal user, ConnectorOperationsService svc, CancellationToken ct) =>
        {
            // Correlation key stashed at start; verifier is optional (absent for MiAuth).
            var requestToken = FirstNonEmpty(oauthToken, state, token, session);
            var verifier = FirstNonEmpty(oauthVerifier, code) ?? "";
            var ok = !string.IsNullOrEmpty(requestToken)
                && await svc.CompleteOAuthAsync(user.UserId()!, requestToken!, verifier, ct);
            return Results.Content(OAuthCallbackHtml(ok), "text/html");
        })
        .WithSummary("OAuth provider callback — completes the connect flow and closes the popup");
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrEmpty(v));

    private static string OAuthCallbackUrl(IConfiguration cfg, HttpRequest req)
    {
        // Must match the provider app's registered callback exactly, so prefer explicit config.
        var baseUrl = cfg["OAuth:CallbackBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = $"{req.Scheme}://{req.Host}";
        return $"{baseUrl.TrimEnd('/')}/api/connectors/oauth/callback";
    }

    private static string OAuthCallbackHtml(bool ok)
    {
        var okJs = ok ? "true" : "false";
        var msg = ok ? "Connected — you can close this window." : "Connection failed. Please try again.";
        return $$"""
            <!doctype html><html><head><meta charset="utf-8"><title>PostyFox</title></head>
            <body style="font-family:sans-serif;padding:2rem;text-align:center">
            <script>
            (function () {
              var ok = {{okJs}};
              try { if (window.opener) window.opener.postMessage({ type: 'postyfox-oauth', ok: ok }, window.location.origin); } catch (e) {}
              if (window.opener) { window.close(); }
              else { window.location.replace('/connectors?oauth=' + (ok ? 'success' : 'error')); }
            })();
            </script>
            <p>{{msg}}</p></body></html>
            """;
    }

    public sealed record TelegramLoginBody(string? Value);
}

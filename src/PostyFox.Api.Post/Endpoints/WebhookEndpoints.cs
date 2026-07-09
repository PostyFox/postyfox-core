using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Triggers;

namespace PostyFox.Api.Post.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/{sourceType}", async (string sourceType, HttpRequest req, ExternalTriggerService svc, CancellationToken ct) =>
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync(ct);
            var headers = req.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            var result = await svc.HandleWebhookAsync(sourceType, headers, body, ct);
            return result.Outcome switch
            {
                WebhookOutcome.UnknownSource => Results.NotFound(),
                WebhookOutcome.Unauthorized => Results.Unauthorized(),
                WebhookOutcome.Challenge => Results.Text(result.Challenge ?? "", "text/plain"),
                _ => Results.Ok(new { processed = result.FiredCount })
            };
        })
        .AllowAnonymous()
        .WithTags("webhooks")
        .WithSummary("Inbound external-trigger webhook")
        .WithDescription("Signature-verified, deduplicated inbound webhook for a trigger source; fans out to matching triggers.");
    }
}

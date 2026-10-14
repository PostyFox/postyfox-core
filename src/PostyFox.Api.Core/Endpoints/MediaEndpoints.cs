using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Options;
using PostyFox.Web.Auth;
using PostyFox.Application.Resources;

namespace PostyFox.Api.Core.Endpoints;

public static class MediaEndpoints
{
    public const string Container = MediaContainers.Media;

    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media")
            .RequireAuthorization().WithTags("media")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .DisableAntiforgery();

        group.MapPost("", async (IFormFile? file, ClaimsPrincipal user, IObjectStore store, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = Messages.NoFileUploaded });

            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            var key = $"{user.UserId()}/{Guid.NewGuid():N}/{Path.GetFileName(file.FileName)}";

            await using var stream = file.OpenReadStream();
            await store.PutAsync(Container, key, stream, contentType, ct);

            return Results.Ok(new MediaRef(Container, key, contentType));
        })
        .WithSummary("Upload a media file")
        .WithDescription("Stores an uploaded file in the object store and returns a MediaRef to attach to a post.")
        .Produces<MediaRef>()
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("limits", (IOptions<MediaOptions> options) =>
            Results.Ok(new MediaLimitsDto(options.Value.MaxUploadSizeBytes)))
        .WithSummary("Reports the configured max upload size")
        .WithDescription(
            "Mirrors the gateway's own configured cap for POST /api/media (see MediaOptions) rather " +
            "than introspecting it, so the frontend can reject an oversized file before attempting " +
            "the upload. MaxUploadSizeBytes is null when the deployment hasn't configured one.")
        .Produces<MediaLimitsDto>();

        // Platforms that fetch media by URL (Instagram) pull staged copies from here, so the object
        // store itself never needs to be internet-reachable. Anonymous because the fetcher has no
        // session (oauth2-proxy skips auth for this route too); the random key is the only
        // credential, and only the staging prefix is reachable.
        app.MapMethods("/api/media/staged/{key}", [HttpMethods.Get, HttpMethods.Head], async (string key, IObjectStore store, CancellationToken ct) =>
        {
            if (StagedContentType(key) is not { } contentType)
                return Results.NotFound();

            var stream = await store.OpenReadAsync(MediaContainers.Staging, key, ct);
            return stream is null ? Results.NotFound() : Results.Stream(stream, contentType);
        })
        .AllowAnonymous()
        .ExcludeFromDescription();
    }

    /// <summary>Staged keys are always "{guid}.{ext}" (see connectors-node's instagram connector).</summary>
    private static string? StagedContentType(string key)
    {
        var ext = Path.GetExtension(key);
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "D", out _)) return null;
        return ext switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".mp4" => "video/mp4",
            _ => null,
        };
    }
}

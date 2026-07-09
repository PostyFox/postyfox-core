using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Core.Endpoints;

public static class MediaEndpoints
{
    public const string Container = "media";

    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/media")
            .RequireAuthorization().WithTags("media")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .DisableAntiforgery();

        group.MapPost("", async (IFormFile? file, ClaimsPrincipal user, IObjectStore store, CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "No file uploaded" });

            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            var key = $"{user.UserId()}/{Guid.NewGuid():N}/{Path.GetFileName(file.FileName)}";

            await using var stream = file.OpenReadStream();
            await store.PutAsync(Container, key, stream, contentType, ct);

            // The returned MediaRef is what the client attaches to a post's `media` array.
            return Results.Ok(new MediaRef(Container, key, contentType));
        })
        .WithSummary("Upload a media file")
        .WithDescription("Stores an uploaded file in the object store and returns a MediaRef to attach to a post.")
        .Produces<MediaRef>()
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }
}

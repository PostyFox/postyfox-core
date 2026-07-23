using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Dtos;
using PostyFox.Application.Posting;
using PostyFox.Web.Auth;

namespace PostyFox.Api.Post.Endpoints;

public static class PostEndpoints
{
    public static void MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/posts")
            .RequireAuthorization().WithTags("posts")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("", async (CreatePostRequest body, ClaimsPrincipal user, PostIntakeService svc, CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(user.UserId()!, body, ct);
            return result is null
                ? Results.BadRequest(new { error = "No valid, enabled target connectors specified" })
                : Results.Accepted($"/api/posts/{result.PostId}", result);
        })
        .WithSummary("Create a post")
        .WithDescription("Accepts a post for one or more target connectors and enqueues generation + delivery. Returns 202 with the post id; poll the status endpoint for progress.")
        .Produces<CreatePostResponse>(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("", async (ClaimsPrincipal user, PostStatusService svc, CancellationToken ct, string? filter = null, int limit = 50) =>
            Results.Ok(await svc.ListAsync(user.UserId()!, string.Equals(filter, "active", StringComparison.OrdinalIgnoreCase), limit, ct)))
        .WithSummary("List posts")
        .WithDescription("Returns the user's posts newest-first (id, title, aggregated status, target counts), bounded by the retention window. Pass `filter=active` for only the posts still being processed. `limit` is clamped to 1..200 (default 50).")
        .Produces<IReadOnlyList<PostSummaryDto>>();

        group.MapGet("{id:guid}", async (Guid id, ClaimsPrincipal user, PostStatusService svc, CancellationToken ct) =>
            await svc.GetAsync(user.UserId()!, id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
        .WithSummary("Get post status")
        .WithDescription("Returns the aggregated root status and per-target delivery status.")
        .Produces<PostStatusDto>()
        .Produces(StatusCodes.Status404NotFound);
    }
}

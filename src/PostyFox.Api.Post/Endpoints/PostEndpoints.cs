using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Posting;
using PostyFox.Domain.Enums;
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
            try
            {
                var result = await svc.CreateAsync(user.UserId()!, body, ct);
                if (result is null) return Results.BadRequest(new { error = "No valid, enabled target connectors specified" });
                return result.RootStatus == PostRootStatus.Draft
                    ? Results.Created($"/api/posts/{result.PostId}", result)
                    : Results.Accepted($"/api/posts/{result.PostId}", result);
            }
            catch (ConnectorValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithSummary("Create a post")
        .WithDescription("Accepts a post for one or more target connectors and enqueues generation + delivery. `targetOptions` carries per-submission platform choices keyed by connector id (see the service definition's `postOptionsSchema`). Set `isDraft` to save it for later instead — it's persisted with no targets resolved/validated and nothing enqueued; publish it later via `POST /{id}/publish`. Returns 202 (or 201 for a draft) with the post id; poll the status endpoint for progress.")
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

        group.MapGet("{id:guid}/content", async (Guid id, ClaimsPrincipal user, PostStatusService svc, CancellationToken ct) =>
            await svc.GetContentAsync(user.UserId()!, id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
        .WithSummary("Get a post's authored content")
        .WithDescription("Returns a post's authored content as-is, no media duplication — used to load a draft back into the compose form for editing.")
        .Produces<PostContentDto>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("{id:guid}", async (Guid id, CreatePostRequest body, ClaimsPrincipal user, PostIntakeService svc, CancellationToken ct) =>
        {
            try
            {
                return await svc.UpdateDraftAsync(user.UserId()!, id, body, ct) switch
                {
                    DraftActionOutcome.Success => Results.NoContent(),
                    DraftActionOutcome.NotADraft => Results.Conflict(new { error = "Post has already been published and can no longer be edited as a draft" }),
                    _ => Results.NotFound()
                };
            }
            catch (ConnectorValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithSummary("Update a draft")
        .WithDescription("Overwrites a draft's authored content and target selection in place. 409 once it's been published — recreate it via duplicate instead.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("{id:guid}/publish", async (Guid id, ClaimsPrincipal user, PostIntakeService svc, CancellationToken ct) =>
        {
            try
            {
                var result = await svc.PublishDraftAsync(user.UserId()!, id, ct);
                return result.Outcome switch
                {
                    DraftActionOutcome.Success => Results.Ok(result.Response),
                    DraftActionOutcome.NotADraft => Results.Conflict(new { error = "Post has already been published" }),
                    DraftActionOutcome.NoValidTargets => Results.BadRequest(new { error = "No valid, enabled target connectors specified" }),
                    _ => Results.NotFound()
                };
            }
            catch (ConnectorValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithSummary("Publish a draft")
        .WithDescription("Resolves the draft's stored target selection against the user's current connectors, enqueues generation + delivery, and it stops being a draft. Returns 202 with the post id; poll the status endpoint for progress.")
        .Produces<CreatePostResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("{id:guid}/duplicate", async (Guid id, ClaimsPrincipal user, PostDuplicationService svc, CancellationToken ct) =>
            await svc.DuplicateAsync(user.UserId()!, id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
        .WithSummary("Duplicate a post for editing")
        .WithDescription("Returns a post's authored content (title, body, tags, media, targets, schedule) to re-seed the compose form for \"post again\". Media is copied to fresh blobs so the recreated post is independent of the original. Does not itself create a post.")
        .Produces<PostContentDto>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("{id:guid}/cancel", async (Guid id, ClaimsPrincipal user, PostLifecycleService svc, CancellationToken ct) =>
            await svc.CancelAsync(user.UserId()!, id, ct) switch
            {
                CancelOutcome.Cancelled => Results.NoContent(),
                CancelOutcome.NothingToCancel => Results.Conflict(new { error = "Post has nothing left to cancel" }),
                _ => Results.NotFound()
            })
        .WithSummary("Cancel a post")
        .WithDescription("Cancels every target that hasn't been sent yet (queued/generating/ready). Already-delivered targets are kept. Returns 204 on success, 409 if there is nothing left to cancel.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("{id:guid}", async (Guid id, ClaimsPrincipal user, PostLifecycleService svc, CancellationToken ct) =>
            await svc.DeleteAsync(user.UserId()!, id, ct) ? Results.NoContent() : Results.NotFound())
        .WithSummary("Delete a post")
        .WithDescription("Permanently removes a post and its stored payload/media. Works for history entries and for stale/orphaned queued rows; any pending queue message for it then no-ops.")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Application.Telemetry;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

public sealed class PostIntakeService(
    IAppDbContext db,
    IObjectStore objectStore,
    IMessageBus bus,
    IClock clock,
    IOptions<PipelineOptions> options)
{
    private readonly PipelineOptions _options = options.Value;

    /// <summary>
    /// Persists a post + one target per selected connector, stores the payload, and enqueues
    /// generation for each target (delayed if scheduled). Returns null if no valid targets.
    /// </summary>
    public async Task<CreatePostResponse?> CreateAsync(string userId, CreatePostRequest request, CancellationToken ct = default)
    {
        var targetIds = (request.Targets ?? []).Distinct().ToList();
        if (targetIds.Count == 0) return null;

        var connectors = await db.UserConnectors
            .Include(c => c.ServiceDefinition)
            .Where(c => c.UserId == userId && c.Enabled && targetIds.Contains(c.Id))
            .ToListAsync(ct);
        if (connectors.Count == 0) return null;

        var now = clock.UtcNow;
        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title ?? string.Empty,
            Description = request.Description ?? string.Empty,
            HtmlDescription = request.HtmlDescription ?? string.Empty,
            TagsJson = Json.Serialize(request.Tags ?? []),
            MediaManifestJson = Json.Serialize(request.Media ?? []),
            VariablesJson = Json.Serialize(request.Variables ?? new Dictionary<string, string>()),
            TemplateId = request.TemplateId,
            PostAt = request.PostAt,
            RootStatus = PostRootStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var connector in connectors)
        {
            post.Targets.Add(new PostTarget
            {
                Id = Guid.NewGuid(),
                PostId = post.Id,
                ConnectorId = connector.Id,
                Platform = connector.ServiceDefinition!.Platform,
                Status = TargetStatus.Queued,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // From here on, every log in this request carries the PostId (see PostIdLogEnricher), so a
        // user can hand a dev the post id from the UI and the dev finds the intake telemetry too.
        PostTelemetry.SetBusinessBaggage(post.Id);

        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);

        // Persist the human-authored payload alongside the record (mirrors media storage).
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/title", post.Title, ct: ct);
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/description", post.Description, ct: ct);
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/description-html", post.HtmlDescription, ct: ct);

        var delay = request.PostAt is { } at && at > now ? at - now : (TimeSpan?)null;
        foreach (var target in post.Targets)
            await bus.PublishAsync(new GenerateTargetCommand { PostId = post.Id, TargetId = target.Id }, delay, ct);

        return new CreatePostResponse(post.Id, post.RootStatus);
    }
}

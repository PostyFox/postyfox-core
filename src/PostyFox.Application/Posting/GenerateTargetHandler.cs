using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

/// <summary>Renders a target's content, then enqueues delivery.</summary>
public sealed class GenerateTargetHandler(
    IAppDbContext db,
    ITemplateEngine engine,
    IMessageBus bus,
    IClock clock,
    ILogger<GenerateTargetHandler> logger) : IMessageHandler<GenerateTargetCommand>
{
    public async Task HandleAsync(GenerateTargetCommand message, CancellationToken ct)
    {
        var target = await db.PostTargets.Include(t => t.Post)
            .FirstOrDefaultAsync(t => t.Id == message.TargetId, ct);
        if (target?.Post is null)
        {
            logger.LogWarning("Generate: target {TargetId} not found", message.TargetId);
            return;
        }
        if (target.Status is TargetStatus.Ready or TargetStatus.Delivering or TargetStatus.Delivered)
            return; // idempotent: already generated

        var post = target.Post;

        var variables = Json.Deserialize<Dictionary<string, string>>(post.VariablesJson) ?? new();
        variables.TryAdd("title", post.Title);
        variables.TryAdd("description", post.Description);

        var tags = Json.Deserialize<List<string>>(post.TagsJson) ?? [];
        var media = Json.Deserialize<List<MediaRef>>(post.MediaManifestJson) ?? [];

        var body = post.Description;
        if (post.TemplateId is { } templateId)
        {
            var template = await db.Templates.FirstOrDefaultAsync(t => t.Id == templateId, ct);
            if (template is not null) body = template.MarkdownBody;
        }

        var rendered = engine.Render(new RenderRequest(
            target.Platform,
            string.IsNullOrEmpty(post.Title) ? null : post.Title,
            body,
            variables,
            tags,
            media));

        target.RenderedContentJson = Json.Serialize(rendered);
        target.Status = TargetStatus.Ready;
        target.UpdatedAt = clock.UtcNow;
        await UpdateRootStatusAsync(post.Id, ct);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new DeliverTargetCommand { PostId = post.Id, TargetId = target.Id }, ct: ct);
    }

    private async Task UpdateRootStatusAsync(Guid postId, CancellationToken ct)
    {
        var post = await db.Posts.Include(p => p.Targets).FirstAsync(p => p.Id == postId, ct);
        post.RootStatus = RootStatusCalculator.Compute(post.Targets);
        post.UpdatedAt = clock.UtcNow;
    }
}

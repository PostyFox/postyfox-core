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
    IConnectorRegistry registry,
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
        if (target.Status == TargetStatus.Cancelled)
        {
            logger.LogInformation("Generate: target {TargetId} was cancelled; skipping", message.TargetId);
            return; // user cancelled while this was sitting on the delayed queue
        }

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

        var supportsTags = true;
        var maxContentLength = (int?)null;
        if (registry.TryGet(target.Platform, out var connector))
        {
            var descriptor = connector.Describe();
            supportsTags = descriptor.SupportsTags;
            maxContentLength = descriptor.MaxContentLength;
        }

        var textTemplateValues = await ResolveTextTemplatesAsync(post.UserId, target.ConnectorId, ct);

        var rendered = engine.Render(new RenderRequest(
            target.Platform,
            string.IsNullOrEmpty(post.Title) ? null : post.Title,
            body,
            variables,
            tags,
            media,
            post.Rating,
            target.IncludeTags,
            supportsTags,
            maxContentLength,
            textTemplateValues));

        target.RenderedContentJson = Json.Serialize(rendered);
        target.Status = TargetStatus.Ready;
        target.UpdatedAt = clock.UtcNow;
        await UpdateRootStatusAsync(post.Id, ct);
        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new DeliverTargetCommand { PostId = post.Id, TargetId = target.Id }, ct: ct);
    }

    /// <summary>
    /// Resolves every one of the user's text templates (see <c>{{tt:name}}</c>,
    /// <see cref="ITemplateEngine"/>) to a single value for this specific target: its connector's
    /// override when one is set, else the template's default, else an empty string. Resolved fresh at
    /// generate time (not stored on the post), so editing a template after a post is scheduled but
    /// before it fires picks up the newer value.
    /// </summary>
    private async Task<Dictionary<string, string>> ResolveTextTemplatesAsync(
        string userId, Guid? connectorId, CancellationToken ct)
    {
        var templates = await db.TextTemplates.Where(t => t.UserId == userId).ToListAsync(ct);
        var connectorKey = connectorId?.ToString();
        var resolved = new Dictionary<string, string>(templates.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var t in templates)
        {
            var overrides = Json.Deserialize<Dictionary<string, string>>(t.ConnectorValuesJson);
            resolved[t.Name] = connectorKey is not null && overrides is not null
                && overrides.TryGetValue(connectorKey, out var overrideValue)
                ? overrideValue
                : t.DefaultValue;
        }
        return resolved;
    }

    private async Task UpdateRootStatusAsync(Guid postId, CancellationToken ct)
    {
        var post = await db.Posts.Include(p => p.Targets).FirstAsync(p => p.Id == postId, ct);
        post.RootStatus = RootStatusCalculator.Compute(post.Targets);
        post.UpdatedAt = clock.UtcNow;
    }
}

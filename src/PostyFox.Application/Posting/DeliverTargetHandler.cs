using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Application.Services;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

/// <summary>
/// Delivers a rendered target to its platform connector. On transient failure it retries
/// with exponential backoff (via delayed re-publish) up to the configured attempt limit,
/// then marks the target failed.
/// </summary>
public sealed class DeliverTargetHandler(
    IAppDbContext db,
    IConnectorRegistry registry,
    ISecretsProvider secrets,
    IMessageBus bus,
    IClock clock,
    IOptions<PipelineOptions> options,
    ILogger<DeliverTargetHandler> logger) : IMessageHandler<DeliverTargetCommand>
{
    private readonly PipelineOptions _options = options.Value;

    public async Task HandleAsync(DeliverTargetCommand message, CancellationToken ct)
    {
        var target = await db.PostTargets.Include(t => t.Post)
            .FirstOrDefaultAsync(t => t.Id == message.TargetId, ct);
        if (target?.Post is null)
        {
            logger.LogWarning("Deliver: target {TargetId} not found", message.TargetId);
            return;
        }
        if (target.Status == TargetStatus.Delivered) return; // idempotent
        if (target.RenderedContentJson is null)
        {
            await FailAsync(target, "Target has not been generated", ct);
            return;
        }

        if (!registry.TryGet(target.Platform, out var connector))
        {
            await FailAsync(target, $"No connector registered for platform '{target.Platform}'", ct);
            return;
        }

        var userId = target.Post.UserId;
        string configJson = "{}";
        string? secretJson = null;
        if (target.ConnectorId is { } connectorId)
        {
            var uc = await db.UserConnectors.FirstOrDefaultAsync(c => c.Id == connectorId, ct);
            configJson = uc?.ConfigJson ?? "{}";
            secretJson = await secrets.GetSecretAsync(UserConnectorService.SecretName(connectorId, userId), ct);
        }

        var rendered = Json.Deserialize<RenderedPost>(target.RenderedContentJson)!;
        var context = new ConnectorContext(target.ConnectorId ?? Guid.Empty, userId, configJson, secretJson, null);

        target.Status = TargetStatus.Delivering;
        target.Attempts++;
        target.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        DeliveryResult result;
        try
        {
            result = await connector.DeliverAsync(context, rendered, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deliver: connector {Platform} threw for target {TargetId}", target.Platform, target.Id);
            result = DeliveryResult.Fail(ex.Message);
        }

        if (result.Success)
        {
            target.Status = TargetStatus.Delivered;
            target.ExternalId = result.ExternalId;
            target.ExternalUrl = result.ExternalUrl;
            target.Error = null;
        }
        else if (target.Attempts < _options.MaxDeliveryAttempts)
        {
            target.Error = result.Error;
            target.UpdatedAt = clock.UtcNow;
            await UpdateRootStatusAsync(target.PostId, ct);
            await db.SaveChangesAsync(ct);

            var backoff = TimeSpan.FromSeconds(_options.RetryBaseSeconds * Math.Pow(2, target.Attempts - 1));
            logger.LogWarning("Deliver: target {TargetId} attempt {Attempt} failed; retrying in {Backoff}s",
                target.Id, target.Attempts, backoff.TotalSeconds);
            await bus.PublishAsync(new DeliverTargetCommand { PostId = target.PostId, TargetId = target.Id }, backoff, ct);
            return;
        }
        else
        {
            target.Status = TargetStatus.Failed;
            target.Error = result.Error;
        }

        target.UpdatedAt = clock.UtcNow;
        await UpdateRootStatusAsync(target.PostId, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task FailAsync(PostTarget target, string error, CancellationToken ct)
    {
        target.Status = TargetStatus.Failed;
        target.Error = error;
        target.UpdatedAt = clock.UtcNow;
        await UpdateRootStatusAsync(target.PostId, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task UpdateRootStatusAsync(Guid postId, CancellationToken ct)
    {
        var post = await db.Posts.Include(p => p.Targets).FirstAsync(p => p.Id == postId, ct);
        post.RootStatus = RootStatusCalculator.Compute(post.Targets);
        post.UpdatedAt = clock.UtcNow;
    }
}

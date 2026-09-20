using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;
using PostyFox.Application.Services;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

/// <summary>
/// Executes one due post-automation rule (issue #323): reposts or deletes a delivered target on its
/// platform. Unlike <see cref="DeliverTargetHandler"/>, there is no retry/backoff here — a
/// repost/delete either lands or the rule is marked <see cref="AutomationStatus.Failed"/> with the
/// connector's error for the user to see. Automations are a one-shot action the post's own delivery
/// never depends on, not a pipeline stage.
/// </summary>
public sealed class AutomationExecutionHandler(
    IAppDbContext db,
    IConnectorRegistry registry,
    ISecretsProvider secrets,
    IClock clock,
    ILogger<AutomationExecutionHandler> logger) : IMessageHandler<ExecuteAutomationCommand>
{
    public async Task HandleAsync(ExecuteAutomationCommand message, CancellationToken ct)
    {
        var automation = await db.PostTargetAutomations
            .Include(a => a.PostTarget!.Post)
            .FirstOrDefaultAsync(a => a.Id == message.AutomationId, ct);
        var target = automation?.PostTarget;
        if (automation is null || target?.Post is null)
        {
            logger.LogWarning("Automation: {AutomationId} or its target not found", message.AutomationId);
            return;
        }
        if (automation.Status != AutomationStatus.Pending) return; // idempotent: already ran, failed or was cancelled

        if (target.ExternalId is null)
        {
            await FailAsync(automation, "Target has no stored external id", ct);
            return;
        }
        if (!registry.TryGet(target.Platform, out var connector))
        {
            await FailAsync(automation, $"No connector registered for platform '{target.Platform}'", ct);
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
            secretJson = await PairedUserAgentService.ApplyAsync(db, target.Platform, secretJson, ct);
        }
        var context = new ConnectorContext(target.ConnectorId ?? Guid.Empty, userId, configJson, secretJson, target.TargetId);
        var descriptor = connector.Describe();

        try
        {
            switch (automation.Action)
            {
                // The descriptor flag is the authoritative capability check (same one intake used to
                // accept this rule in the first place); the interface check alongside it is just the
                // mechanical "does this class know how", since one C# class (HttpConnector) backs
                // several Node-side platforms whose real capabilities differ.
                case AutomationAction.Repost when descriptor.SupportsRepost && connector is IRepostConnector repostable:
                    var reposted = await repostable.RepostAsync(context, target.ExternalId, ct);
                    if (reposted.Success) await CompleteAsync(automation, ct);
                    else await FailAsync(automation, reposted.Error ?? "repost failed", ct);
                    break;

                case AutomationAction.Delete when descriptor.SupportsDelete && connector is IDeleteConnector deletable:
                    var deleted = await deletable.DeleteRemoteAsync(context, target.ExternalId, ct);
                    if (deleted) await CompleteAsync(automation, ct);
                    else await FailAsync(automation, "delete failed", ct);
                    break;

                default:
                    // The compose form only offers actions the target's platform declares, and intake
                    // rejects anything else — this only fires for automations created before a
                    // connector's capability changed underneath them.
                    await FailAsync(automation, $"{target.Platform} no longer supports {automation.Action}", ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Automation: connector {Platform} threw for target {TargetId}", target.Platform, target.Id);
            await FailAsync(automation, ex.Message, ct);
        }
    }

    private async Task CompleteAsync(PostTargetAutomation automation, CancellationToken ct)
    {
        automation.Status = AutomationStatus.Done;
        automation.ExecutedAt = clock.UtcNow;
        automation.Error = null;
        await db.SaveChangesAsync(ct);
    }

    private async Task FailAsync(PostTargetAutomation automation, string error, CancellationToken ct)
    {
        automation.Status = AutomationStatus.Failed;
        automation.ExecutedAt = clock.UtcNow;
        automation.Error = error;
        await db.SaveChangesAsync(ct);
    }
}

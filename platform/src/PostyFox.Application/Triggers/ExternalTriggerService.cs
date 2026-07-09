using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Application.Posting;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Triggers;

/// <summary>
/// Source-agnostic external-trigger engine: registration of interest, plus inbound webhook
/// handling (verify → challenge/dedupe → frequency-throttled fan-out into the posting pipeline).
/// </summary>
public sealed class ExternalTriggerService(
    IAppDbContext db,
    ISecretStore secrets,
    IClock clock,
    ITriggerSourceRegistry sources,
    PostIntakeService intake)
{
    public async Task<TriggerDto?> RegisterAsync(string userId, TriggerRegistrationRequest req, CancellationToken ct = default)
    {
        var connectorOk = await db.UserConnectors.AnyAsync(c => c.UserId == userId && c.Id == req.TargetConnectorId && c.Enabled, ct);
        if (!connectorOk) return null;

        var trigger = new ExternalTrigger
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SourceType = req.SourceType,
            ExternalAccount = req.ExternalAccount,
            TemplateId = req.TemplateId,
            TargetConnectorId = req.TargetConnectorId,
            NotifyFrequencyHrs = req.NotifyFrequencyHrs,
            CreatedAt = clock.UtcNow
        };
        db.ExternalTriggers.Add(trigger);

        if (!await db.ExternalInterests.AnyAsync(i => i.SourceType == req.SourceType && i.ExternalAccount == req.ExternalAccount && i.UserId == userId, ct))
            db.ExternalInterests.Add(new ExternalInterest { SourceType = req.SourceType, ExternalAccount = req.ExternalAccount, UserId = userId });

        await db.SaveChangesAsync(ct);
        return ToDto(trigger);
    }

    public async Task<IReadOnlyList<TriggerDto>> ListAsync(string userId, CancellationToken ct = default) =>
        (await db.ExternalTriggers.Where(t => t.UserId == userId).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var t = await db.ExternalTriggers.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id, ct);
        if (t is null) return false;
        db.ExternalTriggers.Remove(t);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<WebhookResult> HandleWebhookAsync(string sourceType, IReadOnlyDictionary<string, string> headers, string rawBody, CancellationToken ct = default)
    {
        if (!sources.TryGet(sourceType, out var source))
            return new WebhookResult(WebhookOutcome.UnknownSource);

        var parsed = source.Parse(headers, rawBody);
        if (parsed.IsChallenge)
            return new WebhookResult(WebhookOutcome.Challenge, parsed.Challenge);

        var signingSecret = await secrets.GetSecretAsync($"trigger-{sourceType}-signing", ct);
        if (!source.VerifySignature(headers, rawBody, signingSecret))
            return new WebhookResult(WebhookOutcome.Unauthorized);

        if (string.IsNullOrEmpty(parsed.ExternalAccount))
            return new WebhookResult(WebhookOutcome.Processed, FiredCount: 0);

        // Dedupe by (source, messageId)
        if (!string.IsNullOrEmpty(parsed.MessageId))
        {
            if (await db.WebhookDedupes.AnyAsync(d => d.Source == sourceType && d.MessageId == parsed.MessageId, ct))
                return new WebhookResult(WebhookOutcome.AlreadyProcessed);
            db.WebhookDedupes.Add(new WebhookDedupe { Source = sourceType, MessageId = parsed.MessageId!, SeenAt = clock.UtcNow });
            await db.SaveChangesAsync(ct);
        }

        var triggers = await db.ExternalTriggers
            .Where(t => t.SourceType == sourceType && t.ExternalAccount == parsed.ExternalAccount)
            .ToListAsync(ct);

        var fired = 0;
        foreach (var trigger in triggers)
        {
            if (trigger.TargetConnectorId is null) continue;
            if (trigger.LastFiredAt is { } last && clock.UtcNow - last < TimeSpan.FromHours(trigger.NotifyFrequencyHrs))
                continue; // frequency throttle

            var request = new CreatePostRequest(
                [trigger.TargetConnectorId.Value], null, null, null, null, null, trigger.TemplateId, parsed.Variables, null);
            var result = await intake.CreateAsync(trigger.UserId, request, ct);
            if (result is not null)
            {
                trigger.LastFiredAt = clock.UtcNow;
                fired++;
            }
        }
        await db.SaveChangesAsync(ct);
        return new WebhookResult(WebhookOutcome.Processed, FiredCount: fired);
    }

    private static TriggerDto ToDto(ExternalTrigger t) =>
        new(t.Id, t.SourceType, t.ExternalAccount, t.TemplateId, t.TargetConnectorId, t.NotifyFrequencyHrs, t.LastFiredAt);
}

using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

/// <summary>
/// Admin-published terms of service and per-user acceptance (issue #417). Each publish is a new
/// version that everyone must accept again; publishing empty content turns the terms off.
/// </summary>
public sealed class TermsOfServiceService(IAppDbContext db, IClock clock)
{
    /// <summary>The version users must have accepted, or null when no terms are in force.</summary>
    public async Task<int?> GetCurrentVersionAsync(CancellationToken ct = default)
    {
        var latest = await db.TermsOfService
            .OrderByDescending(t => t.Version)
            .Select(t => new { t.Version, InForce = t.Content != "" })
            .FirstOrDefaultAsync(ct);
        return latest is { InForce: true } ? latest.Version : null;
    }

    public Task<bool> HasAcceptedAsync(string userId, int version, CancellationToken ct = default) =>
        db.TermsAcceptances.AnyAsync(a => a.UserId == userId && a.TermsVersion == version, ct);

    public async Task<TermsStatusDto> GetStatusAsync(string userId, string ownerUserId, CancellationToken ct = default)
    {
        var latest = await db.TermsOfService.AsNoTracking().OrderByDescending(t => t.Version).FirstOrDefaultAsync(ct);
        if (latest is null || latest.Content == "") return new TermsStatusDto(null, true, true);

        var accepted = await HasAcceptedAsync(userId, latest.Version, ct);
        var ownerAccepted = ownerUserId == userId ? accepted : await HasAcceptedAsync(ownerUserId, latest.Version, ct);
        return new TermsStatusDto(ToDto(latest), accepted, ownerAccepted);
    }

    /// <summary>Returns false when <paramref name="version"/> is not the current version (a stale page).</summary>
    public async Task<bool> AcceptAsync(string userId, int version, CancellationToken ct = default)
    {
        if (await GetCurrentVersionAsync(ct) != version) return false;
        if (await HasAcceptedAsync(userId, version, ct)) return true;

        db.TermsAcceptances.Add(new TermsAcceptance { UserId = userId, TermsVersion = version, AcceptedAt = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Publishes a new version. Returns null when <paramref name="content"/> is blank, which turns the terms off.</summary>
    public async Task<TermsDto?> PublishAsync(string adminUserId, string? content, CancellationToken ct = default)
    {
        var terms = new TermsOfService
        {
            Content = string.IsNullOrWhiteSpace(content) ? "" : content,
            PublishedByUserId = adminUserId,
            PublishedAt = clock.UtcNow
        };
        db.TermsOfService.Add(terms);
        await db.SaveChangesAsync(ct);
        return terms.Content == "" ? null : ToDto(terms);
    }

    private static TermsDto ToDto(TermsOfService t) => new(t.Version, t.Content, t.PublishedAt);
}

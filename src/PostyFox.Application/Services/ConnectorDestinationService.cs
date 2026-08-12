using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

/// <summary>
/// Manages the destinations a user exposes for posting under one connector login (see
/// <see cref="ConnectorDestination"/>) — e.g. picking which Telegram chats reachable from a single
/// MTProto login should show up as individually selectable targets in the compose form. Only
/// meaningful for connectors that declare
/// <see cref="ConnectorDescriptor.SupportsMultipleTargets"/>; live discovery of what can
/// be exposed (e.g. Telegram's chat list) is a separate concern handled by
/// <see cref="ConnectorOperationsService.ListTargetsAsync"/>.
/// </summary>
public sealed class ConnectorDestinationService(IAppDbContext db, IClock clock, IConnectorRegistry registry)
{
    /// <summary>Exposed destinations for one connector, or null if it isn't the user's.</summary>
    public async Task<IReadOnlyList<ConnectorDestinationDto>?> ListAsync(string userId, Guid connectorId, CancellationToken ct = default)
    {
        var owned = await db.UserConnectors.AnyAsync(c => c.UserId == userId && c.Id == connectorId, ct);
        if (!owned) return null;

        return await db.ConnectorDestinations
            .Where(d => d.ConnectorId == connectorId)
            .OrderBy(d => d.Name)
            .Select(d => new ConnectorDestinationDto(d.Id, d.ConnectorId, d.ExternalId, d.Name))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Every destination the user has exposed across all their connectors, flattened with the owning
    /// connector's identity — what the compose form needs to build its full set of selectable targets
    /// (single-destination connectors plus each exposed destination of multi-target ones).
    /// </summary>
    public async Task<IReadOnlyList<ConnectorDestinationSummaryDto>> ListAllAsync(string userId, CancellationToken ct = default) =>
        await db.ConnectorDestinations
            .Include(d => d.Connector!.ServiceDefinition)
            .Where(d => d.Connector!.UserId == userId)
            .OrderBy(d => d.Connector!.DisplayName).ThenBy(d => d.Name)
            .Select(d => new ConnectorDestinationSummaryDto(
                d.Id, d.ConnectorId, d.Connector!.ServiceDefinition!.Platform, d.Connector!.DisplayName, d.ExternalId, d.Name))
            .ToListAsync(ct);

    /// <summary>
    /// Replaces the full set of destinations exposed for a connector with <paramref name="selections"/>
    /// (matched by <see cref="ConnectorDestinationInput.ExternalId"/>): existing rows not present are
    /// removed, new ones are added, and names are refreshed in case the platform-side label changed
    /// since it was last exposed. Returns null if the connector isn't the user's or doesn't support
    /// multiple targets.
    /// </summary>
    public async Task<IReadOnlyList<ConnectorDestinationDto>?> SetAsync(
        string userId, Guid connectorId, IReadOnlyList<ConnectorDestinationInput> selections, CancellationToken ct = default)
    {
        var uc = await db.UserConnectors.Include(c => c.ServiceDefinition)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Id == connectorId, ct);
        if (uc?.ServiceDefinition is null) return null;
        if (!registry.TryGet(uc.ServiceDefinition.Platform, out var connector) || !connector.Describe().SupportsMultipleTargets)
            return null;

        var existing = await db.ConnectorDestinations.Where(d => d.ConnectorId == connectorId).ToListAsync(ct);
        var wanted = selections.Where(s => !string.IsNullOrWhiteSpace(s.ExternalId))
            .GroupBy(s => s.ExternalId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Name, StringComparer.Ordinal);

        var now = clock.UtcNow;
        foreach (var stale in existing.Where(d => !wanted.ContainsKey(d.ExternalId)))
            db.ConnectorDestinations.Remove(stale);

        foreach (var row in existing.Where(d => wanted.ContainsKey(d.ExternalId)))
        {
            var name = wanted[row.ExternalId];
            if (row.Name != name)
            {
                row.Name = name;
                row.UpdatedAt = now;
            }
        }

        var existingIds = existing.Select(d => d.ExternalId).ToHashSet(StringComparer.Ordinal);
        foreach (var (externalId, name) in wanted)
        {
            if (existingIds.Contains(externalId)) continue;
            db.ConnectorDestinations.Add(new ConnectorDestination
            {
                Id = Guid.NewGuid(),
                ConnectorId = connectorId,
                ExternalId = externalId,
                Name = name,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);

        return await db.ConnectorDestinations
            .Where(d => d.ConnectorId == connectorId)
            .OrderBy(d => d.Name)
            .Select(d => new ConnectorDestinationDto(d.Id, d.ConnectorId, d.ExternalId, d.Name))
            .ToListAsync(ct);
    }
}

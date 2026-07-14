using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

public sealed class ServiceCatalogService(IAppDbContext db, IConnectorRegistry connectors)
{
    public async Task<IReadOnlyList<ServiceDefinitionDto>> ListAsync(CancellationToken ct = default)
    {
        var defs = await db.ServiceDefinitions.Where(s => s.Enabled)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
        return defs.Select(Map).ToList();
    }

    public async Task<ServiceDefinitionDto?> GetAsync(string id, CancellationToken ct = default)
    {
        var def = await db.ServiceDefinitions.FirstOrDefaultAsync(s => s.Id == id, ct);
        return def is null ? null : Map(def);
    }

    // Capabilities are authoritative in each connector's Describe(); merge them into the catalogue
    // entry so clients can tailor the compose experience (char limits, media/title support, …).
    private ServiceDefinitionDto Map(ServiceDefinition s)
    {
        bool supportsTitle = false, supportsMedia = false, supportsThreads = false;
        int? maxContentLength = null;
        if (connectors.TryGet(s.Platform, out var connector))
        {
            var d = connector.Describe();
            supportsTitle = d.SupportsTitle;
            supportsMedia = d.SupportsMedia;
            supportsThreads = d.SupportsThreads;
            maxContentLength = d.MaxContentLength;
        }

        return new ServiceDefinitionDto(
            s.Id, s.Name, s.Enabled, s.ConfigSchema, s.SecureConfigSchema, s.Platform,
            supportsTitle, supportsMedia, supportsThreads, maxContentLength);
    }
}

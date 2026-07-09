using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;

namespace PostyFox.Application.Services;

public sealed class ServiceCatalogService(IAppDbContext db)
{
    public async Task<IReadOnlyList<ServiceDefinitionDto>> ListAsync(CancellationToken ct = default) =>
        await db.ServiceDefinitions.Where(s => s.Enabled)
            .OrderBy(s => s.Name)
            .Select(s => new ServiceDefinitionDto(s.Id, s.Name, s.Enabled, s.ConfigSchema, s.SecureConfigSchema, s.Platform))
            .ToListAsync(ct);

    public async Task<ServiceDefinitionDto?> GetAsync(string id, CancellationToken ct = default) =>
        await db.ServiceDefinitions.Where(s => s.Id == id)
            .Select(s => new ServiceDefinitionDto(s.Id, s.Name, s.Enabled, s.ConfigSchema, s.SecureConfigSchema, s.Platform))
            .FirstOrDefaultAsync(ct);
}

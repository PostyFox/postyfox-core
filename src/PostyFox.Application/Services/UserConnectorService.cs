using Microsoft.EntityFrameworkCore;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

public sealed class UserConnectorService(IAppDbContext db, ISecretsProvider secrets, IClock clock)
{
    /// <summary>Secret store key for a connector's secure config.</summary>
    public static string SecretName(Guid connectorId, string userId) => $"conn-{connectorId:N}-{userId}";

    public async Task<IReadOnlyList<UserConnectorDto>> ListAsync(string userId, CancellationToken ct = default) =>
        await db.UserConnectors.Where(c => c.UserId == userId)
            .Include(c => c.ServiceDefinition)
            .OrderBy(c => c.DisplayName)
            .Select(c => new UserConnectorDto(c.Id, c.ServiceDefinitionId,
                c.ServiceDefinition!.Platform, c.DisplayName, c.ConfigJson, c.Enabled))
            .ToListAsync(ct);

    public async Task<UserConnectorDto?> GetAsync(string userId, Guid id, CancellationToken ct = default) =>
        await db.UserConnectors.Where(c => c.UserId == userId && c.Id == id)
            .Include(c => c.ServiceDefinition)
            .Select(c => new UserConnectorDto(c.Id, c.ServiceDefinitionId,
                c.ServiceDefinition!.Platform, c.DisplayName, c.ConfigJson, c.Enabled))
            .FirstOrDefaultAsync(ct);

    public async Task<UserConnectorDto?> UpsertAsync(string userId, UserConnectorUpsertRequest request, CancellationToken ct = default)
    {
        var def = await db.ServiceDefinitions.FirstOrDefaultAsync(s => s.Id == request.ServiceDefinitionId, ct);
        if (def is null) return null;

        // Enforce the field descriptors declared on the service definition. The client runs the same
        // checks for fast feedback, but this is the authoritative gate (throws → 400 with the message).
        if (ConfigSchemaValidator.Validate(def.ConfigSchema, request.ConfigJson) is { } configError)
            throw new ConnectorValidationException(configError);
        // Secrets are only present when the user actually (re)entered them — a blank payload means
        // "keep the stored secret unchanged", so there is nothing to validate in that case.
        if (!string.IsNullOrWhiteSpace(request.SecureConfigJson)
            && ConfigSchemaValidator.Validate(def.SecureConfigSchema, request.SecureConfigJson) is { } secretError)
            throw new ConnectorValidationException(secretError);

        UserConnector? entity = request.Id is { } id
            ? await db.UserConnectors.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == id, ct)
            : null;

        if (entity is null)
        {
            entity = new UserConnector
            {
                Id = request.Id ?? Guid.NewGuid(),
                UserId = userId,
                CreatedAt = clock.UtcNow
            };
            db.UserConnectors.Add(entity);
        }

        entity.ServiceDefinitionId = request.ServiceDefinitionId;
        entity.DisplayName = request.DisplayName;
        entity.ConfigJson = request.ConfigJson;
        entity.Enabled = request.Enabled;
        entity.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.SecureConfigJson))
            await secrets.SetSecretAsync(SecretName(entity.Id, userId), request.SecureConfigJson, ct);

        return new UserConnectorDto(entity.Id, entity.ServiceDefinitionId, def.Platform, entity.DisplayName, entity.ConfigJson, entity.Enabled);
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.UserConnectors.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == id, ct);
        if (entity is null) return false;
        db.UserConnectors.Remove(entity);
        await db.SaveChangesAsync(ct);
        await secrets.TryDeleteSecretAsync(SecretName(id, userId), ct);
        return true;
    }
}

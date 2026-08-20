using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

/// <summary>CRUD for a user's reusable tag presets, applied client-side into the compose form's tags field.</summary>
public sealed class TagPresetService(IAppDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<TagPresetDto>> ListAsync(string userId, CancellationToken ct = default) =>
        await db.TagPresets.Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new TagPresetDto(t.Id, t.Name, Json.Deserialize<List<string>>(t.TagsJson) ?? new()))
            .ToListAsync(ct);

    public async Task<TagPresetDto?> GetAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.TagPresets.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<TagPresetDto> UpsertAsync(string userId, TagPresetUpsertRequest request, CancellationToken ct = default)
    {
        TagPreset? entity = request.Id is { } id
            ? await db.TagPresets.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct)
            : null;

        if (entity is null)
        {
            entity = new TagPreset
            {
                Id = request.Id ?? Guid.NewGuid(),
                UserId = userId,
                CreatedAt = clock.UtcNow
            };
            db.TagPresets.Add(entity);
        }

        entity.Name = request.Name;
        entity.TagsJson = Json.Serialize(request.Tags);
        entity.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.TagPresets.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct);
        if (entity is null) return false;
        db.TagPresets.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static TagPresetDto ToDto(TagPreset entity) =>
        new(entity.Id, entity.Name, Json.Deserialize<List<string>>(entity.TagsJson) ?? new());
}

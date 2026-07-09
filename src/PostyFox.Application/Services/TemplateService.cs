using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

public sealed class TemplateService(IAppDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<TemplateDto>> ListAsync(string userId, CancellationToken ct = default) =>
        await db.Templates.Where(t => t.UserId == userId)
            .OrderBy(t => t.Title)
            .Select(t => new TemplateDto(t.Id, t.Title, t.MarkdownBody))
            .ToListAsync(ct);

    public async Task<TemplateDto?> GetAsync(string userId, Guid id, CancellationToken ct = default) =>
        await db.Templates.Where(t => t.UserId == userId && t.Id == id)
            .Select(t => new TemplateDto(t.Id, t.Title, t.MarkdownBody))
            .FirstOrDefaultAsync(ct);

    public async Task<TemplateDto> UpsertAsync(string userId, TemplateUpsertRequest request, CancellationToken ct = default)
    {
        Template? entity = request.Id is { } id
            ? await db.Templates.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct)
            : null;

        if (entity is null)
        {
            entity = new Template
            {
                Id = request.Id ?? Guid.NewGuid(),
                UserId = userId,
                CreatedAt = clock.UtcNow
            };
            db.Templates.Add(entity);
        }

        entity.Title = request.Title;
        entity.MarkdownBody = request.MarkdownBody;
        entity.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        return new TemplateDto(entity.Id, entity.Title, entity.MarkdownBody);
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.Templates.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct);
        if (entity is null) return false;
        db.Templates.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

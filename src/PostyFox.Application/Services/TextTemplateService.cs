using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

/// <summary>
/// CRUD for a user's reusable text templates — named snippets referenced inline in a post as
/// <c>{{tt:Name}}</c> and resolved per delivery target at generation time (see
/// <see cref="ITemplateEngine"/> and <see cref="Posting.GenerateTargetHandler"/>).
/// </summary>
public sealed class TextTemplateService(IAppDbContext db, IClock clock)
{
    public async Task<IReadOnlyList<TextTemplateDto>> ListAsync(string userId, CancellationToken ct = default) =>
        (await db.TextTemplates.Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct))
            .Select(ToDto)
            .ToList();

    public async Task<TextTemplateDto?> GetAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.TextTemplates.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct);
        return entity is null ? null : ToDto(entity);
    }

    /// <exception cref="ConnectorValidationException">
    /// The name is blank, doesn't match the <c>{{tt:name}}</c> token's allowed characters, or is
    /// already used (case-insensitively) by another of this user's text templates.
    /// </exception>
    public async Task<TextTemplateDto> UpsertAsync(string userId, TextTemplateUpsertRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            throw new ConnectorValidationException("Text template name is required.");
        if (!TextTemplateNamePattern.IsMatch(name))
            throw new ConnectorValidationException(
                "Text template name may only contain letters, digits, underscores and hyphens (it is used as {{tt:name}}).");

        var existing = await db.TextTemplates.Where(t => t.UserId == userId).ToListAsync(ct);
        if (existing.Any(t => t.Id != request.Id && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConnectorValidationException($"A text template named '{name}' already exists.");

        var entity = request.Id is { } id ? existing.FirstOrDefault(t => t.Id == id) : null;
        if (entity is null)
        {
            entity = new TextTemplate { Id = request.Id ?? Guid.NewGuid(), UserId = userId, CreatedAt = clock.UtcNow };
            db.TextTemplates.Add(entity);
        }

        entity.Name = name;
        entity.DefaultValue = request.DefaultValue ?? string.Empty;
        entity.ConnectorValuesJson = Json.Serialize(
            (request.ConnectorValues ?? new Dictionary<Guid, string>())
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value));
        entity.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.TextTemplates.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == id, ct);
        if (entity is null) return false;
        db.TextTemplates.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Matches the token regex in <see cref="Templating.TemplateEngine"/> — kept in sync deliberately.</summary>
    private static readonly System.Text.RegularExpressions.Regex TextTemplateNamePattern = new("^[A-Za-z0-9_-]+$");

    private static TextTemplateDto ToDto(TextTemplate entity) => new(
        entity.Id,
        entity.Name,
        entity.DefaultValue,
        Json.Deserialize<Dictionary<string, string>>(entity.ConnectorValuesJson)
            ?.ToDictionary(kv => Guid.Parse(kv.Key), kv => kv.Value)
        ?? new Dictionary<Guid, string>());
}

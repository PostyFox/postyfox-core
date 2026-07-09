using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

public sealed class ApiKeyService(IAppDbContext db, IApiKeyHasher hasher, IClock clock)
{
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
    private const int KeyLength = 40;
    private const int PrefixLength = 8;

    public async Task<ApiKeyCreatedDto> CreateAsync(string userId, string? name, CancellationToken ct = default)
    {
        await EnsureUserAsync(userId, ct);

        var key = GenerateKey();
        var prefix = key[..PrefixLength];
        var entity = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Prefix = prefix,
            KeyHash = hasher.Hash(key),
            Name = name,
            CreatedAt = clock.UtcNow
        };
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync(ct);

        return new ApiKeyCreatedDto(entity.Id, key, prefix);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> ListAsync(string userId, CancellationToken ct = default)
    {
        // Ordered client-side: the per-user key set is tiny and this stays provider-agnostic
        // (SQLite cannot ORDER BY DateTimeOffset).
        var keys = await db.ApiKeys.Where(k => k.UserId == userId).ToListAsync(ct);
        return keys
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto(k.Id, k.Prefix, k.Name, k.CreatedAt, k.RevokedAt))
            .ToList();
    }

    public async Task<bool> RevokeAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.ApiKeys.FirstOrDefaultAsync(k => k.UserId == userId && k.Id == id, ct);
        if (entity is null || entity.RevokedAt is not null) return false;
        entity.RevokedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Validates a presented API key and returns the owning user id, or null.</summary>
    public async Task<string?> ValidateAsync(string presentedKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(presentedKey) || presentedKey.Length < PrefixLength) return null;
        var prefix = presentedKey[..PrefixLength];

        var candidates = await db.ApiKeys
            .Where(k => k.Prefix == prefix && k.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var candidate in candidates)
        {
            if (hasher.Verify(presentedKey, candidate.KeyHash))
            {
                candidate.LastUsedAt = clock.UtcNow;
                await db.SaveChangesAsync(ct);
                return candidate.UserId;
            }
        }
        return null;
    }

    private async Task EnsureUserAsync(string userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            db.Users.Add(new User { Id = userId, CreatedAt = clock.UtcNow });
        }
    }

    private static string GenerateKey()
    {
        var chars = new char[KeyLength];
        for (var i = 0; i < KeyLength; i++)
            chars[i] = Chars[RandomNumberGenerator.GetInt32(Chars.Length)];
        return new string(chars);
    }
}

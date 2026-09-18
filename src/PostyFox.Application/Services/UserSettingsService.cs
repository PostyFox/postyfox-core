using Microsoft.EntityFrameworkCore;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;

namespace PostyFox.Application.Services;

/// <summary>Per-user preferences. A user with no row yet gets the defaults.</summary>
public sealed class UserSettingsService(IAppDbContext db, IClock clock)
{
    public async Task<UserSettingsDto> GetAsync(string userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return new UserSettingsDto(user?.IncludeAdvertisingLine ?? false);
    }

    public async Task<UserSettingsDto> UpdateAsync(string userId, UserSettingsUpdateRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            user = new User { Id = userId, CreatedAt = clock.UtcNow };
            db.Users.Add(user);
        }

        user.IncludeAdvertisingLine = request.IncludeAdvertisingLine;
        await db.SaveChangesAsync(ct);
        return new UserSettingsDto(user.IncludeAdvertisingLine);
    }
}

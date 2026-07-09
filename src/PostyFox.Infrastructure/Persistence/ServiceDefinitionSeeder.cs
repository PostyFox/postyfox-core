using Microsoft.EntityFrameworkCore;
using PostyFox.Domain.Entities;

namespace PostyFox.Infrastructure.Persistence;

/// <summary>
/// Seeds the platform catalogue (equivalent to the legacy AvailableServices table). Only
/// DiscordWH has a working connector in Phase 2; the others are catalogue entries whose
/// connectors land in Phase 3.
/// </summary>
public static class ServiceDefinitionSeeder
{
    public static readonly ServiceDefinition[] Definitions =
    [
        new() { Id = "DiscordWH", Name = "Discord Web Hook", Platform = "DiscordWH", Enabled = true,
                ConfigSchema = "{\"Webhook\":\"\"}" },
        new() { Id = "Telegram", Name = "Telegram", Platform = "Telegram", Enabled = true,
                ConfigSchema = "{\"PhoneNumber\":\"\",\"DefaultPostingTarget\":\"\"}" },
        new() { Id = "BlueSky", Name = "BlueSky", Platform = "BlueSky", Enabled = true,
                ConfigSchema = "{\"Handle\":\"\"}", SecureConfigSchema = "{\"AppPassword\":\"\"}" },
        new() { Id = "Tumblr", Name = "Tumblr", Platform = "Tumblr", Enabled = true,
                ConfigSchema = "{\"Username\":\"\"}", SecureConfigSchema = "{\"OAuthAccessToken\":\"\",\"OAuthRefreshToken\":\"\"}" },
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        foreach (var def in Definitions)
        {
            var existing = await db.ServiceDefinitions.FirstOrDefaultAsync(s => s.Id == def.Id, ct);
            if (existing is null)
            {
                db.ServiceDefinitions.Add(def);
            }
            else
            {
                existing.Name = def.Name;
                existing.Platform = def.Platform;
                existing.Enabled = def.Enabled;
                existing.ConfigSchema = def.ConfigSchema;
                existing.SecureConfigSchema = def.SecureConfigSchema;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}

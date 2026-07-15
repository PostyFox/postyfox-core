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
    // Config/secret schemas are JSON objects keyed by field name; each value is a *field descriptor*
    // carrying both presentation (label/help/placeholder/type/link) and validation (required/pattern/
    // message/min-maxLength) metadata. The frontend renders + pre-validates from these; the server
    // enforces the validation keys authoritatively (see ConfigSchemaValidator). Adding/changing a
    // field's behaviour is a server-only change — the client needs no edits.

    private const string DiscordSchema = """
        { "Webhook": {
            "label": "Webhook URL", "type": "url", "required": true,
            "placeholder": "https://discord.com/api/webhooks/…",
            "help": "Server Settings → Integrations → Webhooks → New Webhook → Copy URL.",
            "link": { "href": "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks", "text": "How to create a webhook" }
        } }
        """;

    private const string TelegramSchema = """
        { "PhoneNumber": {
            "label": "Phone number", "type": "tel", "required": true,
            "placeholder": "+1234567890",
            "help": "The phone number of the Telegram account to post as."
          },
          "DefaultPostingTarget": {
            "label": "Default posting target",
            "placeholder": "@mychannel or chat id",
            "help": "The chat/channel posts go to by default."
        } }
        """;

    // Bluesky handles must NOT carry a leading "@" — the AT Protocol handle resolver rejects it.
    private const string BlueSkyConfigSchema = """
        { "Handle": {
            "label": "Handle", "required": true,
            "placeholder": "yourname.bsky.social",
            "help": "Your Bluesky handle.",
            "pattern": "^[^@]",
            "message": "Enter your handle without a leading “@” (e.g. yourname.bsky.social)."
        } }
        """;

    private const string BlueSkySecureSchema = """
        { "AppPassword": {
            "label": "App password", "type": "password", "required": true,
            "help": "Create a dedicated app password — never use your main password.",
            "link": { "href": "https://bsky.app/settings/app-passwords", "text": "bsky.app/settings/app-passwords" }
        } }
        """;

    private const string TumblrSchema = """
        { "Username": {
            "label": "Blog username", "required": true,
            "placeholder": "yourblog",
            "help": "The Tumblr blog to post to."
        } }
        """;

    public static readonly ServiceDefinition[] Definitions =
    [
        new() { Id = "DiscordWH", Name = "Discord Web Hook", Platform = "DiscordWH", Enabled = true,
                ConfigSchema = DiscordSchema },
        new() { Id = "Telegram", Name = "Telegram", Platform = "Telegram", Enabled = true,
                ConfigSchema = TelegramSchema },
        new() { Id = "BlueSky", Name = "BlueSky", Platform = "BlueSky", Enabled = true,
                ConfigSchema = BlueSkyConfigSchema, SecureConfigSchema = BlueSkySecureSchema },
        // Tumblr credentials are obtained via the OAuth "connect" flow (SupportsOAuth), not entered
        // by hand — so there is no user-facing secure config schema.
        new() { Id = "Tumblr", Name = "Tumblr", Platform = "Tumblr", Enabled = true,
                ConfigSchema = TumblrSchema, SecureConfigSchema = null },
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

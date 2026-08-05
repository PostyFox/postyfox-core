using System.Text.Encodings.Web;
using System.Text.Json;
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

    // FurAffinity's category/theme/species/gender fields are numeric IDs chosen from fixed lists on
    // its submission form. Presenting them as `options` on the descriptors turns five "enter an ID"
    // boxes into five named dropdowns. The lists run to ~500 entries, so they live in an embedded
    // JSON file rather than a literal here — see Persistence/Schemas/README.md for provenance and
    // how to regenerate them when FurAffinity changes its form.
    private static readonly string FurAffinityConfigSchema = Minified("furaffinity.schema.json");

    // Shared by every Fediverse platform (Mastodon, Pleroma, Pixelfed, …). The connect (OAuth/MiAuth)
    // flow yields the access token, so there is no user-facing secure schema. https:// is added
    // automatically when the scheme is omitted.
    private const string FediverseSchema = """
        { "InstanceUrl": {
            "label": "Instance URL", "type": "url", "required": true,
            "placeholder": "https://your.instance",
            "help": "The URL of the server your account is on."
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
        new() { Id = "FurAffinity", Name = "FurAffinity", Platform = "FurAffinity", Enabled = true,
                ConfigSchema = FurAffinityConfigSchema, SecureConfigSchema = null },
        // Fediverse platforms — credentials come from the OAuth/MiAuth "connect" flow (SupportsOAuth),
        // not entered by hand, so there is no user-facing secure config schema. All share one config
        // schema (just the instance URL); the connector auto-detects the server software at connect.
        new() { Id = "Mastodon", Name = "Mastodon", Platform = "Mastodon", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Pleroma", Name = "Pleroma", Platform = "Pleroma", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Akkoma", Name = "Akkoma", Platform = "Akkoma", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Friendica", Name = "Friendica", Platform = "Friendica", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Firefish", Name = "Firefish", Platform = "Firefish", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Iceshrimp", Name = "Iceshrimp", Platform = "Iceshrimp", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "GoToSocial", Name = "GoToSocial", Platform = "GoToSocial", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Hometown", Name = "Hometown", Platform = "Hometown", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Pixelfed", Name = "Pixelfed", Platform = "Pixelfed", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
    ];

    /// <summary>
    /// Reads an embedded schema and drops its formatting. The file on disk is pretty-printed so it can
    /// be reviewed and diffed; the copy stored in the database and served to clients has no need for
    /// 20 KB of indentation. Parsing here also fails the boot if a schema is ever left malformed.
    /// </summary>
    private static string Minified(string fileName)
    {
        var name = $"PostyFox.Infrastructure.Persistence.Schemas.{fileName}";
        using var stream = typeof(ServiceDefinitionSeeder).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded config schema '{name}' is missing.");
        using var doc = JsonDocument.Parse(stream);
        return JsonSerializer.Serialize(
            doc.RootElement,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

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
